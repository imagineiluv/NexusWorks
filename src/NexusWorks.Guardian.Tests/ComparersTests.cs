using FluentAssertions;
using NexusWorks.Guardian.Comparison;
using NexusWorks.Guardian.Inventory;
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
    public void XmlComparer_should_treat_reordered_sibling_elements_as_equivalent()
    {
        using var artifacts = new TestArtifactFactory();
        var current = artifacts.WriteTextFile("current.xml",
            "<root><item id=\"a\">alpha</item><item id=\"b\">beta</item><item id=\"c\">gamma</item></root>");
        var patch = artifacts.WriteTextFile("patch.xml",
            "<root><item id=\"c\">gamma</item><item id=\"a\">alpha</item><item id=\"b\">beta</item></root>");

        var comparer = new XmlComparer();
        var detail = comparer.Compare(current, patch);

        detail.IsEquivalent.Should().BeTrue("reordering siblings with matching keys should be treated as equivalent");
        detail.ChangedXPaths.Should().BeEmpty();
    }

    [Fact]
    public void XmlComparer_should_detect_real_changes_among_reordered_siblings()
    {
        using var artifacts = new TestArtifactFactory();
        // Canonical key includes attributes AND text, so changing text produces a different key.
        // item id="a" with text "alpha" vs "CHANGED" → keys differ → one removed + one added.
        var current = artifacts.WriteTextFile("current.xml",
            "<root><item id=\"a\">alpha</item><item id=\"b\">beta</item></root>");
        var patch = artifacts.WriteTextFile("patch.xml",
            "<root><item id=\"b\">beta</item><item id=\"a\">CHANGED</item></root>");

        var comparer = new XmlComparer();
        var detail = comparer.Compare(current, patch);

        detail.IsEquivalent.Should().BeFalse("text content of item id=a has changed");
        detail.AddedNodes.Should().BeGreaterThanOrEqualTo(1);
        detail.RemovedNodes.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void XmlComparer_should_handle_mixed_element_names_with_reordering()
    {
        using var artifacts = new TestArtifactFactory();
        var current = artifacts.WriteTextFile("current.xml",
            "<config><db host=\"localhost\" /><cache ttl=\"60\" /><log level=\"info\" /></config>");
        var patch = artifacts.WriteTextFile("patch.xml",
            "<config><log level=\"info\" /><db host=\"localhost\" /><cache ttl=\"60\" /></config>");

        var comparer = new XmlComparer();
        var detail = comparer.Compare(current, patch);

        detail.IsEquivalent.Should().BeTrue("elements with different names reordered should still match");
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

    // ── Hash Cache Tests ──

    [Fact]
    public void CachingHashProvider_should_return_cached_hash_for_unchanged_file()
    {
        using var artifacts = new TestArtifactFactory();
        var filePath = artifacts.WriteTextFile("data.txt", "hello");

        var inner = new Sha256HashProvider();
        var caching = new CachingHashProvider(inner);

        var first = caching.Compute(filePath);
        var second = caching.Compute(filePath);

        first.Should().Be(second);
        caching.CacheHits.Should().Be(1);
        caching.CacheMisses.Should().Be(1);
    }

    [Fact]
    public void CachingHashProvider_should_recompute_hash_when_file_changes()
    {
        using var artifacts = new TestArtifactFactory();
        var filePath = artifacts.WriteTextFile("data.txt", "hello");

        var inner = new Sha256HashProvider();
        var caching = new CachingHashProvider(inner);

        var first = caching.Compute(filePath);

        // Modify file (ensure different timestamp)
        Thread.Sleep(50);
        File.WriteAllText(filePath, "world");
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddSeconds(1));

        var second = caching.Compute(filePath);

        first.Should().NotBe(second);
        caching.CacheMisses.Should().Be(2);
    }

    [Fact]
    public void CachingHashProvider_should_persist_and_reload_cache()
    {
        using var artifacts = new TestArtifactFactory();
        var filePath = artifacts.WriteTextFile("data.txt", "persist-test");
        var cachePath = Path.Combine(artifacts.RootPath, "cache.json");

        var inner = new Sha256HashProvider();
        var caching1 = new CachingHashProvider(inner);
        var expectedHash = caching1.Compute(filePath);
        caching1.SaveTo(cachePath);

        // Load cache into a new provider
        var loadedCache = CachingHashProvider.LoadFrom(cachePath);
        var caching2 = new CachingHashProvider(inner, loadedCache);
        var cachedHash = caching2.Compute(filePath);

        cachedHash.Should().Be(expectedHash);
        caching2.CacheHits.Should().Be(1);
    }
}
