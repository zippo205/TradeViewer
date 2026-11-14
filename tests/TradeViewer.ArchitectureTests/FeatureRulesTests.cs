using System.Reflection;

namespace TradeViewer.ArchitectureTests;

/// <summary>Architecture chapter 4: a feature never uses the types of another feature; what they share is a port in Abstractions.</summary>
public class FeatureRulesTests
{
    private const string Features = "TradeViewer.Application.Features";

    [Fact]
    public void No_feature_depends_on_another_feature()
    {
        var violations = ArchitectureRules.FeatureViolations(Assembly.Load("TradeViewer.Application"), Features);

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }
}
