using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using SiparisAjani.Contracts;

using SiparisAjani.Core.Matching;
using SiparisAjani.Core.Models;
using SiparisAjani.Core.Parsing;
using SiparisAjani.Core.Text;
using SiparisAjani.Infrastructure.Catalog;
using SiparisAjani.Infrastructure.Persistence;
using SiparisAjani.Infrastructure.Persistence.Entities;
using SiparisAjani.Infrastructure.Storage;

namespace SiparisAjani.Core.Tests;

/// <summary>
/// Her test için geçici klasörde gerçek bir SQLite dosyası açar. Bellek içi sağlayıcı
/// kullanılmıyor: göçlerin, indekslerin ve yabancı anahtarların gerçekten çalıştığını görmek
/// istiyoruz. Testler asla depoyu ya da <c>App_Data/</c> klasörünü kirletmez.
/// </summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly string _directory;

    public TestDatabase()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"siparisajani-db-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);

        ConnectionString = $"Data Source={Path.Combine(_directory, "test.db")}";
        DbContextOptions<OrderAgentDbContext> options = new DbContextOptionsBuilder<OrderAgentDbContext>()
          .UseSqlite(ConnectionString)
          .Options;

        Factory = new SimpleDbContextFactory(options);
    }

    public string ConnectionString { get; }

    public IDbContextFactory<OrderAgentDbContext> Factory { get; }

    public static string CatalogCsvPath => Path.Combine(TestData.Root, "data", "katalog", "hirdavat-ornek.csv");

    public OrderAgentDbContext CreateContext() => Factory.CreateDbContext();

    /// <summary>Göçleri uygular ve (istenirse) katalogu tohumlar.</summary>
    public Task<int> InitializeAsync(string? seedPath = null)
    {
        return DatabaseInitializer.InitializeAsync(Factory, seedPath ?? CatalogCsvPath, NullLogger.Instance);
    }

    /// <summary>Verilen tablolardaki tüm metin değerlerini ham SQL ile okur (KVKK denetimi için).</summary>
    public List<string> ReadAllText(params string[] tables)
    {
        List<string> values = [];
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        foreach (string table in tables)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM {table}";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                for (int index = 0; index < reader.FieldCount; index++)
                {
                    if (!reader.IsDBNull(index))
                    {
                        values.Add(reader.GetValue(index).ToString() ?? string.Empty);
                    }
                }
            }
        }

        return values;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}

/// <summary>Testlerde her çağrıda yeni bağlam üreten küçük fabrika (havuzlama yok).</summary>
internal sealed class SimpleDbContextFactory(DbContextOptions<OrderAgentDbContext> options)
   : IDbContextFactory<OrderAgentDbContext>
{
    public OrderAgentDbContext CreateDbContext() => new(options);
}

