using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Blaizio.Docs;
using Blaizio.Docs.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddBlaizioUi();
builder.Services.AddScoped<IDocsJs, DocsJs>();
builder.Services.AddSingleton<ICodeHighlighter, CodeHighlighter>();
builder.Services.AddScoped<IApiDocs, ApiDocs>();

await builder.Build().RunAsync();
