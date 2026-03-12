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

public class SampleDatasetTests
{
    [Fact]
    public void Sample_guardian_dataset_should_produce_expected_statuses()
    {
        var repositoryRoot = RepositoryRootLocator.Find();
        var sampleRoot = Path.Combine(repositoryRoot, "sample", "guardian");
        var currentRoot = Path.Combine(sampleRoot, "current");
        var patchRoot = Path.Combine(sampleRoot, "patch");
        var baselinePath = Path.Combine(sampleRoot, "baseline.xlsx");

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
        byPath["conf/layout.xml"].Status.Should().Be(CompareStatus.Ok);
        byPath["conf/layout.xml"].Summary.Should().Contain("normalized XML is equivalent");
        byPath["conf/settings.yaml"].Status.Should().Be(CompareStatus.Changed);
        byPath["conf/feature-flags.yaml"].Status.Should().Be(CompareStatus.Ok);
        byPath["conf/feature-flags.yaml"].Summary.Should().Contain("normalized YAML is equivalent");
        byPath["lib/core-guardian.jar"].Status.Should().Be(CompareStatus.Changed);
        byPath["notes/release.txt"].Status.Should().Be(CompareStatus.Changed);
        byPath["conf/required.xml"].Status.Should().Be(CompareStatus.MissingRequired);
        byPath["only-current.txt"].Status.Should().Be(CompareStatus.Removed);
        byPath["only-patch.txt"].Status.Should().Be(CompareStatus.Added);
    }
}
