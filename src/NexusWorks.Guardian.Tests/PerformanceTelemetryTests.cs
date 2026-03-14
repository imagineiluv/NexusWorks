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

[Trait("Category", "Infrastructure")]
public class PerformanceTelemetryTests
{
    [Fact]
    public void Engine_should_capture_stage_metrics_for_large_dataset()
    {
        using var artifacts = new TestArtifactFactory();
        var currentRoot = artifacts.CreateDirectory("current");
        var patchRoot = artifacts.CreateDirectory("patch");

        for (var index = 0; index < 120; index++)
        {
            artifacts.WriteTextFile(Path.Combine("current", "bin", $"file-{index:000}.txt"), $"current-{index}");
            artifacts.WriteTextFile(Path.Combine("patch", "bin", $"file-{index:000}.txt"), index % 4 == 0 ? $"patch-{index}" : $"current-{index}");
        }

        for (var index = 0; index < 20; index++)
        {
            artifacts.WriteTextFile(
                Path.Combine("current", "conf", $"app-{index:000}.xml"),
                $"<app version=\"1\"><name>guardian-{index}</name></app>");
            artifacts.WriteTextFile(
                Path.Combine("patch", "conf", $"app-{index:000}.xml"),
                $"<app version=\"{(index % 2 == 0 ? 2 : 1)}\"><name>guardian-{index}</name></app>");
            artifacts.WriteTextFile(
                Path.Combine("current", "config", $"settings-{index:000}.yaml"),
                $"service:{Environment.NewLine}  replicas: 2{Environment.NewLine}  shard: {index}{Environment.NewLine}");
            artifacts.WriteTextFile(
                Path.Combine("patch", "config", $"settings-{index:000}.yaml"),
                $"service:{Environment.NewLine}  replicas: {(index % 3 == 0 ? 3 : 2)}{Environment.NewLine}  shard: {index}{Environment.NewLine}");
        }

        var baselinePath = artifacts.WriteBaselineWorkbook(
            "baseline.xlsx",
            [
                new BaselineRule("R-XML", null, "conf/*.xml", GuardianFileType.Xml, false, CompareMode.Hash | CompareMode.XmlStructure, false, false, 1, null),
                new BaselineRule("R-YAML", null, "config/*.yaml", GuardianFileType.Yaml, false, CompareMode.Hash | CompareMode.YamlStructure, false, false, 2, null),
            ]);

        var engine = new GuardianComparisonEngine(
            new ClosedXmlBaselineReader(),
            new BaselineValidator(),
            new FileSystemInventoryScanner(new Sha256HashProvider()),
            new BaselineRuleResolver(),
            new GuardianFileComparer(new JarComparer(), new XmlComparer(), new YamlComparer(), new StatusEvaluator()));

        var result = engine.Execute(new ComparisonExecutionRequest(currentRoot, patchRoot, baselinePath));

        result.Performance.Should().NotBeNull();
        result.TotalDurationMs.Should().BeGreaterThan(0);
        result.StageMetrics.Should().Contain(stage => stage.StageName == "Current Inventory Scan" && stage.ItemCount == 160);
        result.StageMetrics.Should().Contain(stage => stage.StageName == "Patch Inventory Scan" && stage.ItemCount == 160);
        result.StageMetrics.Should().Contain(stage => stage.StageName == "Candidate Compare" && stage.ItemCount == result.Items.Count);
        result.StageMetrics.Should().OnlyContain(stage => stage.Concurrency >= 1 && stage.DurationMs >= 0);
        result.StageMetrics.Single(stage => stage.StageName == "Candidate Compare").ItemsPerSecond.Should().BeGreaterThan(0);
        result.Performance!.PeakConcurrency.Should().BeGreaterThan(1);
    }
}
