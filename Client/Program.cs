using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using StallmedManager.Client;
using StallmedManager.Client.Services;
using System.Globalization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<UserManager>();
builder.Services.AddScoped<UiPreferencesService>();
builder.Services.AddScoped<DataService>();
builder.Services.AddScoped<StockSearchClientService>();

var host = builder.Build();

await host.RunAsync();