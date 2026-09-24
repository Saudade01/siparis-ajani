using SiparisAjani.Web.Components;
using SiparisAjani.Web.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
.AddInteractiveServerComponents()
.AddHubOptions(options =>
    {
        // Sipariş metni 20.000 karaktere kadar çıkabilir; Türkçe karakterlerle varsayılan 32 KB sınırını aşar.
        options.MaximumReceiveMessageSize = 256 * 1024;
    });

string apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5080";
int apiTimeoutSeconds = builder.Configuration.GetValue("Api:TimeoutSeconds", 30);

builder.Services.AddHttpClient<OrderAgentApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(apiTimeoutSeconds);
});
builder.Services.AddScoped<ApiStatusNotifier>();
builder.Services.AddScoped<FileDownloadService>();

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
.AddInteractiveServerRenderMode();

app.Run();