using System.Text.RegularExpressions;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>NF6: no request leaves the home network. The page, its styles and its scripts must all come from the application itself.</summary>
public class OutsideRequestsTests : IClassFixture<TradeViewerFactory>
{
    private static readonly Regex Attribute = new Regex("(?:src|href)\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
    private static readonly Regex CssReference = new Regex("(?:url\\(\\s*['\"]?|@import\\s+['\"])([^'\")\\s]+)", RegexOptions.IgnoreCase);

    private readonly TradeViewerFactory _factory;

    public OutsideRequestsTests(TradeViewerFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task The_page_references_only_files_of_the_application()
    {
        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/");

        var references = Attribute.Matches(html).Select(match => match.Groups[1].Value).ToList();

        Assert.NotEmpty(references);
        Assert.All(references, reference => Assert.True(IsLocal(reference), "Outside reference in index.html: " + reference));
    }

    [Fact]
    public async Task The_stylesheets_reference_only_files_of_the_application()
    {
        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/");
        var stylesheets = Attribute.Matches(html)
            .Select(match => match.Groups[1].Value)
            .Where(reference => reference.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(stylesheets);
        foreach (var stylesheet in stylesheets)
        {
            var css = await client.GetStringAsync("/" + stylesheet.TrimStart('/'));
            foreach (Match match in CssReference.Matches(css))
            {
                Assert.True(IsLocal(match.Groups[1].Value), "Outside reference in " + stylesheet + ": " + match.Groups[1].Value);
            }
        }
    }

    private static bool IsLocal(string reference)
    {
        return !reference.StartsWith("//", StringComparison.Ordinal)
            && !Regex.IsMatch(reference, "^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase)
            || reference.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
    }
}
