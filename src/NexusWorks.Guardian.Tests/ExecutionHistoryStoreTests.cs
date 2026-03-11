using FluentAssertions;
using NexusWorks.Guardian.Baseline;
using NexusWorks.Guardian.Comparison;
using NexusWorks.Guardian.Evaluation;
using NexusWorks.Guardian.Inventory;
using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.Orchestration;
using NexusWorks.Guardian.Reporting;
using NexusWorks.Guardian.RuleResolution;
using NexusWorks.Guardian.Tests.TestSupport;

namespace NexusWorks.Guardian.Tests;

public class ExecutionHistoryStoreTests
{
    [Fact]
    public void History_store_should_list_and_load_recent_reports()
    {
        using var artifacts = new TestArtifactFactory();
        var outputRoot = artifacts.CreateDirectory("output");

        var firstReport = ExecuteRun(artifacts, outputRoot, "History One", replicas: 2);
        var secondReport = ExecuteRun(artifacts, outputRoot, "History Two", replicas: 3);

        var historyStore = new FileSystemExecutionHistoryStore();
        var history = historyStore.ListRecent(outputRoot, maxCount: 10);

        history.Should().HaveCountGreaterThanOrEqualTo(2);
        history[0].ReportTitle.Should().Be("History Two");
        history.Should().Contain(entry => entry.ExecutionId == firstReport.Summary.ExecutionId);

        var loaded = historyStore.Load(secondReport.Artifacts.JsonResultPath);

        loaded.Should().NotBeNull();
        loaded!.ReportTitle.Should().Be("History Two");
        loaded.Result.Items.Should().Contain(item => item.RelativePath == "conf/settings.yaml");
    }

    private static ExecutionReport ExecuteRun(TestArtifactFactory artifacts, string outputRoot, string reportTitle, int replicas)
    {
        var rootPrefix = reportTitle.Replace(" ", "-");
        var currentRoot = artifacts.CreateDirectory($"{rootPrefix}/current");
        var patchRoot = artifacts.CreateDirectory($"{rootPrefix}/patch");

        Directory.CreateDirectory(Path.Combine(currentRoot, "conf"));
        Directory.CreateDirectory(Path.Combine(patchRoot, "conf"));
        File.WriteAllText(Path.Combine(currentRoot, "conf", "settings.yaml"), """
app:
  region: ap-northeast-2
  replicas: 1
""");
        File.WriteAllText(Path.Combine(patchRoot, "conf", "settings.yaml"), $$"""
app:
  region: ap-northeast-2
  replicas: {{replicas}}
""");

        var baselinePath = artifacts.WriteBaselineWorkbook(
            $"{rootPrefix}/baseline.xlsx",
            [
                new BaselineRule("R001", "conf/settings.yaml", null, GuardianFileType.Yaml, false, CompareMode.Hash | CompareMode.YamlStructure, false, false, 1, null),
            ]);

        var runner = new GuardianExecutionRunner(
            new GuardianComparisonEngine(
                new ClosedXmlBaselineReader(),
                new BaselineValidator(),
                new FileSystemInventoryScanner(new Sha256HashProvider()),
                new BaselineRuleResolver(),
                new GuardianFileComparer(new JarComparer(), new XmlComparer(), new YamlComparer(), new StatusEvaluator())),
            new GuardianReportService(
                new ResultAggregator(),
                new StaticHtmlReportWriter(),
                new ClosedXmlExcelReportWriter()));

        return runner.ExecuteAndWriteReports(
            new ComparisonExecutionRequest(currentRoot, patchRoot, baselinePath),
            outputRoot,
            reportTitle);
    }
}
