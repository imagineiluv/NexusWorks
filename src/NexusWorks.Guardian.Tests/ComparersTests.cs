using FluentAssertions;
using NexusWorks.Guardian.Comparison;
using NexusWorks.Guardian.Tests.TestSupport;

namespace NexusWorks.Guardian.Tests;

[Trait("Category", "Comparison")]
public class ComparersTests
{
    [Fact]
    public void XmlComparer_should_ignore_comments_whitespace_and_attribute_order()
    {
        using var artifacts = new TestArtifactFactory();
        var current = artifacts.WriteTextFile("current.xml", "<root enabled=\"true\" version=\"1\"><name> Guardian </name><!-- note --></root>");
        var patch = artifacts.WriteTextFile("patch.xml", "<root version=\"1\" enabled=\"true\"><name>Guardian</name></root>");

        var comparer = new XmlComparer();
        var detail = comparer.Compare(current, patch);

        detail.IsEquivalent.Should().BeTrue();
        detail.ChangedXPaths.Should().BeEmpty();
    }

    [Fact]
    public void JarComparer_should_detect_added_removed_and_changed_entries()
    {
        using var artifacts = new TestArtifactFactory();
        var current = artifacts.WriteJar("current.jar", new Dictionary<string, string>
        {
            ["META-INF/MANIFEST.MF"] = "Version: 1",
            ["BOOT-INF/classes/A.txt"] = "same",
            ["BOOT-INF/classes/B.txt"] = "remove-me",
            ["BOOT-INF/classes/com/nexus/guardian/LegacyRule.class"] = "legacy",
        });
        var patch = artifacts.WriteJar("patch.jar", new Dictionary<string, string>
        {
            ["META-INF/MANIFEST.MF"] = "Version: 2",
            ["BOOT-INF/classes/A.txt"] = "same",
            ["BOOT-INF/classes/C.txt"] = "add-me",
            ["BOOT-INF/classes/com/nexus/guardian/LegacyRule.class"] = "legacy-v2",
            ["BOOT-INF/classes/com/nexus/guardian/PolicyRule.class"] = "policy",
        });

        var comparer = new JarComparer();
        var detail = comparer.Compare(current, patch);

        detail.ManifestChanged.Should().BeTrue();
        detail.AddedEntries.Should().Contain("BOOT-INF/classes/C.txt");
        detail.RemovedEntries.Should().Contain("BOOT-INF/classes/B.txt");
        detail.ChangedEntries.Should().Contain("META-INF/MANIFEST.MF");
        detail.AddedClassCount.Should().Be(1);
        detail.RemovedClassCount.Should().Be(0);
        detail.ChangedClassCount.Should().Be(1);
        detail.PackageSummaries.Should().ContainSingle(summary => summary.PackageName == "com.nexus.guardian");
        detail.PackageSummaries[0].TotalChangedCount.Should().Be(2);
    }

    [Fact]
    public void YamlComparer_should_ignore_comments_and_key_order()
    {
        using var artifacts = new TestArtifactFactory();
        var current = artifacts.WriteTextFile("current.yaml", """
root:
  service:
    port: 8080
    enabled: true
""");
        var patch = artifacts.WriteTextFile("patch.yaml", """
# comment
root:
  service:
    enabled: true
    port: 8080
""");

        var comparer = new YamlComparer();
        var detail = comparer.Compare(current, patch);

        detail.IsEquivalent.Should().BeTrue();
        detail.ChangedPaths.Should().BeEmpty();
    }

    [Fact]
    public void YamlComparer_should_detect_changed_paths()
    {
        using var artifacts = new TestArtifactFactory();
        var current = artifacts.WriteTextFile("current.yaml", """
root:
  service:
    port: 8080
    features:
      - auth
""");
        var patch = artifacts.WriteTextFile("patch.yaml", """
root:
  service:
    port: 9090
    features:
      - auth
      - audit
""");

        var comparer = new YamlComparer();
        var detail = comparer.Compare(current, patch);

        detail.IsEquivalent.Should().BeFalse();
        detail.ChangedPaths.Should().Contain("root.service.port");
        detail.ChangedPaths.Should().Contain("root.service.features[1]");
        detail.AddedKeys.Should().Be(1);
    }

    [Fact]
    public void XmlComparer_should_capture_detailed_change_entries()
    {
        using var artifacts = new TestArtifactFactory();
        var current = artifacts.WriteTextFile("current.xml", "<root><item enabled=\"true\">alpha</item></root>");
        var patch = artifacts.WriteTextFile("patch.xml", "<root><item enabled=\"false\">beta</item><extra /></root>");

        var comparer = new XmlComparer();
        var detail = comparer.Compare(current, patch);

        detail.ChangedXPaths.Should().Contain("/root[1]/item[1]/@attributes");
        detail.Changes.Should().Contain(change => change.ChangeKind == "attributes" && change.Path == "/root[1]/item[1]/@attributes");
        detail.Changes.Should().Contain(change => change.ChangeKind == "text" && change.Path == "/root[1]/item[1]/text()");
        detail.Changes.Should().Contain(change => change.ChangeKind == "added" && change.Path == "/root[1]/extra[1]");
    }
}
