using TradeViewer.Api.Endpoints;
using TradeViewer.Api.Http;
using TradeViewer.Api.Realtime;
using TradeViewer.Application;
using TradeViewer.Application.Abstractions.Realtime;
using TradeViewer.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddErrorHandling();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IWorkspaceNotifier, HubWorkspaceNotifier>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);

var app = builder.Build();

app.UseErrorHandling();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapHealthEndpoints();
app.MapWorkspaceEndpoints();
app.MapAssetEndpoints();
app.MapCandleEndpoints();
app.MapWatchlistEndpoints();
app.MapDrawingEndpoints();
app.MapChartSettingsEndpoints();
app.MapHub<WorkspaceHub>(WorkspaceHub.Route);

// An unknown API route is a 404 with problem details, never the page of the client.
app.Map("/api/{**rest}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Lets the integration tests start the application.</summary>
public partial class Program
{
}
