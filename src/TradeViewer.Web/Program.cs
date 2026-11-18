using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TradeViewer.Web;
using TradeViewer.Web.Api;
using TradeViewer.Web.Drawings;
using TradeViewer.Web.Realtime;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The browser talks to TradeViewer.Api only, which served this application (architecture, chapter 2).
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<WorkspaceApi>();
builder.Services.AddScoped<AssetsApi>();
builder.Services.AddScoped<WatchlistsApi>();
builder.Services.AddScoped<DownloaderApi>();
builder.Services.AddScoped<WorkspaceEvents>();

// Drawings: one window id, one save queue and one session per window, so saves and undo survive moving between pages.
builder.Services.AddScoped<WindowIdentity>();
builder.Services.AddScoped<DrawingsApi>();
builder.Services.AddScoped<IndicatorsApi>();
builder.Services.AddScoped<DrawingSaveQueue>(services => new DrawingSaveQueue(services.GetRequiredService<DrawingsApi>()));
builder.Services.AddScoped<DrawingSession>();

await builder.Build().RunAsync();
