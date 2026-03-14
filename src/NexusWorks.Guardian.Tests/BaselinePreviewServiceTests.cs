using FluentAssertions;
using NexusWorks.Guardian.Baseline;
using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.Tests.TestSupport;

namespace NexusWorks.Guardian.Tests;

[Trait("Category", "Baseline")]
public class BaselinePreviewServiceTests
{
    [Fact]
    public void Preview_service_should_summarize_rule_counts()
    {
        using var artifacts = new TestArtifactFactory();
        var baselinePath = artifacts.WriteBaselineWorkbook(
            "baseline.xlsx",
            [
                new BaselineRule("R001", "conf/app.xml", null, GuardianFileType.Xml, true, CompareMode.Hash | CompareMode.XmlStructure, false, false, 1, null),
                new BaselineRule("R002", null, "lib/*.jar", GuardianFileType.Jar, false, CompareMode.Hash | CompareMode.JarEntry, false, false, 2, null),
                new BaselineRule("R003", "conf/settings.yaml", null, GuardianFileType.Yaml, false, CompareMode.Hash | CompareMode.YamlStructure, false, true, 3, null),
            ]);

        var previewService = new BaselinePreviewService(new ClosedXmlBaselineReader(), new BaselineValidator());

        var summary = previewService.Load(baselinePath);

        summary.TotalRuleCount.Should().Be(3);
        summary.RequiredRuleCount.Should().Be(1);
        summary.ExcludedRuleCount.Should().Be(1);
        summary.FileTypeCounts.Should().Contain([
            new KeyValuePair<string, int>(GuardianFileType.Jar.ToString(), 1),
            new KeyValuePair<string, int>(GuardianFileType.Xml.ToString(), 1),
            new KeyValuePair<string, int>(GuardianFileType.Yaml.ToString(), 1),
        ]);
    }
}
