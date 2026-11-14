using System.Reflection;

namespace TradeViewer.ArchitectureTests;

/// <summary>Architecture chapter 3, Dependency Rules: everything not listed is forbidden.</summary>
public class DependencyRulesTests
{
    private static readonly string[] All =
    {
        "TradeViewer.Domain", "TradeViewer.Contracts", "TradeViewer.Application",
        "TradeViewer.Infrastructure", "TradeViewer.Api", "TradeViewer.Web"
    };

    public static IEnumerable<object[]> Layers()
    {
        yield return new object[] { "TradeViewer.Domain", Array.Empty<string>() };
        yield return new object[] { "TradeViewer.Contracts", Array.Empty<string>() };
        yield return new object[] { "TradeViewer.Application", new[] { "TradeViewer.Domain", "TradeViewer.Contracts" } };
        yield return new object[] { "TradeViewer.Infrastructure", new[] { "TradeViewer.Application", "TradeViewer.Domain", "TradeViewer.Contracts" } };
        yield return new object[] { "TradeViewer.Web", new[] { "TradeViewer.Contracts" } };
        yield return new object[] { "TradeViewer.Api", new[] { "TradeViewer.Application", "TradeViewer.Infrastructure", "TradeViewer.Contracts" } };
    }

    [Theory]
    [MemberData(nameof(Layers))]
    public void A_layer_depends_only_on_the_layers_it_may_use(string layer, string[] allowed)
    {
        var assembly = Assembly.Load(layer);
        var forbidden = All.Where(name => name != layer && !allowed.Contains(name)).ToArray();

        var result = ArchitectureRules.NoDependency(assembly, layer, forbidden);

        Assert.True(result.IsSuccessful, Describe(layer, result.FailingTypeNames));
    }

    [Fact]
    public void The_application_layer_has_types_the_rules_can_check()
    {
        // The rules above would pass on an empty assembly; this keeps them honest while Application has content.
        Assert.NotEmpty(Assembly.Load("TradeViewer.Application").GetTypes());
    }

    private static string Describe(string layer, IEnumerable<string>? failing)
    {
        return layer + " depends on a layer it must not use: " + string.Join(", ", failing ?? Array.Empty<string>());
    }
}
