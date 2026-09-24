using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using SiparisAjani.Infrastructure.Persistence.Entities;

namespace SiparisAjani.Infrastructure.Persistence;

/// <summary>
/// Uygulamanın tek veritabanı bağlamı (SQLite). Katalog, öğrenilen düzeltmeler ve
/// çözümleme geçmişinin ölçüm kayıtları burada durur. Şema göçlerle yönetilir
/// (<c>Migrations</c> klasörü), açılışta uygulanır.
/// </summary>
public sealed class OrderAgentDbContext(DbContextOptions<OrderAgentDbContext> options) : DbContext(options)
{
    public DbSet<CatalogItemEntity> CatalogItems => Set<CatalogItemEntity>();

    public DbSet<ProductAliasEntity> ProductAliases => Set<ProductAliasEntity>();

    public DbSet<OrderDraftEntity> OrderDrafts => Set<OrderDraftEntity>();

    public DbSet<OrderDraftLineEntity> OrderDraftLines => Set<OrderDraftLineEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<CatalogItemEntity>(entity =>
        {
            entity.ToTable("CatalogItems");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Sku).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(256).IsRequired();
            entity.Property(item => item.NormalizedName).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Unit).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Barcode).HasMaxLength(64);
            entity.Property(item => item.Category).HasMaxLength(64);

            // Stok kodu kataloğun iş anahtarıdır: aynı kod iki kez yazılamaz.
            entity.HasIndex(item => item.Sku).IsUnique().HasDatabaseName("IX_CatalogItems_Sku");
            entity.HasIndex(item => item.NormalizedName).HasDatabaseName("IX_CatalogItems_NormalizedName");
        });

        modelBuilder.Entity<ProductAliasEntity>(entity =>
        {
            entity.ToTable("ProductAliases");
            entity.HasKey(alias => alias.Id);
            entity.Property(alias => alias.NormalizedProductText).HasMaxLength(256).IsRequired();
            entity.Property(alias => alias.Sku).HasMaxLength(64).IsRequired();

            entity.HasIndex(alias => alias.NormalizedProductText)
.IsUnique()
.HasDatabaseName("IX_ProductAliases_NormalizedProductText");
            entity.HasIndex(alias => alias.Sku).HasDatabaseName("IX_ProductAliases_Sku");
        });

        modelBuilder.Entity<OrderDraftEntity>(entity =>
        {
            entity.ToTable("OrderDrafts");
            entity.HasKey(draft => draft.Id);
            entity.Property(draft => draft.DraftId).HasMaxLength(64).IsRequired();

            // Enum'lar sayı değil metin olarak saklanır: veritabanına elle bakan biri
            // "RuleBased" görsün, sıralama değişirse eski kayıtlar bozulmasın.
            entity.Property(draft => draft.Source).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(draft => draft.Extractor).HasConversion<string>().HasMaxLength(32).IsRequired();

            entity.HasIndex(draft => draft.DraftId).IsUnique().HasDatabaseName("IX_OrderDrafts_DraftId");
            entity.HasIndex(draft => draft.CreatedAt).HasDatabaseName("IX_OrderDrafts_CreatedAt");

            entity.HasMany(draft => draft.Lines)
.WithOne(line => line.OrderDraft!)
.HasForeignKey(line => line.OrderDraftId)
.OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderDraftLineEntity>(entity =>
        {
            entity.ToTable("OrderDraftLines");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(line => line.MatchedSku).HasMaxLength(64);

            entity.HasIndex(line => line.OrderDraftId).HasDatabaseName("IX_OrderDraftLines_OrderDraftId");
            entity.HasIndex(line => line.MatchedSku).HasDatabaseName("IX_OrderDraftLines_MatchedSku");
        });

        // SQLite DateTimeOffset'i metin olarak saklar ve ORDER BY yapamaz ("son N taslak"
        // sorgusu bu yüzden çalışmıyordu). Yerleşik ikili dönüştürücü hem saat dilimini korur
        // hem de sıralanabilir bir sayı yazar.
        foreach (IMutableProperty property in modelBuilder.Model
.GetEntityTypes()
.SelectMany(entityType => entityType.GetProperties())
.Where(property => property.ClrType == typeof(DateTimeOffset)))
        {
            property.SetValueConverter(typeof(DateTimeOffsetToBinaryConverter));
        }
    }
}