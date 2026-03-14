using FluentAssertions;
using NexusWorks.Guardian.Baseline;
using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.Tests.TestSupport;

namespace NexusWorks.Guardian.Tests;

[Trait("Category", "Validation")]
public class BaselineValidationTests
{
    private readonly BaselineValidator _validator = new();

    // ── file_type + compare_mode Compatibility ──

    [Fact]
    public void Validator_should_reject_JarEntry_with_non_Jar_file_type()
    {
        var rules = new[]
        {
            new BaselineRule("R001", "conf/app.xml", null, GuardianFileType.Xml,
                false, CompareMode.JarEntry, false, false, 1, null),
        };

        var act = () => _validator.Validate(rules);

        act.Should().Throw<BaselineValidationException>()
            .WithMessage("*JarEntry*requires*Jar*");
    }

    [Fact]
    public void Validator_should_reject_XmlStructure_with_non_Xml_file_type()
    {
        var rules = new[]
        {
            new BaselineRule("R001", "lib/app.jar", null, GuardianFileType.Jar,
                false, CompareMode.XmlStructure, false, false, 1, null),
        };

        var act = () => _validator.Validate(rules);

        act.Should().Throw<BaselineValidationException>()
            .WithMessage("*XmlStructure*requires*Xml*");
    }

    [Fact]
    public void Validator_should_reject_YamlStructure_with_non_Yaml_file_type()
    {
        var rules = new[]
        {
            new BaselineRule("R001", "conf/app.xml", null, GuardianFileType.Xml,
                false, CompareMode.YamlStructure, false, false, 1, null),
        };

        var act = () => _validator.Validate(rules);

        act.Should().Throw<BaselineValidationException>()
            .WithMessage("*YamlStructure*requires*Yaml*");
    }

    [Fact]
    public void Validator_should_reject_incompatible_structural_mode_combination()
    {
        // XmlStructure | JarEntry with Xml file type: JarEntry check fails first
        var rules = new[]
        {
            new BaselineRule("R001", "conf/app.xml", null, GuardianFileType.Xml,
                false, CompareMode.XmlStructure | CompareMode.JarEntry, false, false, 1, null),
        };

        var act = () => _validator.Validate(rules);

        act.Should().Throw<BaselineValidationException>()
            .WithMessage("*JarEntry*requires*Jar*");
    }

