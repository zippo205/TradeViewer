using System.Xml.Linq;

namespace TradeViewer.ArchitectureTests;

/// <summary>The project references and packages of the src projects, which the type rules cannot see while an assembly is empty.</summary>
public class ProjectReferenceTests
{
    public static IEnumerable<object[]> Projects()
    {
        yield return new object[] { "TradeViewer.Domain", Array.Empty<string>() };
        yield return new object[] { "TradeViewer.Contracts", Array.Empty<string>() };
        yield return new object[] { "TradeViewer.Application", new[] { "TradeViewer.Domain", "TradeViewer.Contracts" } };
        yield return new object[] { "TradeViewer.Infrastructure", new[] { "TradeViewer.Application", "TradeViewer.Domain", "TradeViewer.Contracts" } };
        yield return new object[] { "TradeViewer.Web", new[] { "TradeViewer.Contracts" } };
        yield return new object[] { "TradeViewer.Api", new[] { "TradeViewer.Application", "TradeViewer.Infrastructure", "TradeViewer.Contracts", "TradeViewer.Web" } };
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void A_project_references_only_the_projects_it_may_use(string project, string[] allowed)
    {
        var references = Elements(project, "ProjectReference")
            .Select(path => Path.GetFileNameWithoutExtension(path.Replace('\\', '/')))
            .ToArray();

        Assert.True(
            references.All(allowed.Contains),
            project + " references " + string.Join(", ", references.Except(allowed)) + ", which the dependency rules forbid.");
    }

    [Theory]
    [InlineData("TradeViewer.Domain")]
    [InlineData("TradeViewer.Contracts")]
    public void Domain_and_Contracts_have_no_package_references(string project)
    {
        Assert.Empty(Elements(project, "PackageReference"));
    }

    private static IEnumerable<string> Elements(string project, string name)
    {
        var document = XDocument.Load(RepositoryPaths.Project(project));

        return document.Descendants(name).Select(element => (string?)element.Attribute("Include") ?? string.Empty);
    }
}
