using System.Text;
using System.Text.Json;

using Anthropic;
using Anthropic.Models.Messages;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SiparisAjani.Contracts;

using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Models;
using SiparisAjani.Core.Text;

namespace SiparisAjani.Infrastructure.Claude;

/// <summary>
/// Siparişi Claude ile ayrıştırır (resmi <c>Anthropic</c> paketi, yapılandırılmış JSON çıktı).
///
/// KVKK: modele ham mesaj değil, maskelenmiş metinden ayıklanmış ürün satırı adayları gider
/// . Selamlaşma, imza, adres ve iletişim satırları sunucudan hiç çıkmaz.
/// </summary>
public sealed class ClaudeOrderExtractor(
    AnthropicClient client,
    IOptions<ClaudeOptions> options,
    ILogger<ClaudeOrderExtractor> logger) : IOrderExtractor
{
    private const string RefusalWarning =
        "Model bu metni işlemeyi reddetti. Kural tabanlı sonucu kullanın ya da metni sadeleştirip tekrar deneyin.";

    private const string FailureWarning =
        "Claude'a ulaşılamadı, sipariş çözümlenemedi. Biraz sonra tekrar deneyin.";

    private const string SystemPrompt = """
        Sen bir toptancının sipariş masasında çalışan asistansın. Sana WhatsApp ya da e-postadan
        gelmiş, ürün satırı olabilecek metin parçaları verilecek.

        Görevin: her sipariş kalemini ayrı bir satır olarak çıkarmak.
        - quantity: yazılı miktar. "iki" gibi yazıyla yazılmışsa sayıya çevir. Yazmıyorsa null.
        - unit: adet, kutu, paket, koli, metre, çuval, torba, rulo, kova, çift, takım, top gibi
          sipariş birimi. "kg", "lt", "mm" gibi ölçüler birim DEĞİLDİR, ürün adının parçasıdır.
          Birim yazmıyorsa null.
        - productText: yalnızca ürün adı; miktar ve birim çıkarılmış hâli. Ebatları koru (4x40, 3.5x25).
        - "toplam 15 olsun" gibi düzeltmelerde son miktarı yaz ve kalemi bir kez üret.
        - "5'er adet" gibi dağıtılmış miktarları her satıra ayrı ayrı uygula.
        - Ürün olmayan satırları (selamlaşma, teşekkür, teslimat notu) atla.
        - Metinde [telefon], [e-posta], [ad] gibi yer tutucular varsa bunlar maskelenmiş kişisel
          veridir; asla ürün sayma ve çıktıda tekrarlama.

        Yalnızca istenen JSON'u üret.
        """;

    public ExtractorKind Kind => ExtractorKind.Claude;

    public async Task<ExtractionResult> ExtractAsync(string maskedText, CancellationToken cancellationToken = default)
    {
        ProductLineFilterResult filtered = ProductLineFilter.Filter(maskedText);
        if (filtered.Lines.Count == 0)
        {
            return new ExtractionResult([], ["Metinde ürün satırı bulunamadı."]);
        }

        List<string> warnings = [BuildNarrowingWarning(filtered)];

        try
        {
            ClaudeCallResult call = await SendAsync(BuildPrompt(filtered.Lines), cancellationToken);
            if (call.Refused)
            {
                logger.LogWarning("Claude isteği reddetti; boş sonuç dönülüyor.");
                return new ExtractionResult([], [.. warnings, RefusalWarning]);
            }

            ExtractionResult parsed = ClaudeResponseParser.Parse(call.Text);
            return new ExtractionResult(parsed.Lines, [.. warnings, .. parsed.Warnings]);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Sipariş metni günlüğe yazılmaz; yalnızca hata türü.
            logger.LogError("Claude çağrısı başarısız: {ExceptionType}", exception.GetType().Name);
            return new ExtractionResult([], [.. warnings, FailureWarning]);
        }
    }

    /// <summary>Modele kaç satır gittiğini kullanıcıya bildirir (KVKK şeffaflığı).</summary>
    public static string BuildNarrowingWarning(ProductLineFilterResult filtered)
    {
        return $"Modele yalnızca {filtered.Lines.Count} ürün satırı gönderildi; " +
               $"{filtered.DroppedCount} satır (selamlaşma, imza, iletişim bilgisi) ayıklandı.";
    }

    /// <summary>Modele gidecek kullanıcı mesajını kurar.</summary>
    public static string BuildPrompt(IReadOnlyList<string> lines)
    {
        StringBuilder builder = new();
        builder.AppendLine("Aşağıdaki satırlardan sipariş kalemlerini çıkar:");
        foreach (string line in lines)
        {
            builder.Append("- ").AppendLine(line);
        }

        return builder.ToString();
    }

    private sealed record ClaudeCallResult(string? Text, bool Refused);

    private async Task<ClaudeCallResult> SendAsync(string prompt, CancellationToken cancellationToken)
    {
        ClaudeOptions settings = options.Value;
        Dictionary<string, JsonElement> schema = ClaudeResponseParser.CreateSchema();

        if (settings.UseServerSideFallback)
        {
            return await SendWithFallbackAsync(settings, schema, prompt, cancellationToken);
        }

        Message response = await client.Messages.Create(
            new MessageCreateParams
            {
                Model = settings.Model,
                MaxTokens = settings.MaxTokens,
                System = SystemPrompt,
                Messages = [new() { Role = Role.User, Content = prompt }],
                OutputConfig = new OutputConfig
                {
                    Format = new JsonOutputFormat { Schema = schema }
                }
            },
            cancellationToken: cancellationToken);

        return ReadResult(response.StopReason?.Raw(), response.Content.Select(block => block.Value).OfType<TextBlock>());
    }

    private async Task<ClaudeCallResult> SendWithFallbackAsync(
        ClaudeOptions settings,
        Dictionary<string, JsonElement> schema,
        string prompt,
        CancellationToken cancellationToken)
    {
        // Model politika gereği reddederse sunucu tarafı yedek modele düşer (beta uç noktası).
        Anthropic.Models.Beta.Messages.BetaMessage response = await client.Beta.Messages.Create(
            new Anthropic.Models.Beta.Messages.MessageCreateParams
            {
                Model = settings.Model,
                MaxTokens = settings.MaxTokens,
                System = SystemPrompt,
                Messages = [new() { Role = Anthropic.Models.Beta.Messages.Role.User, Content = prompt }],
                OutputConfig = new Anthropic.Models.Beta.Messages.BetaOutputConfig
                {
                    Format = new Anthropic.Models.Beta.Messages.BetaJsonOutputFormat { Schema = schema }
                },
                Betas = ["server-side-fallback-2026-06-01"],
                Fallbacks = new List<Anthropic.Models.Beta.Messages.BetaFallbackParam>
                {
                    new() { Model = Model.ClaudeOpus4_8 }
                }
            },
            cancellationToken: cancellationToken);

        return ReadResult(
            response.StopReason?.Raw(),
            response.Content.Select(block => block.Value).OfType<Anthropic.Models.Beta.Messages.BetaTextBlock>());
    }

    private static ClaudeCallResult ReadResult(string? stopReason, IEnumerable<TextBlock> blocks)
    {
        return string.Equals(stopReason, "refusal", StringComparison.Ordinal)
            ? new ClaudeCallResult(null, Refused: true)
            : new ClaudeCallResult(string.Concat(blocks.Select(block => block.Text)), Refused: false);
    }

    private static ClaudeCallResult ReadResult(
        string? stopReason,
        IEnumerable<Anthropic.Models.Beta.Messages.BetaTextBlock> blocks)
    {
        return string.Equals(stopReason, "refusal", StringComparison.Ordinal)
            ? new ClaudeCallResult(null, Refused: true)
            : new ClaudeCallResult(string.Concat(blocks.Select(block => block.Text)), Refused: false);
    }
}