public class DatabaseInitializerTests
{
    [Fact]
    public async Task Initialize_GocleriUygularVeTablolariOlusturur()
    {
        using TestDatabase database = new();

        await database.InitializeAsync();

        using OrderAgentDbContext context = database.CreateContext();
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.ProductAliases.ToListAsync());
        Assert.Empty(await context.OrderDrafts.ToListAsync());
        Assert.Empty(await context.OrderDraftLines.ToListAsync());
    }

    [Fact]
    public async Task Initialize_BosVeritabaniniCsvdenTohumlar()
    {
        using TestDatabase database = new();

        int seeded = await database.InitializeAsync();

        Assert.Equal(TestData.Catalog.Count, seeded);

        using OrderAgentDbContext context = database.CreateContext();
        CatalogItemEntity item = await context.CatalogItems.SingleAsync(entity => entity.Sku == "VD-4040-SNT");
        Assert.Equal("Sunta Vidası 4x40 (500'lü kutu)", item.Name);
        Assert.Equal(TurkishTextNormalizer.Normalize(item.Name), item.NormalizedName);
    }

    [Fact]
    public async Task Initialize_DoluVeritabaniniTekrarTohumlamaz()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        await using (OrderAgentDbContext context = database.CreateContext())
        {
            await context.CatalogItems.Where(item => item.Sku == "VD-4040-SNT").ExecuteDeleteAsync();
        }

        int second = await database.InitializeAsync();

        // İkinci açılışta CSV'ye hiç bakılmaz: kullanıcının içe aktardığı katalog korunur.
        Assert.Equal(TestData.Catalog.Count - 1, second);
    }

    [Fact]
    public async Task Initialize_KatalogDosyasiYoksaBosBaslar()
    {
        using TestDatabase database = new();

        int seeded = await database.InitializeAsync(Path.Combine(Path.GetTempPath(), $"yok-{Guid.NewGuid():N}.csv"));

        Assert.Equal(0, seeded);
    }

    [Fact]
    public async Task Initialize_AyniStokKoduIkiKezYazilamaz()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        await using OrderAgentDbContext context = database.CreateContext();
        context.CatalogItems.Add(CatalogItemEntity.FromModel(
            new CatalogItem("VD-4040-SNT", "Kopya", "adet", null, null),
            DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}

public class EfCatalogStoreTests
{
    [Fact]
    public async Task Store_TohumlananKatalogaVeritabanindanUlasir()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfCatalogStore store = new(database.Factory);

        Assert.Equal(TestData.Catalog.Count, store.Count);
        Assert.NotNull(store.Find("vd-4040-snt"));
        Assert.Null(store.Find("YOK-1"));
        Assert.Null(store.Find("  "));
    }

    [Fact]
    public async Task All_KatalogDegismedikceAyniOrnegiDoner()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfCatalogStore store = new(database.Factory);

        // Eşleştirici dizinini bu örneğe göre önbelleğe alıyor.
        Assert.Same(store.All, store.All);
    }

    [Fact]
    public async Task Search_TurkceKarakterdenBagimsizBulur()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfCatalogStore store = new(database.Factory);

        Assert.Contains(store.Search("sunta vidasi", 20), item => item.Sku == "VD-4040-SNT");
        Assert.Contains(store.Search("SUNTA VİDASI", 20), item => item.Sku == "VD-4040-SNT");
    }

    [Fact]
    public async Task Search_StokKoduVeBarkodlaDaBulur()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfCatalogStore store = new(database.Factory);

        Assert.Contains(store.Search("VD-4040-SNT", 20), item => item.Sku == "VD-4040-SNT");
    }

    [Fact]
    public async Task Search_BosSorgudaLimitKadarDonerVeYuzIleSinirlar()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfCatalogStore store = new(database.Factory);

        Assert.Equal(5, store.Search(null, 5).Count);
        Assert.Equal(100, store.Search(null, 1000).Count);
    }

    [Fact]
    public async Task Replace_VeritabaninaYazarVeEskisiniSiler()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfCatalogStore store = new(database.Factory);
        store.Replace([new CatalogItem("T-1", "Test Vida 4x40", "kutu", "869", "vida")]);

        Assert.Equal(1, store.Count);
        Assert.Null(store.Find("VD-4040-SNT"));

        // Yeni bir örnek aynı veriyi veritabanından okur: kalıcılık gerçekten sağlanıyor.
        EfCatalogStore reopened = new(database.Factory);
        Assert.Equal(1, reopened.Count);
        Assert.Equal("Test Vida 4x40", reopened.Find("T-1")!.Name);
        Assert.Contains(reopened.Search("test vida", 20), item => item.Sku == "T-1");
    }

    [Fact]
    public async Task Replace_NormalizeAdiDaYazar()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfCatalogStore store = new(database.Factory);
        store.Replace([new CatalogItem("T-1", "Şeffaf Silikon", "adet", null, null)]);

        await using OrderAgentDbContext context = database.CreateContext();
        CatalogItemEntity item = await context.CatalogItems.SingleAsync();
        Assert.Equal("seffaf silikon", item.NormalizedName);
    }
}

public class EfAliasStoreTests
{
    [Fact]
    public async Task SaveAsync_KaydederVeNormallestirerekGeriOkur()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfAliasStore store = new(database.Factory);
        await store.SaveAsync("japon yapıştırıcı", "YP-HZL-20");

