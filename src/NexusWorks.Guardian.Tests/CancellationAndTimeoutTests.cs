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

[Trait("Category", "Cancellation")]
public class CancellationAndTimeoutTests
{
    // ── CancellationToken in Baseline Pipeline ──

    [Fact]
    public void BaselineReader_should_respect_cancellation_token()
    {
        using var artifacts = new TestArtifactFactory();
        var baselinePath = artifacts.WriteBaselineWorkbook("baseline.xlsx",
        [
            new BaselineRule("R001", "conf/app.xml", null, GuardianFileType.Xml,
                false, CompareMode.Hash, false, false, 1, null),
        ]);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var reader = new ClosedXmlBaselineReader();
        var act = () => reader.Read(baselinePath, cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void BaselineValidator_should_respect_cancellation_token()
    {
        var rules = new[]
        {
            new BaselineRule("R001", "conf/app.xml", null, GuardianFileType.Xml,
                false, CompareMode.Hash, false, false, 1, null),
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var validator = new BaselineValidator();
        var act = () => validator.Validate(rules, cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void BaselinePreviewService_should_respect_cancellation_token()
    {
        using var artifacts = new TestArtifactFactory();
        var baselinePath = artifacts.WriteBaselineWorkbook("baseline.xlsx",
        [
            new BaselineRule("R001", "conf/app.xml", null, GuardianFileType.Xml,
                false, CompareMode.Hash, false, false, 1, null),
        ]);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var service = new BaselinePreviewService(new ClosedXmlBaselineReader(), new BaselineValidator());
        var act = () => service.Load(baselinePath, cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    // ── CancellationToken in Engine ──

    [Fact]
    public void Engine_should_respect_cancellation_token()
    {
        using var artifacts = new TestArtifactFactory();
        var currentRoot = artifacts.CreateDirectory("current");
        var patchRoot = artifacts.CreateDirectory("patch");
        File.WriteAllText(Path.Combine(currentRoot, "test.txt"), "content");
        File.WriteAllText(Path.Combine(patchRoot, "test.txt"), "content");

        var baselinePath = artifacts.WriteBaselineWorkbook("baseline.xlsx",
        [
            new BaselineRule("R001", "test.txt", null, GuardianFileType.Auto,
                false, CompareMode.Hash, false, false, 1, null),
        ]);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var engine = CreateEngine();
        var request = new ComparisonExecutionRequest(currentRoot, patchRoot, baselinePath);

        var act = () => engine.Execute(request, cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    // ── Timeout Mechanism ──

    [Fact]
    public void ExecutionRunner_should_use_default_timeout_when_options_not_specified()
    {
        GuardianExecutionRunner.DefaultTimeout.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void ExecutionRunner_should_complete_within_timeout()
    {
        using var artifacts = new TestArtifactFactory();
        var currentRoot = artifacts.CreateDirectory("current");
        var patchRoot = artifacts.CreateDirectory("patch");
        File.WriteAllText(Path.Combine(currentRoot, "test.txt"), "content");
        File.WriteAllText(Path.Combine(patchRoot, "test.txt"), "content");

        var baselinePath = artifacts.WriteBaselineWorkbook("baseline.xlsx",
        [
            new BaselineRule("R001", "test.txt", null, GuardianFileType.Auto,
                false, CompareMode.Hash, false, false, 1, null),
        ]);

        var outputRoot = artifacts.CreateDirectory("output");
        var runner = CreateRunner();
        var request = new ComparisonExecutionRequest(currentRoot, patchRoot, baselinePath);

        // Should complete successfully with generous timeout
        var act = () => runner.ExecuteAndWriteReports(request, outputRoot, "Timeout Test",
            TimeSpan.FromMinutes(5));

        act.Should().NotThrow();
    }

    [Fact]
    public void ExecutionRunner_should_use_timeout_from_ComparisonOptions()
    {
        using var artifacts = new TestArtifactFactory();
        var currentRoot = artifacts.CreateDirectory("current");
        var patchRoot = artifacts.CreateDirectory("patch");
        File.WriteAllText(Path.Combine(currentRoot, "test.txt"), "content");
        File.WriteAllText(Path.Combine(patchRoot, "test.txt"), "content");

        var baselinePath = artifacts.WriteBaselineWorkbook("baseline.xlsx",
        [
            new BaselineRule("R001", "test.txt", null, GuardianFileType.Auto,
                false, CompareMode.Hash, false, false, 1, null),
        ]);

        var outputRoot = artifacts.CreateDirectory("output");
        var runner = CreateRunner();

        // Request with custom timeout in options
        var options = new ComparisonOptions(Timeout: TimeSpan.FromMinutes(5));
        var request = new ComparisonExecutionRequest(currentRoot, patchRoot, baselinePath, options);

        var act = () => runner.ExecuteAndWriteReports(request, outputRoot, "Options Timeout Test");

        act.Should().NotThrow();
    }

    // ── ComparisonOptions.EffectiveOptions ──

    [Fact]
    public void EffectiveOptions_should_return_default_when_options_null()
    {
        var request = new ComparisonExecutionRequest("/current", "/patch", "/baseline.xlsx");

        request.EffectiveOptions.Should().BeSameAs(ComparisonOptions.Default);
    }

    [Fact]
    public void EffectiveOptions_should_return_specified_options()
    {
        var options = new ComparisonOptions(Timeout: TimeSpan.FromSeconds(30), MaxConcurrency: 4);
        var request = new ComparisonExecutionRequest("/current", "/patch", "/baseline.xlsx", options);

        request.EffectiveOptions.Should().BeSameAs(options);
        request.EffectiveOptions.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        request.EffectiveOptions.MaxConcurrency.Should().Be(4);
    }

    // ── Helper Methods ──

    private static GuardianComparisonEngine CreateEngine()
        => new(
            new ClosedXmlBaselineReader(),
            new BaselineValidator(),
            new FileSystemInventoryScanner(new Sha256HashProvider()),
            new BaselineRuleResolver(),
            new GuardianFileComparer(new JarComparer(), new XmlComparer(), new YamlComparer(), new StatusEvaluator()));

    private static GuardianExecutionRunner CreateRunner()
        => new(
            CreateEngine(),
            new GuardianReportService(
                new ResultAggregator(),
                new StaticHtmlReportWriter(),
                new ClosedXmlExcelReportWriter()));
}
