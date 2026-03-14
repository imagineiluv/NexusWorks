using FluentAssertions;
using NexusWorks.Guardian.Baseline;
using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.Tests.TestSupport;

namespace NexusWorks.Guardian.Tests;

[Trait("Category", "Baseline")]
public class BaselineReaderTests
{
    [Fact]
    public void ClosedXmlBaselineReader_should_load_rules_from_rules_sheet()
    {
        using var artifacts = new TestArtifactFactory();
        var baselinePath = artifacts.WriteBaselineWorkbook(
            "baseline.xlsx",
            [
                new BaselineRule("R001", "conf/app.xml", null, GuardianFileType.Xml, true, CompareMode.Hash | CompareMode.XmlStructure, false, false, 1, "core xml"),
                new BaselineRule("R002", null, "config/*.yaml", GuardianFileType.Yaml, false, CompareMode.Hash | CompareMode.YamlStructure, true, false, 2, null),
            ]);

        var reader = new ClosedXmlBaselineReader();
        var rules = reader.Read(baselinePath);

        rules.Should().HaveCount(2);
        rules[0].RelativePath.Should().Be("conf/app.xml");
        rules[1].Pattern.Should().Be("config/*.yaml");
        rules[1].FileType.Should().Be(GuardianFileType.Yaml);
        rules[1].CompareMode.Should().Be(CompareMode.Hash | CompareMode.YamlStructure);
    }

    [Fact]
    public void BaselineValidator_should_reject_rule_without_path_or_pattern()
    {
        var validator = new BaselineValidator();
        var rules = new[]
        {
            new BaselineRule("R001", null, null, GuardianFileType.Xml, true, CompareMode.Hash, false, false, 1, null),
        };

        var act = () => validator.Validate(rules);

        act.Should().Throw<BaselineValidationException>()
            .WithMessage("*relative_path or pattern*");
    }
}