        Assert.Equal("YP-HZL-20", store.Resolve("Japon Yapıştırıcı"));
        Assert.Equal("YP-HZL-20", new EfAliasStore(database.Factory).Resolve("japon yapistirici"));
    }

    [Fact]
    public async Task SaveAsync_AyniMetniGuncellerYeniSatirEklemez()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfAliasStore store = new(database.Factory);
        await store.SaveAsync("japon yapıştırıcı", "YP-HZL-20");
        await store.SaveAsync("Japon yapıştırıcı", "YP-HZL-50");

        await using OrderAgentDbContext context = database.CreateContext();
        ProductAliasEntity alias = await context.ProductAliases.SingleAsync();
        Assert.Equal("YP-HZL-50", alias.Sku);
        Assert.Equal("YP-HZL-50", store.Resolve("japon yapıştırıcı"));
    }

    [Fact]
    public async Task Resolve_BilinmeyenVeBosMetindeNullDoner()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfAliasStore store = new(database.Factory);

        Assert.Null(store.Resolve("hiç görülmemiş ürün"));
        Assert.Null(store.Resolve("   "));
    }

    [Fact]
    public async Task SaveAsync_BosDegerleriYokSayar()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfAliasStore store = new(database.Factory);
        await store.SaveAsync("   ", "YP-HZL-20");
        await store.SaveAsync("japon yapıştırıcı", "  ");

        await using OrderAgentDbContext context = database.CreateContext();
        Assert.Empty(await context.ProductAliases.ToListAsync());
    }

    [Fact]
    public async Task Parse_TakmaAdEslestirmeyiEtkiler()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfCatalogStore catalog = new(database.Factory);
        EfAliasStore aliases = new(database.Factory);
        OrderParsingService service = new(
            new PersonalDataMasker(),
            new RuleBasedOrderExtractor(),
            new ProductMatcher(),
            catalog,
            aliases);

        const string Text = "3 tane japon yapıştırıcı";

        OrderDraftDto before = await service.ParseAsync(new ParseOrderRequest(Text));
        Assert.NotEqual("YP-HZL-20", before.Lines[0].BestMatch?.Sku);

        await aliases.SaveAsync("japon yapıştırıcı", "YP-HZL-20");

        OrderDraftDto after = await service.ParseAsync(new ParseOrderRequest(Text));
        Assert.Equal(LineStatus.Matched, after.Lines[0].Status);
        Assert.Equal("YP-HZL-20", after.Lines[0].BestMatch!.Sku);
    }
}

public class EfOrderDraftLogTests
{
    private static OrderDraftDto CreateDraft(string draftId, DateTimeOffset createdAt)
    {
        return new OrderDraftDto(
            draftId,
            ExtractorKind.RuleBased,
            CustomerHint: null,
            [
                new OrderLineDto(
                    1,
                    "5 kutu 4x40 sunta vidası",
                    5m,
                    "kutu",
                    "4x40 sunta vidası",
                    LineStatus.Matched,
                    new MatchCandidateDto("VD-4040-SNT", "Sunta Vidası 4x40 (500'lü kutu)", "kutu", 0.91),
                    []),
                new OrderLineDto(
                    2,
                    "2 koli lavabo açıcı",
                    2m,
                    "koli",
                    "lavabo açıcı",
                    LineStatus.NotFound,
                    null,
                    [])
            ],
            ["phone", "name"],
            ["2. satır katalogda bulunamadı, arayıp seçebilirsiniz."],
            createdAt);
    }

    [Fact]
    public async Task RecordAsync_OlcumleriVeSatirlariYazar()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfOrderDraftLog log = new(database.Factory, NullLogger<EfOrderDraftLog>.Instance);
        await log.RecordAsync(OrderDraftSummary.FromDraft(
            CreateDraft("d_1", DateTimeOffset.UtcNow),
            OrderSource.WhatsApp,
            durationMs: 42,
            characterCount: 120));

        await using OrderAgentDbContext context = database.CreateContext();
        OrderDraftEntity draft = await context.OrderDrafts.Include(entity => entity.Lines).SingleAsync();

