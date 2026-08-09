using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Blaizio.Docs;
using Blaizio.Docs.Services;
using Blaizio.Ui;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
// One call registers everything Blaizio needs (stores, dialog/toast services, theme service);
// the options lambda is where app-wide component defaults go, e.g. o => o.Toast.RichColors = true.
builder.Services.AddBlaizio();
builder.Services.AddScoped<IDocsJs, DocsJs>();
builder.Services.AddSingleton<IExampleSource, ExampleSource>();
builder.Services.AddSingleton<ISnippetSource, SnippetSource>();
builder.Services.AddSingleton<ICodeHighlighter, CodeHighlighter>();
builder.Services.AddScoped<IApiDocs, ApiDocs>();
// The docs' own registry at /r - feeds the per-skin "Source" view on component pages.
builder.Services.AddScoped<IRegistrySource, RegistrySource>();
builder.Services.AddSingleton<ISlotCatalog, SlotCatalog>();
// The /community data files (registries + themes) under wwwroot/community/.
builder.Services.AddScoped<ICommunitySource, CommunitySource>();
// The theme composer's selection/locks/history - pure state - and the applier that writes a
// selection onto the document (theme.ts via IDocsJs). ThemesPage coordinates the two.
builder.Services.AddScoped<ThemeComposerState>();
builder.Services.AddScoped<ThemeApplier>();

await builder.Build().RunAsync();
