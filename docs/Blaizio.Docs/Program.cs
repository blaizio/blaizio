using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Blaizio.Docs;
using Blaizio.Docs.Services;
using Blaizio.Ui;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
// Registered explicitly (not via AddBlaizioUi) so the dogfood build — where the styled layer is
// CLI-copied source and the app-wide DI glue is intentionally not shipped — compiles identically.
builder.Services.AddBlaizioBase();
builder.Services.AddScoped<IDialogService, DialogService>();
builder.Services.AddScoped<IToastService, ToastService>();
builder.Services.AddScoped<IDocsJs, DocsJs>();
builder.Services.AddSingleton<ICodeHighlighter, CodeHighlighter>();
builder.Services.AddScoped<IApiDocs, ApiDocs>();

await builder.Build().RunAsync();
