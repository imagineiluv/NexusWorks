using FluentAssertions;
using NexusWorks.Guardian.Infrastructure;
using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.Tests;

[Trait("Category", "Configuration")]
public class ComparisonOptionsTests
{
    // ── ComparisonOptions Defaults ──

    [Fact]
    public void Default_options_should_have_null_timeout()
    {
        ComparisonOptions.Default.Timeout.Should().BeNull();
    }

    [Fact]
    public void Default_options_should_have_null_max_concurrency()
    {
        ComparisonOptions.Default.MaxConcurrency.Should().BeNull();
    }

    [Fact]
    public void Default_options_should_validate_file_type_combinations()
    {
        ComparisonOptions.Default.ValidateFileTypeCombinations.Should().BeTrue();
    }

    [Fact]
    public void Custom_options_should_preserve_values()
    {
        var options = new ComparisonOptions(
            Timeout: TimeSpan.FromSeconds(45),
            MaxConcurrency: 4,
            ValidateFileTypeCombinations: false);

        options.Timeout.Should().Be(TimeSpan.FromSeconds(45));
        options.MaxConcurrency.Should().Be(4);
        options.ValidateFileTypeCombinations.Should().BeFalse();
    }

    // ── PerformanceTuning Worker Count ──

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public void GetWorkerCount_should_return_one_for_zero_or_one_items(int itemCount, int expected)
    {
        GuardianPerformanceTuning.GetWorkerCount(itemCount).Should().Be(expected);
    }

    [Fact]
    public void GetWorkerCount_should_return_at_least_two_for_multiple_items()
    {
        var result = GuardianPerformanceTuning.GetWorkerCount(10);

        result.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void GetWorkerCount_should_not_exceed_eight()
    {
        var result = GuardianPerformanceTuning.GetWorkerCount(1000);

        result.Should().BeLessThanOrEqualTo(8);
    }

    [Fact]
    public void GetWorkerCount_should_not_exceed_item_count()
    {
        var result = GuardianPerformanceTuning.GetWorkerCount(3);

        result.Should().BeLessThanOrEqualTo(3);
    }

    // ── MaxConcurrency Override ──

    [Fact]
    public void GetWorkerCount_with_override_should_cap_at_override_value()
    {
        var result = GuardianPerformanceTuning.GetWorkerCount(100, maxConcurrencyOverride: 2);

        result.Should().Be(2);
    }

    [Fact]
    public void GetWorkerCount_with_null_override_should_use_default()
    {
        var withoutOverride = GuardianPerformanceTuning.GetWorkerCount(100);
        var withNullOverride = GuardianPerformanceTuning.GetWorkerCount(100, maxConcurrencyOverride: null);

        withNullOverride.Should().Be(withoutOverride);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void GetWorkerCount_with_zero_or_negative_override_should_use_default(int overrideValue)
    {
        var withDefault = GuardianPerformanceTuning.GetWorkerCount(100);
        var withOverride = GuardianPerformanceTuning.GetWorkerCount(100, maxConcurrencyOverride: overrideValue);

        withOverride.Should().Be(withDefault);
    }

    [Fact]
    public void GetWorkerCount_with_large_override_should_still_respect_base_count()
    {
        // Override of 100 should not increase beyond the base calculation
        var baseCount = GuardianPerformanceTuning.GetWorkerCount(5);
        var withLargeOverride = GuardianPerformanceTuning.GetWorkerCount(5, maxConcurrencyOverride: 100);

        withLargeOverride.Should().Be(baseCount);
    }

    // ── ComparisonExecutionRequest ──

    [Fact]
    public void Request_EffectiveOptions_should_return_default_when_null()
    {
        var request = new ComparisonExecutionRequest("/c", "/p", "/b");

        request.EffectiveOptions.Should().BeSameAs(ComparisonOptions.Default);
        request.EffectiveOptions.Timeout.Should().BeNull();
        request.EffectiveOptions.MaxConcurrency.Should().BeNull();
    }

    [Fact]
    public void Request_EffectiveOptions_should_return_provided_options()
    {
        var options = new ComparisonOptions(MaxConcurrency: 3);
        var request = new ComparisonExecutionRequest("/c", "/p", "/b", options);

        request.EffectiveOptions.Should().BeSameAs(options);
        request.EffectiveOptions.MaxConcurrency.Should().Be(3);
    }
}
