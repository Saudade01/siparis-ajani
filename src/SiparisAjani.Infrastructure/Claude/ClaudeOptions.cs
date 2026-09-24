namespace SiparisAjani.Infrastructure.Claude;

/// <summary>
/// Claude ayarları (<c>appsettings.json</c> → <c>Anthropic</c>).
/// Varsayılan olarak KAPALIDIR: açmak için hem <see cref="Enabled"/> true olmalı hem de
/// bir anahtar bulunmalı (KVKK incelemesi tamamlanana kadar yalnızca sentetik veri).
/// </summary>
public sealed class ClaudeOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>KVKK incelemesi bitene kadar varsayılan false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Boşsa <c>ANTHROPIC_API_KEY</c> ortam değişkenine bakılır.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-opus-5";

    public int MaxTokens { get; set; } = 16_000;

    /// <summary>
    /// Model politika gereği reddederse sunucu tarafında yedek modele düşülsün mü?
    /// Beta uç noktası kullanılır.
    /// </summary>
    public bool UseServerSideFallback { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Yapılandırmadan ya da ortam değişkeninden çözülen anahtar.</summary>
    public string? ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            return ApiKey.Trim();
        }

        string? fromEnvironment = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        return string.IsNullOrWhiteSpace(fromEnvironment) ? null : fromEnvironment.Trim();
    }

    /// <summary>Claude modu hem açık hem de anahtarı var mı?</summary>
    public bool IsUsable => Enabled && ResolveApiKey() is not null;
}