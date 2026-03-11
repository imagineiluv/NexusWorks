using ClosedXML.Excel;
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

public class ReportGenerationTests
{
    [Fact]
    public void Runner_should_generate_html_excel_json_and_log_outputs()
    {
        using var artifacts = new TestArtifactFactory();
        var currentRoot = artifacts.CreateDirectory("current");
        var patchRoot = artifacts.CreateDirectory("patch");
        var outputRoot = artifacts.CreateDirectory("output");

        File.WriteAllText(Path.Combine(currentRoot, "same.txt"), "same");
        Directory.CreateDirectory(Path.Combine(currentRoot, "conf"));
        File.WriteAllText(Path.Combine(currentRoot, "conf", "app.xml"), "<app enabled=\"true\" version=\"1\"><name>guardian</name></app>");
        File.WriteAllText(Path.Combine(currentRoot, "conf", "settings.yaml"), """
app:
  region: ap-northeast-2
  replicas: 2
""");

        File.WriteAllText(Path.Combine(patchRoot, "same.txt"), "same");
        Directory.CreateDirectory(Path.Combine(patchRoot, "conf"));
        File.WriteAllText(Path.Combine(patchRoot, "conf", "app.xml"), "<app version=\"2\" enabled=\"true\"><name>guardian</name></app>");
        File.WriteAllText(Path.Combine(patchRoot, "conf", "settings.yaml"), """
app:
  region: ap-northeast-2
  replicas: 4
""");

        var baselinePath = artifacts.WriteBaselineWorkbook(
            "baseline.xlsx",
            [
                new BaselineRule("R001", "conf/app.xml", null, GuardianFileType.Xml, false, CompareMode.Hash | CompareMode.XmlStructure, false, false, 1, null),
                new BaselineRule("R002", "conf/required.xml", null, GuardianFileType.Xml, true, CompareMode.Hash | CompareMode.XmlStructure, false, false, 2, null),
                new BaselineRule("R003", "conf/settings.yaml", null, GuardianFileType.Yaml, false, CompareMode.Hash | CompareMode.YamlStructure, false, false, 3, null),
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

        var report = runner.ExecuteAndWriteReports(
            new ComparisonExecutionRequest(currentRoot, patchRoot, baselinePath),
            outputRoot,
            "Guardian Smoke");

        File.Exists(report.Artifacts.HtmlReportPath).Should().BeTrue();
        File.Exists(report.Artifacts.ExcelReportPath).Should().BeTrue();
        File.Exists(report.Artifacts.JsonResultPath).Should().BeTrue();
        File.Exists(report.Artifacts.LogPath).Should().BeTrue();
        report.Artifacts.OutputDirectory.Should().Contain(Path.Combine("output", "guardian"));

        File.ReadAllText(report.Artifacts.HtmlReportPath).Should().Contain("Guardian Smoke").And.Contain("conf/app.xml").And.Contain("conf/settings.yaml");
        File.ReadAllText(report.Artifacts.JsonResultPath).Should().Contain("Guardian Smoke").And.Contain("report.html");
        File.ReadAllText(report.Artifacts.LogPath).Should().Contain("ExecutionId=").And.Contain("StatusCounts=").And.Contain("TotalDurationMs=").And.Contain("StageMetrics=");

        report.Summary.Performance.Should().NotBeNull();
        report.Summary.StageMetrics.Should().Contain(stage => stage.StageName == "Candidate Compare");

        using var workbook = new XLWorkbook(report.Artifacts.ExcelReportPath);
        workbook.TryGetWorksheet("SUMMARY", out _).Should().BeTrue();
        workbook.TryGetWorksheet("DETAIL", out _).Should().BeTrue();
        workbook.TryGetWorksheet("JAR_DETAIL", out _).Should().BeTrue();
        workbook.TryGetWorksheet("XML_DETAIL", out _).Should().BeTrue();
        workbook.TryGetWorksheet("YAML_DETAIL", out _).Should().BeTrue();
        workbook.Worksheet("SUMMARY").Cell(6, 1).GetString().Should().Be("TotalDurationMs");
        workbook.Worksheet("SUMMARY").Cell(7, 1).GetString().Should().Be("PeakConcurrency");
        workbook.Worksheet("DETAIL").RowsUsed().Count().Should().Be(report.Result.Items.Count + 1);
    }
}
