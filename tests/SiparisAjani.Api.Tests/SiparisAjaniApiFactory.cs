using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace SiparisAjani.Api.Tests;

/// <summary>
/// Testler için API örneği. Claude modu bilerek KAPALI tutulur:
/// testler hiçbir koşulda gerçek API çağırmaz.
/// Veritabanı geçici klasörde açılır ve test sonunda silinir; depo ve
/// <c>App_Data/</c> kirlenmez. Her test sınıfı kendi dosyasını alır.
/// </summary>
public sealed class SiparisAjaniApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseDirectory =
        Path.Combine(Path.GetTempPath(), $"siparisajani-tests-{Guid.NewGuid():N}");

    public string DatabasePath => Path.Combine(_databaseDirectory, "test.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_databaseDirectory);

        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:Enabled"] = "false",
                ["Anthropic:ApiKey"] = string.Empty,
                ["Database:ConnectionString"] = $"Data Source={DatabasePath}"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_databaseDirectory))
        {
            Directory.Delete(_databaseDirectory, recursive: true);
        }
    }
}