using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradeViewer.Api.Http;
using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.IntegrationTests.Api;

public class ErrorHandlingTests
{
    [Fact]
    public async Task An_unexpected_exception_is_a_500_without_internal_details()
    {
        using var host = await StartAsync(app => app.Map("/boom", branch => branch.Run(_ => throw new InvalidOperationException("secret detail"))));

        var response = await host.GetTestClient().GetAsync("/boom");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("secret detail", body);
    }

    [Theory]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.InvalidData, 422)]
    [InlineData(ErrorType.Unavailable, 503)]
    public async Task An_error_of_a_handler_gets_its_status_and_code(ErrorType type, int status)
    {
        var error = new Error(type, "thing.problem", "Something is wrong.");
        using var host = await StartAsync(app => app.Run(context => ResultHttpExtensions.ToProblem(error).ExecuteAsync(context)));

        var response = await host.GetTestClient().GetAsync("/");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("thing.problem", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("Something is wrong.", json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_validation_error_is_a_400_with_the_fields()
    {
        var fields = new Dictionary<string, string[]> { ["symbol"] = new[] { "Required." } };
        var error = Error.Validation("asset.invalid", "Invalid asset.", fields);
        using var host = await StartAsync(app => app.Run(context => ResultHttpExtensions.ToProblem(error).ExecuteAsync(context)));

        var response = await host.GetTestClient().GetAsync("/");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Required.", json.RootElement.GetProperty("errors").GetProperty("symbol")[0].GetString());
        Assert.Equal("asset.invalid", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Results_map_to_ok_and_no_content()
    {
        using var host = await StartAsync(app => app.Run(context =>
        {
            IResult result = context.Request.Path == "/value"
                ? Result.Success(7).ToHttpResult()
                : Result.Success().ToNoContentResult();
            return result.ExecuteAsync(context);
        }));
        var client = host.GetTestClient();

        var value = await client.GetAsync("/value");
        var none = await client.GetAsync("/none");

        Assert.Equal(HttpStatusCode.OK, value.StatusCode);
        Assert.Equal("7", await value.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, none.StatusCode);
    }

    private static async Task<IHost> StartAsync(Action<IApplicationBuilder> pipeline)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddLogging(logging => logging.ClearProviders());
                    services.AddErrorHandling();
                })
                .Configure(app =>
                {
                    app.UseErrorHandling();
                    pipeline(app);
                }))
            .Build();

        await host.StartAsync();
        return host;
    }
}
