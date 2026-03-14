using FluentAssertions;
using NexusWorks.Guardian.Evaluation;
using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.Tests;

[Trait("Category", "Evaluation")]
public class StatusEvaluatorTests
{
    [Fact]
    public void Evaluate_should_return_missing_required_for_required_missing_file()
    {
        var evaluator = new StatusEvaluator();

        var result = evaluator.Evaluate(new StatusEvaluationContext(
            Required: true,
            CurrentExists: true,
            PatchExists: false,
            IsEquivalent: false,
            HasDifferences: true,
            FileType: GuardianFileType.Auto));

        result.Status.Should().Be(CompareStatus.MissingRequired);
        result.Severity.Should().Be(Severity.Critical);
    }

    [Fact]
    public void Evaluate_should_return_high_severity_for_changed_structured_file()
    {
        var evaluator = new StatusEvaluator();

        var result = evaluator.Evaluate(new StatusEvaluationContext(
            Required: false,
            CurrentExists: true,
            PatchExists: true,
            IsEquivalent: false,
            HasDifferences: true,
            FileType: GuardianFileType.Xml));

        result.Status.Should().Be(CompareStatus.Changed);
        result.Severity.Should().Be(Severity.High);
    }

    [Fact]
    public void Evaluate_should_return_low_severity_for_added_optional_file()
    {
        var evaluator = new StatusEvaluator();

        var result = evaluator.Evaluate(new StatusEvaluationContext(
            Required: false,
            CurrentExists: false,
            PatchExists: true,
            IsEquivalent: false,
            HasDifferences: true,
            FileType: GuardianFileType.Auto));

        result.Status.Should().Be(CompareStatus.Added);
        result.Severity.Should().Be(Severity.Low);
    }
}
