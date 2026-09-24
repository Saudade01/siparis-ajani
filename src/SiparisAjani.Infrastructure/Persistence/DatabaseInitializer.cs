using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SiparisAjani.Core.Models;
using SiparisAjani.Infrastructure.Catalog;
using SiparisAjani.Infrastructure.Persistence.Entities;

namespace SiparisAjani.Infrastructure.Persistence;

/// <summary>
/// Açılışta veritabanını hazırlar: göçleri uygular, katalog boşsa CSV'den tohumlar.
/// Katalog doluysa CSV'ye hiç bakılmaz; kullanıcının içe aktardığı katalog korunur.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task<int> InitializeAsync(
        IDbContextFactory<OrderAgentDbContext> contextFactory,
        string catalogSeedPath,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(logger);

        await using OrderAgentDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        string? connectionString = context.Database.GetConnectionString();
        if (connectionString is not null)
        {
            DatabaseOptions.EnsureDirectory(connectionString);
        }

        await context.Database.MigrateAsync(cancellationToken);

        int existing = await context.CatalogItems.CountAsync(cancellationToken);
        if (existing > 0)
        {
            logger.LogInformation("Katalog veritabanında hazır: {Count} ürün.", existing);
            return existing;
        }

        return await SeedCatalogAsync(context, catalogSeedPath, logger, cancellationToken);
    }

    private static async Task<int> SeedCatalogAsync(
        OrderAgentDbContext context,
        string catalogSeedPath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(catalogSeedPath))
        {
            logger.LogWarning("Başlangıç kataloğu bulunamadı: {SeedPath}. Katalog boş başlıyor.", catalogSeedPath);
            return 0;
        }

        CatalogLoadResult result;
        try
        {
            await using FileStream stream = File.OpenRead(catalogSeedPath);
            result = CsvCatalogLoader.Load(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError("Başlangıç kataloğu okunamadı: {ExceptionType}", exception.GetType().Name);
            return 0;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        context.CatalogItems.AddRange(result.Items.Select(item => CatalogItemEntity.FromModel(item, now)));
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Katalog CSV'den tohumlandı: {Count} ürün, {Skipped} satır atlandı.",
            result.Items.Count,
            result.Skipped);

        return result.Items.Count;
    }
}