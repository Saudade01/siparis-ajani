using Microsoft.Data.Sqlite;

namespace SiparisAjani.Infrastructure.Persistence;

/// <summary>Veritabanı yapılandırması (<c>appsettings.json</c> → <c>Database</c>).</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Varsayılan bağlantı dizesi; yol içerik köküne göre çözülür.</summary>
    public const string DefaultConnectionString = "Data Source=App_Data/siparisajani.db";

    public string ConnectionString { get; set; } = DefaultConnectionString;

    /// <summary>
    /// Bağlantı dizesindeki göreli dosya yolunu <paramref name="basePath"/> altına taşır.
    /// Çalışma dizini neresi olursa olsun aynı veritabanı dosyası açılsın diye gereklidir.
    /// </summary>
    public static string Resolve(string? connectionString, string basePath)
    {
        string value = string.IsNullOrWhiteSpace(connectionString) ? DefaultConnectionString : connectionString;
        SqliteConnectionStringBuilder builder = new(value);

        if (builder.DataSource.Length > 0
            && !builder.DataSource.Contains(":memory:", StringComparison.Ordinal)
            && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.GetFullPath(Path.Combine(basePath, builder.DataSource));
        }

        return builder.ToString();
    }

    /// <summary>Veritabanı dosyasının klasörünü oluşturur; SQLite eksik klasörü kendisi açmaz.</summary>
    public static void EnsureDirectory(string connectionString)
    {
        SqliteConnectionStringBuilder builder = new(connectionString);
        if (builder.DataSource.Contains(":memory:", StringComparison.Ordinal))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(builder.DataSource);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}