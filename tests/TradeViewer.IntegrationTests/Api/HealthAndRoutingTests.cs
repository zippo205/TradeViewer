using System.Net;
using System.Text.Json;

namespace TradeViewer.IntegrationTests.Api;

public class HealthAndRoutingTests : IClassFixture<TradeViewerFactory>
{
    private readonly TradeViewerFactory _factory;

    public HealthAndRoutingTests(TradeViewerFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_answers_ok()
    {
        var response = await _factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_start_created_the_data_path()
    {
        _ = _factory.CreateClient();

        Assert.True(Directory.Exists(_factory.DataPath));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/some/page/of/the/client")]
    public async Task The_client_application_is_served_for_every_page(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("_framework/blazor.webassembly.js", html);
    }

    [Fact]
    public async Task The_framework_files_of_the_client_are_served()
    {
        var response = await _factory.CreateClient().GetAsync("/_framework/blazor.webassembly.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_chart_module_is_served_from_the_client_files()
    {
        var response = await _factory.CreateClient().GetAsync("/chart/index.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("javascript", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api/unknown")]
    [InlineData("/api/assets/binance/BTCUSDT/nothing")]
    public async Task An_unknown_api_route_is_a_404_with_problem_details(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
