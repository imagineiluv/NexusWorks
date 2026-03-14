using FluentAssertions;
using NexusWorks.Guardian.Baseline;
using NexusWorks.Guardian.Comparison;
using NexusWorks.Guardian.Evaluation;
using NexusWorks.Guardian.Inventory;
using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.Orchestration;
using NexusWorks.Guardian.RuleResolution;
using NexusWorks.Guardian.Tests.TestSupport;

namespace NexusWorks.Guardian.Tests;

[Trait("Category", "Integration")]
public class GuardianComparisonEngineTests
{
    [Fact]
    public void Engine_should_return_primary_statuses_for_sample_dataset()
    {
        using var artifacts = new TestArtifactFactory();
        var currentRoot = artifacts.CreateDirectory("current");
        var patchRoot = artifacts.CreateDirectory("patch");

        File.WriteAllText(Path.Combine(currentRoot, "same.txt"), "same");
        Directory.CreateDirectory(Path.Combine(currentRoot, "conf"));
        File.WriteAllText(Path.Combine(currentRoot, "conf", "app.xml"), "<app enabled=\"true\" version=\"1\"><name>guardian</name></app>");
        File.WriteAllText(Path.Combine(currentRoot, "conf", "settings.yaml"), """
app:
  region: ap-northeast-2
  replicas: 2
""");
        File.WriteAllText(Path.Combine(currentRoot, "only-current.txt"), "remove me");

        File.WriteAllText(Path.Combine(patchRoot, "same.txt"), "same");
        Directory.CreateDirectory(Path.Combine(patchRoot, "conf"));
        File.WriteAllText(Path.Combine(patchRoot, "conf", "app.xml"), "<app version=\"2\" enabled=\"true\"><name>guardian</name></app>");
        File.WriteAllText(Path.Combine(patchRoot, "conf", "settings.yaml"), """
app:
  region: ap-northeast-2
  replicas: 3
""");
        File.WriteAllText(Path.Combine(patchRoot, "only-patch.txt"), "add me");

        var baselinePath = artifacts.WriteBaselineWorkbook(
            "baseline.xlsx",
            [
                new BaselineRule("R001", "conf/app.xml", null, GuardianFileType.Xml, false, CompareMode.Hash | CompareMode.XmlStructure, false, false, 1, null),
                new BaselineRule("R002", "conf/required.xml", null, GuardianFileType.Xml, true, CompareMode.Hash | CompareMode.XmlStructure, false, false, 1, null),
                new BaselineRule("R003", null, "missing/*.jar", GuardianFileType.Jar, true, CompareMode.Hash | CompareMode.JarEntry, false, false, 2, null),
                new BaselineRule("R004", "conf/settings.yaml", null, GuardianFileType.Yaml, false, CompareMode.Hash | CompareMode.YamlStructure, false, false, 2, null),
            ]);

        var hashProvider = new Sha256HashProvider();
        var engine = new GuardianComparisonEngine(
            new ClosedXmlBaselineReader(),
            new BaselineValidator(),
            new FileSystemInventoryScanner(hashProvider),
            new BaselineRuleResolver(),
            new GuardianFileComparer(new JarComparer(), new XmlComparer(), new YamlComparer(), new StatusEvaluator()));

        var result = engine.Execute(new ComparisonExecutionRequest(currentRoot, patchRoot, baselinePath));
        var byPath = result.Items.ToDictionary(item => item.RelativePath, StringComparer.OrdinalIgnoreCase);

        byPath["same.txt"].Status.Should().Be(CompareStatus.Ok);
        byPath["conf/app.xml"].Status.Should().Be(CompareStatus.Changed);
        byPath["conf/settings.yaml"].Status.Should().Be(CompareStatus.Changed);
        byPath["only-patch.txt"].Status.Should().Be(CompareStatus.Added);
        byPath["only-current.txt"].Status.Should().Be(CompareStatus.Removed);
        byPath["conf/required.xml"].Status.Should().Be(CompareStatus.MissingRequired);
        byPath["missing/*.jar"].Status.Should().Be(CompareStatus.MissingRequired);
    }
}
