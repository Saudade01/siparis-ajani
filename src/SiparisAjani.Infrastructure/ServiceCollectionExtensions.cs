using Anthropic;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Matching;
using SiparisAjani.Core.Parsing;
using SiparisAjani.Core.Text;
using SiparisAjani.Infrastructure.Catalog;
using SiparisAjani.Infrastructure.Claude;
using SiparisAjani.Infrastructure.Export;
using SiparisAjani.Infrastructure.Persistence;
using SiparisAjani.Infrastructure.Storage;

namespace SiparisAjani.Infrastructure;

/// <summary>Uygulama servislerinin kaydı. API bu tek çağrıyı yapar.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Claude modu açıkken açılışta gösterilecek uyarı .</summary>
    public const string ClaudeEnabledWarning =
        "Claude modu açık. KVKK incelemesi tamamlanana kadar yalnızca sentetik veri kullanın.";

    public static IServiceCollection AddSiparisAjaniInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ClaudeOptions>(configuration.GetSection(ClaudeOptions.SectionName));
        services.Configure<LogoExportOptions>(configuration.GetSection(LogoExportOptions.SectionName));
        services.PostConfigure<LogoExportOptions>(static settings =>
        {
            if (settings.Columns.Count == 0)
            {
                settings.Columns = LogoExportOptions.CreateDefault().Columns;
            }
        });

        // Bağlantı dizesi kayıt anında değil, çözümleme anında okunur: testlerin
        // (WebApplicationFactory) verdiği değer daha geç geliyor.
        services.AddDbContextFactory<OrderAgentDbContext>((provider, options) =>
        {
            IConfiguration current = provider.GetRequiredService<IConfiguration>();
            string connectionString = DatabaseOptions.Resolve(
                current[$"{DatabaseOptions.SectionName}:ConnectionString"],
                Directory.GetCurrentDirectory());

            options.UseSqlite(connectionString);
        });

        services.AddSingleton<IPersonalDataMasker, PersonalDataMasker>();
        services.AddSingleton<IProductMatcher, ProductMatcher>();
        services.AddSingleton<IOrderExporter, ExcelOrderExporter>();
        services.AddSingleton<ICatalogStore>(provider => new EfCatalogStore(
            provider.GetRequiredService<IDbContextFactory<OrderAgentDbContext>>(),
            provider.GetService<TimeProvider>()));
        services.AddSingleton<IAliasStore>(provider => new EfAliasStore(
            provider.GetRequiredService<IDbContextFactory<OrderAgentDbContext>>(),
            provider.GetService<TimeProvider>()));
        services.AddSingleton<IOrderDraftLog, EfOrderDraftLog>();

        services.AddSingleton<RuleBasedOrderExtractor>();
        services.AddSingleton(provider =>
        {
            ClaudeOptions claude = provider.GetRequiredService<IOptions<ClaudeOptions>>().Value;
            return new AnthropicClient
            {
                ApiKey = claude.ResolveApiKey(),
                Timeout = TimeSpan.FromSeconds(claude.TimeoutSeconds)
            };
        });

        services.AddSingleton<IOrderExtractor>(provider =>
        {
            ClaudeOptions claude = provider.GetRequiredService<IOptions<ClaudeOptions>>().Value;
            if (!claude.IsUsable)
            {
                // Varsayılan yol: anahtarsız, deterministik, testlerde ve CI'da çalışan ayrıştırıcı.
                return provider.GetRequiredService<RuleBasedOrderExtractor>();
            }

            return new ClaudeOrderExtractor(
                provider.GetRequiredService<AnthropicClient>(),
                provider.GetRequiredService<IOptions<ClaudeOptions>>(),
                provider.GetRequiredService<ILogger<ClaudeOrderExtractor>>());
        });

        services.AddSingleton<OrderParsingService>(provider => new OrderParsingService(
            provider.GetRequiredService<IPersonalDataMasker>(),
            provider.GetRequiredService<IOrderExtractor>(),
            provider.GetRequiredService<IProductMatcher>(),
            provider.GetRequiredService<ICatalogStore>(),
            provider.GetRequiredService<IAliasStore>(),
            provider.GetService<TimeProvider>()));

        return services;
    }

    /// <summary>
    /// Claude modunun açık olup olmadığını söyler. Açık olması için hem
    /// <c>Anthropic:Enabled=true</c> hem de bir anahtar gerekir.
    /// </summary>
    public static bool IsClaudeEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return ReadClaudeOptions(configuration).IsUsable;
    }

    private static ClaudeOptions ReadClaudeOptions(IConfiguration configuration)
    {
        ClaudeOptions claude = new();
        configuration.GetSection(ClaudeOptions.SectionName).Bind(claude);
        return claude;
    }
}