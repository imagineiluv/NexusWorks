using FluentAssertions;
using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.RuleResolution;

namespace NexusWorks.Guardian.Tests;

public class RuleResolverTests
{
    [Fact]
    public void Resolver_should_prefer_exact_rule_over_pattern_and_defaults()
    {
        var resolver = new BaselineRuleResolver();
        var rules = new[]
        {
            new BaselineRule("R100", null, "conf/*.xml", GuardianFileType.Xml, false, CompareMode.Hash | CompareMode.XmlStructure, false, false, 2, null),
            new BaselineRule("R010", "conf/app.xml", null, GuardianFileType.Xml, true, CompareMode.Hash | CompareMode.XmlStructure, false, false, 1, null),
        };

        var resolved = resolver.Resolve("conf/app.xml", rules);

        resolved.RuleId.Should().Be("R010");
        resolved.Required.Should().BeTrue();
        resolved.Source.Should().Be("exact");
    }

    [Fact]
    public void Resolver_should_fall_back_to_extension_defaults()
    {
        var resolver = new BaselineRuleResolver();

        var resolved = resolver.Resolve("lib/core/app.jar", Array.Empty<BaselineRule>());

        resolved.RuleId.Should().Be("AUTO");
        resolved.FileType.Should().Be(GuardianFileType.Jar);
        resolved.CompareMode.Should().Be(CompareMode.Hash | CompareMode.JarEntry);
    }

    [Fact]
    public void Resolver_should_assign_yaml_compare_mode_for_yaml_extensions()
    {
        var resolver = new BaselineRuleResolver();

        var resolved = resolver.Resolve("config/services/app.yaml", Array.Empty<BaselineRule>());

        resolved.RuleId.Should().Be("AUTO");
        resolved.FileType.Should().Be(GuardianFileType.Yaml);
        resolved.CompareMode.Should().Be(CompareMode.Hash | CompareMode.YamlStructure);
    }
}
