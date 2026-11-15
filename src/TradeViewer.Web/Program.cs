using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TradeViewer.Web;
using TradeViewer.Web.Api;
using TradeViewer.Web.Realtime;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The browser talks to TradeViewer.Api only, which served this application (architecture, chapter 2).
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<WorkspaceApi>();
builder.Services.AddScoped<AssetsApi>();
builder.Services.AddScoped<ChartsApi>();
builder.Services.AddScoped<WorkspaceEvents>();

await builder.Build().RunAsync();