        Assert.Equal("d_1", draft.DraftId);
        Assert.Equal(OrderSource.WhatsApp, draft.Source);
        Assert.Equal(ExtractorKind.RuleBased, draft.Extractor);
        Assert.Equal(2, draft.LineCount);
        Assert.Equal(1, draft.MatchedCount);
        Assert.Equal(1, draft.NotFoundCount);
        Assert.Equal(0, draft.AmbiguousCount);
        Assert.Equal(1, draft.WarningCount);
        Assert.Equal(42, draft.DurationMs);
        Assert.Equal(120, draft.CharacterCount);
        string[] expectedMaskedFields = ["phone", "name"];
        Assert.Equal(expectedMaskedFields, draft.MaskedFields);
        Assert.Equal(2, draft.Lines.Count);
        Assert.Equal("VD-4040-SNT", draft.Lines.Single(line => line.LineNo == 1).MatchedSku);
        Assert.Null(draft.Lines.Single(line => line.LineNo == 2).MatchedSku);
    }

    [Fact]
    public async Task RecordAsync_SiparisMetniniVeKisiselVeriyiYazmaz()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfOrderDraftLog log = new(database.Factory, NullLogger<EfOrderDraftLog>.Instance);
        await log.RecordAsync(OrderDraftSummary.FromDraft(
            CreateDraft("d_1", DateTimeOffset.UtcNow),
            OrderSource.WhatsApp,
            durationMs: 42,
            characterCount: 120));

        List<string> stored = database.ReadAllText("OrderDrafts", "OrderDraftLines");

        // Satır metni, ürün metni, ürün adı ve uyarı cümlesi veritabanına yazılmaz (KVKK kararı).
        string[] forbiddenTexts = ["sunta", "lavabo", "kutu", "koli", "satır", "yapıştırıcı"];
        foreach (string forbidden in forbiddenTexts)
        {
            Assert.DoesNotContain(stored, value => value.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task GetRecentAsync_EnYeniyiOnceDonerVeLimitler()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfOrderDraftLog log = new(database.Factory, NullLogger<EfOrderDraftLog>.Instance);
        DateTimeOffset start = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

        for (int index = 0; index < 5; index++)
        {
            await log.RecordAsync(OrderDraftSummary.FromDraft(
                CreateDraft($"d_{index}", start.AddMinutes(index)),
                OrderSource.WhatsApp,
                durationMs: 10,
                characterCount: 50));
        }

        IReadOnlyList<OrderDraftSummary> recent = await log.GetRecentAsync(3);

        Assert.Equal(3, recent.Count);
        string[] expectedOrder = ["d_4", "d_3", "d_2"];
        Assert.Equal(expectedOrder, recent.Select(summary => summary.DraftId));
        Assert.Equal(2, recent[0].Lines.Count);
        Assert.Equal(1, recent[0].Lines[0].LineNo);
    }

    [Fact]
    public async Task GetRecentAsync_LimitiSinirlar()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfOrderDraftLog log = new(database.Factory, NullLogger<EfOrderDraftLog>.Instance);
        await log.RecordAsync(OrderDraftSummary.FromDraft(
            CreateDraft("d_1", DateTimeOffset.UtcNow),
            OrderSource.Phone,
            durationMs: 10,
            characterCount: 50));

        Assert.Single(await log.GetRecentAsync(0));
        Assert.Single(await log.GetRecentAsync(5000));
    }

    [Fact]
    public async Task Remove_TaslakSilininceSatirlariDaSilinir()
    {
        using TestDatabase database = new();
        await database.InitializeAsync();

        EfOrderDraftLog log = new(database.Factory, NullLogger<EfOrderDraftLog>.Instance);
        await log.RecordAsync(OrderDraftSummary.FromDraft(
            CreateDraft("d_1", DateTimeOffset.UtcNow),
            OrderSource.Email,
            durationMs: 10,
            characterCount: 50));

        await using OrderAgentDbContext context = database.CreateContext();
        OrderDraftEntity draft = await context.OrderDrafts.SingleAsync();
        context.OrderDrafts.Remove(draft);
        await context.SaveChangesAsync();

        Assert.Empty(await context.OrderDraftLines.ToListAsync());
    }
}