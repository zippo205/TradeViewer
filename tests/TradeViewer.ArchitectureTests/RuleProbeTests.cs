namespace TradeViewer.ArchitectureTests;

/// <summary>The rules must fail on a real violation, otherwise they would pass by accident (see Probe/).</summary>
public class RuleProbeTests
{
    [Fact]
    public void The_layer_rule_detects_a_dependency_on_another_assembly()
    {
        var result = ArchitectureRules.NoDependency(
            typeof(RuleProbeTests).Assembly,
            "TradeViewer.ArchitectureTests.Probe.Layers",
            "TradeViewer.Application");

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public void The_feature_rule_detects_a_dependency_between_features()
    {
        var violations = ArchitectureRules.FeatureViolations(
            typeof(RuleProbeTests).Assembly,
            "TradeViewer.ArchitectureTests.Probe.Features");

        Assert.Single(violations);
        Assert.Contains("ProbeFeatureB", violations[0]);
    }

    [Fact]
    public void The_feature_rule_finds_the_probe_features()
    {
        var names = ArchitectureRules.FeatureNames(
            typeof(RuleProbeTests).Assembly,
            "TradeViewer.ArchitectureTests.Probe.Features");

        Assert.Equal(new[] { "A", "B" }, names);
    }
}
