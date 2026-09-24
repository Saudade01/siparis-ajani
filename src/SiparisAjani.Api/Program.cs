using Microsoft.EntityFrameworkCore;

using SiparisAjani.Api.Endpoints;
using SiparisAjani.Infrastructure;
using SiparisAjani.Infrastructure.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Dosya yolları içerik köküne göre çözülür; çalışma dizini neresi olursa olsun aynı sonucu verir.
builder.Configuration["Catalog:SeedPath"] = ResolvePath(
    builder.Environment.ContentRootPath,
    builder.Configuration["Catalog:SeedPath"] ?? DefaultCatalogSeedPath);

builder.Configuration[$"{DatabaseOptions.SectionName}:ConnectionString"] = DatabaseOptions.Resolve(
    builder.Configuration[$"{DatabaseOptions.SectionName}:ConnectionString"],
    builder.Environment.ContentRootPath);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSiparisAjaniInfrastructure(builder.Configuration);

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthEndpoints();
app.MapCatalogEndpoints();
app.MapOrderEndpoints();

ILogger startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Baslangic");

if (ServiceCollectionExtensions.IsClaudeEnabled(app.Configuration))
{
    startupLogger.LogWarning("{Warning}", ServiceCollectionExtensions.ClaudeEnabledWarning);
}

// Göçler uygulanır, katalog boşsa CSV'den tohumlanır; doluysa CSV'ye bakılmaz.
await DatabaseInitializer.InitializeAsync(
    app.Services.GetRequiredService<IDbContextFactory<OrderAgentDbContext>>(),
    app.Configuration["Catalog:SeedPath"]!,
    startupLogger);

app.Run();

static string ResolvePath(string contentRoot, string path)
{
    return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(contentRoot, path));
}

/// <summary>Tümleşik testlerin <c>WebApplicationFactory&lt;Program&gt;</c> kullanabilmesi için.</summary>
public partial class Program
{
    /// <summary>Veritabanı ilk kez oluşturulurken okunacak örnek katalog.</summary>
    public const string DefaultCatalogSeedPath = "../../data/katalog/hirdavat-ornek.csv";
}