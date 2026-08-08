using EpubReader.Application;
using EpubReader.Infrastructure;
using EpubReader.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddEpubReaderCore();
builder.Services.AddEpubReaderApplication();
builder.Services.AddEpubReaderWebPlatform();

await builder.Build().RunAsync();