    [Theory]
    [InlineData(GuardianFileType.Xml, CompareMode.Hash | CompareMode.XmlStructure)]
    [InlineData(GuardianFileType.Jar, CompareMode.Hash | CompareMode.JarEntry)]
    [InlineData(GuardianFileType.Yaml, CompareMode.Hash | CompareMode.YamlStructure)]
    public void Validator_should_accept_valid_file_type_and_compare_mode_combinations(
        GuardianFileType fileType, CompareMode compareMode)
    {
        var rules = new[]
        {
            new BaselineRule("R001", "some/path", null, fileType,
                false, compareMode, false, false, 1, null),
        };

        var act = () => _validator.Validate(rules);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validator_should_allow_Auto_file_type_with_any_compare_mode()
    {
        var rules = new[]
        {
            new BaselineRule("R001", "some/path", null, GuardianFileType.Auto,
                false, CompareMode.JarEntry | CompareMode.XmlStructure, false, false, 1, null),
        };

        var act = () => _validator.Validate(rules);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validator_should_allow_Hash_only_with_any_file_type()
    {
        var rules = new[]
        {
            new BaselineRule("R001", "some/path", null, GuardianFileType.Jar,
                false, CompareMode.Hash, false, false, 1, null),
        };

        var act = () => _validator.Validate(rules);

        act.Should().NotThrow();
    }

    // ── Duplicate Rule Detection ──

    [Fact]
    public void Validator_should_reject_duplicate_rule_ids()
    {
        var rules = new[]
        {
            new BaselineRule("R001", "path/a.xml", null, GuardianFileType.Xml,
                false, CompareMode.Hash, false, false, 1, null),
            new BaselineRule("R001", "path/b.xml", null, GuardianFileType.Xml,
                false, CompareMode.Hash, false, false, 2, null),
        };

        var act = () => _validator.Validate(rules);

        act.Should().Throw<BaselineValidationException>()
            .WithMessage("*Duplicate rule_id*R001*");
    }

    [Fact]
    public void Validator_should_reject_duplicate_rule_ids_case_insensitive()
    {
        var rules = new[]
        {
            new BaselineRule("r001", "path/a.xml", null, GuardianFileType.Xml,
                false, CompareMode.Hash, false, false, 1, null),
            new BaselineRule("R001", "path/b.xml", null, GuardianFileType.Xml,
                false, CompareMode.Hash, false, false, 2, null),
        };

        var act = () => _validator.Validate(rules);

        act.Should().Throw<BaselineValidationException>()
            .WithMessage("*Duplicate rule_id*");
    }

    // ── Empty/Null Rule Fields ──

    [Fact]
    public void Validator_should_reject_empty_rule_id()
    {
        var rules = new[]
        {
            new BaselineRule("", "path/a.xml", null, GuardianFileType.Xml,
                false, CompareMode.Hash, false, false, 1, null),
        };

        var act = () => _validator.Validate(rules);

        act.Should().Throw<BaselineValidationException>()
            .WithMessage("*rule_id must not be empty*");
    }

    [Fact]
    public void Validator_should_reject_rule_without_path_or_pattern()
    {
        var rules = new[]
        {
            new BaselineRule("R001", null, null, GuardianFileType.Xml,
                false, CompareMode.Hash, false, false, 1, null),
        };

        var act = () => _validator.Validate(rules);

        act.Should().Throw<BaselineValidationException>()
            .WithMessage("*relative_path or pattern*");
    }

    [Fact]
    public void Validator_should_accept_rule_with_pattern_only()
    {
        var rules = new[]
        {
            new BaselineRule("R001", null, "lib/*.jar", GuardianFileType.Jar,
                false, CompareMode.Hash, false, false, 1, null),
        };

        var act = () => _validator.Validate(rules);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validator_should_accept_rule_with_relative_path_only()
    {
        var rules = new[]
        {
            new BaselineRule("R001", "conf/app.xml", null, GuardianFileType.Xml,
                false, CompareMode.Hash, false, false, 1, null),
        };

        var act = () => _validator.Validate(rules);

        act.Should().NotThrow();
    }

    // ── Baseline Reader Parse Edge Cases ──

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BaselineReader_should_round_trip_boolean_values(bool expected)
    {
        using var artifacts = new TestArtifactFactory();
        var rules = new[]
        {
            new BaselineRule("R001", "conf/app.xml", null, GuardianFileType.Xml,
                expected, CompareMode.Hash, false, false, 1, null),
        };

        var baselinePath = artifacts.WriteBaselineWorkbook("baseline.xlsx", rules);
        var reader = new ClosedXmlBaselineReader();
        var result = reader.Read(baselinePath);

        result.Should().ContainSingle();
        result[0].Required.Should().Be(expected);
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("xml-structure")]
    [InlineData("xml_structure")]
    [InlineData("yaml-structure")]
    [InlineData("yaml_structure")]
    [InlineData("jar-entry")]
    [InlineData("jar-entries")]
    [InlineData("jar_entry")]
    [InlineData("auto")]
    public void BaselineReader_should_parse_compare_mode_variants(string modeText)
    {
        using var artifacts = new TestArtifactFactory();

        // Determine which file type to use based on the mode text
        var fileType = modeText switch
        {
            "xml-structure" or "xml_structure" => GuardianFileType.Xml,
            "yaml-structure" or "yaml_structure" => GuardianFileType.Yaml,
            "jar-entry" or "jar-entries" or "jar_entry" => GuardianFileType.Jar,
            _ => GuardianFileType.Auto,
        };

        var expectedMode = modeText switch
        {
            "hash" => CompareMode.Hash,
            "xml-structure" or "xml_structure" => CompareMode.XmlStructure,
            "yaml-structure" or "yaml_structure" => CompareMode.YamlStructure,
            "jar-entry" or "jar-entries" or "jar_entry" => CompareMode.JarEntry,
            "auto" => CompareMode.None,
            _ => CompareMode.None,
        };

        var rules = new[]
        {
            new BaselineRule("R001", "some/path", null, fileType,
                false, expectedMode, false, false, 1, null),
        };

        var baselinePath = artifacts.WriteBaselineWorkbook("baseline.xlsx", rules);
        var reader = new ClosedXmlBaselineReader();
        var result = reader.Read(baselinePath);

        result.Should().ContainSingle();
        result[0].CompareMode.Should().Be(expectedMode);
    }

    [Theory]
    [InlineData(GuardianFileType.Auto)]
    [InlineData(GuardianFileType.Jar)]
    [InlineData(GuardianFileType.Xml)]
    [InlineData(GuardianFileType.Yaml)]
    public void BaselineReader_should_round_trip_file_type_values(GuardianFileType expected)
    {
        using var artifacts = new TestArtifactFactory();

        var compareMode = expected switch
        {
            GuardianFileType.Jar => CompareMode.JarEntry,
            GuardianFileType.Xml => CompareMode.XmlStructure,
            GuardianFileType.Yaml => CompareMode.YamlStructure,
            _ => CompareMode.Hash,
        };

        var rules = new[]
        {
            new BaselineRule("R001", "some/path", null, expected,
                false, compareMode, false, false, 1, null),
        };

        var baselinePath = artifacts.WriteBaselineWorkbook("baseline.xlsx", rules);
        var reader = new ClosedXmlBaselineReader();
        var result = reader.Read(baselinePath);

        result.Should().ContainSingle();
        result[0].FileType.Should().Be(expected);
    }

    [Fact]
    public void Validator_should_accept_empty_rule_list()
    {
        var act = () => _validator.Validate(Array.Empty<BaselineRule>());

        act.Should().NotThrow();
    }

    [Fact]
    public void Validator_should_throw_for_null_rules()
    {
        var act = () => _validator.Validate(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
