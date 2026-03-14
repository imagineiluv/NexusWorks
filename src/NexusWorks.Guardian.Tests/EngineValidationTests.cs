using FluentAssertions;
using NexusWorks.Guardian.Baseline;
using NexusWorks.Guardian.Comparison;
using NexusWorks.Guardian.Evaluation;
using NexusWorks.Guardian.Infrastructure;
using NexusWorks.Guardian.Inventory;
using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.Orchestration;
using NexusWorks.Guardian.RuleResolution;
using NexusWorks.Guardian.Tests.TestSupport;

namespace NexusWorks.Guardian.Tests;

[Trait("Category", "Validation")]
public class EngineValidationTests
{
    // ── Input Path Validation ──

    [Fact]
    public void Engine_should_throw_when_current_root_does_not_exist()
    {
        using var artifacts = new TestArtifactFactory();
        var patchRoot = artifacts.CreateDirectory("patch");
        var baselinePath = artifacts.WriteBaselineWorkbook("baseline.xlsx",
        [
            new BaselineRule("R001", "test.txt", null, GuardianFileType.Auto,
                false, CompareMode.Hash, false, false, 1, null),
        ]);

        var engine = CreateEngine();
        var request = new ComparisonExecutionRequest("/nonexistent/current", patchRoot, baselinePath);

        var act = () => engine.Execute(request);

        act.Should().Throw<DirectoryNotFoundException>()
            .WithMessage("*CurrentRootPath*");
    }

    [Fact]
    public void Engine_should_throw_when_patch_root_does_not_exist()
    {
        using var artifacts = new TestArtifactFactory();
        var currentRoot = artifacts.CreateDirectory("current");
        var baselinePath = artifacts.WriteBaselineWorkbook("baseline.xlsx",
        [
            new BaselineRule("R001", "test.txt", null, GuardianFileType.Auto,
                false, CompareMode.Hash, false, false, 1, null),
        ]);

        var engine = CreateEngine();
        var request = new ComparisonExecutionRequest(currentRoot, "/nonexistent/patch", baselinePath);

        var act = () => engine.Execute(request);

        act.Should().Throw<DirectoryNotFoundException>()
            .WithMessage("*PatchRootPath*");
    }

    [Fact]
    public void Engine_should_throw_when_baseline_file_does_not_exist()
    {
        using var artifacts = new TestArtifactFactory();
        var currentRoot = artifacts.CreateDirectory("current");
        var patchRoot = artifacts.CreateDirectory("patch");

        var engine = CreateEngine();
        var request = new ComparisonExecutionRequest(currentRoot, patchRoot, "/nonexistent/baseline.xlsx");

        var act = () => engine.Execute(request);

        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*BaselinePath*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Engine_should_throw_for_null_or_empty_current_root(string? path)
    {
        var engine = CreateEngine();
        var request = new ComparisonExecutionRequest(path!, "/patch", "/baseline.xlsx");

        var act = () => engine.Execute(request);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Engine_should_throw_for_null_or_empty_patch_root(string? path)
    {
        using var artifacts = new TestArtifactFactory();
        var currentRoot = artifacts.CreateDirectory("current");

        var engine = CreateEngine();
        var request = new ComparisonExecutionRequest(currentRoot, path!, "/baseline.xlsx");

        var act = () => engine.Execute(request);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Engine_should_throw_for_null_or_empty_baseline_path(string? path)
    {
        using var artifacts = new TestArtifactFactory();
        var currentRoot = artifacts.CreateDirectory("current");
        var patchRoot = artifacts.CreateDirectory("patch");

        var engine = CreateEngine();
        var request = new ComparisonExecutionRequest(currentRoot, patchRoot, path!);

        var act = () => engine.Execute(request);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Engine_should_throw_for_null_request()
    {
        var engine = CreateEngine();

        var act = () => engine.Execute(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Engine with Logger Integration ──

    [Fact]
    public void Engine_should_log_stage_events_via_logger()
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

        var logger = new BufferedGuardianLogger();
        var engine = CreateEngine(logger);
        var request = new ComparisonExecutionRequest(currentRoot, patchRoot, baselinePath);

        engine.Execute(request);

        // Should have logged stage start/end for: Baseline Load, Current Inventory Scan, Patch Inventory Scan, Candidate Compare
        logger.Entries.Should().Contain(e => e.Message.Contains("Baseline Load"));
        logger.Entries.Should().Contain(e => e.Message.Contains("Current Inventory Scan"));
        logger.Entries.Should().Contain(e => e.Message.Contains("Patch Inventory Scan"));
        logger.Entries.Should().Contain(e => e.Message.Contains("Candidate Compare"));
    }

    [Fact]
    public void Engine_should_use_MaxConcurrency_from_options()
    {
        using var artifacts = new TestArtifactFactory();
        var currentRoot = artifacts.CreateDirectory("current");
        var patchRoot = artifacts.CreateDirectory("patch");

        for (var i = 0; i < 10; i++)
        {
            File.WriteAllText(Path.Combine(currentRoot, $"file{i}.txt"), $"content-{i}");
            File.WriteAllText(Path.Combine(patchRoot, $"file{i}.txt"), $"content-{i}-modified");
        }

        var baselinePath = artifacts.WriteBaselineWorkbook("baseline.xlsx",
            Enumerable.Range(0, 10).Select(i =>
                new BaselineRule($"R{i:D3}", $"file{i}.txt", null, GuardianFileType.Auto,
                    false, CompareMode.Hash, false, false, i, null)).ToArray());

        var options = new ComparisonOptions(MaxConcurrency: 1);
        var engine = CreateEngine();
        var request = new ComparisonExecutionRequest(currentRoot, patchRoot, baselinePath, options);

        // Should complete successfully even with MaxConcurrency = 1
        var result = engine.Execute(request);

        result.Items.Should().HaveCount(10);
    }

    // ── Helper ──

    private static GuardianComparisonEngine CreateEngine(IGuardianLogger? logger = null)
        => logger is not null
            ? new GuardianComparisonEngine(
                new ClosedXmlBaselineReader(),
                new BaselineValidator(),
                new FileSystemInventoryScanner(new Sha256HashProvider()),
                new BaselineRuleResolver(),
                new GuardianFileComparer(new JarComparer(), new XmlComparer(), new YamlComparer(), new StatusEvaluator()),
                logger)
            : new GuardianComparisonEngine(
                new ClosedXmlBaselineReader(),
                new BaselineValidator(),
                new FileSystemInventoryScanner(new Sha256HashProvider()),
                new BaselineRuleResolver(),
                new GuardianFileComparer(new JarComparer(), new XmlComparer(), new YamlComparer(), new StatusEvaluator()));
}
