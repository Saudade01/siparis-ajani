using System.Diagnostics;

using SiparisAjani.Contracts;

using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Models;
using SiparisAjani.Core.Parsing;

namespace SiparisAjani.Api.Endpoints;

/// <summary>Sipariş çözümleme, düzeltme ve dışa aktarma uç noktaları.</summary>
public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.ParseOrder, async (
            ParseOrderRequest request,
            OrderParsingService parser,
            IOrderDraftLog draftLog,
            ILoggerFactory loggers,
            CancellationToken cancellationToken) =>
        {
            ILogger logger = loggers.CreateLogger("ParseOrder");

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return Results.Problem(
                    title: "Sipariş metni boş.",
                    detail: "Çözümlemek için bir sipariş metni yapıştırın.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (request.Text.Length > OrderParsingService.MaxTextLength)
            {
                return Results.Problem(
                    title: "Sipariş metni çok uzun.",
                    detail: $"En fazla {OrderParsingService.MaxTextLength} karakter gönderilebilir. Gönderilen: {request.Text.Length}.",
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            long started = Stopwatch.GetTimestamp();
            OrderDraftDto draft = await parser.ParseAsync(request, cancellationToken);
            int elapsedMs = (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            // Sipariş metni ve kişisel veri günlüğe yazılmaz; yalnızca ölçüm bilgisi.
            logger.LogInformation(
                "Sipariş çözümlendi: {LineCount} satır, {Extractor}, {ElapsedMs} ms, kaynak {Source}.",
                draft.Lines.Count,
                draft.Extractor,
                elapsedMs,
                request.Source);

            // Geçmişe yalnızca ölçüm yazılır (satır sayısı, durumlar, süre, maskelenen alan
            // türleri); metin ve kişisel veri yazılmaz. Yazma hatası isteği düşürmez.
            await draftLog.RecordAsync(
                OrderDraftSummary.FromDraft(draft, request.Source, elapsedMs, request.Text.Length),
                cancellationToken);

            return Results.Ok(draft);
        })
.WithName("ParseOrder");

        app.MapPost(ApiRoutes.Corrections, async (
            CorrectionRequest request,
            IAliasStore aliases,
            ICatalogStore catalog,
            ILoggerFactory loggers,
            CancellationToken cancellationToken) =>
        {
            ILogger logger = loggers.CreateLogger("Corrections");

            if (string.IsNullOrWhiteSpace(request.ProductText) || string.IsNullOrWhiteSpace(request.Sku))
            {
                return Results.Problem(
                    title: "Düzeltme eksik.",
                    detail: "Hem ürün metni hem de stok kodu gereklidir.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (catalog.Find(request.Sku) is null)
            {
                return Results.Problem(
                    title: "Stok kodu katalogda yok.",
                    detail: $"{request.Sku} kodlu ürün katalogda bulunamadı.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            await aliases.SaveAsync(request.ProductText, request.Sku, cancellationToken);
            logger.LogInformation("Düzeltme kaydedildi: {Sku}.", request.Sku);

            return Results.NoContent();
        })
.WithName("SaveCorrection");

        app.MapPost(ApiRoutes.ExportOrder, (
            ExportOrderRequest request,
            ICatalogStore catalog,
            IOrderExporter exporter,
            ILoggerFactory loggers) =>
        {
            ILogger logger = loggers.CreateLogger("ExportOrder");

            if (request.Lines.Count == 0)
            {
                return Results.Problem(
                    title: "Dışa aktarılacak satır yok.",
                    detail: "En az bir sipariş satırı gönderin.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            List<CatalogItem> resolved = new(request.Lines.Count);
            List<string> unknown = [];

            for (int index = 0; index < request.Lines.Count; index++)
            {
                ExportLineDto line = request.Lines[index];
                CatalogItem? item = catalog.Find(line.Sku);
                if (item is null)
                {
                    unknown.Add($"{index + 1}. satır: {line.Sku}");
                    continue;
                }

                resolved.Add(item);
            }

            if (unknown.Count > 0)
            {
                return Results.Problem(
                    title: "Bilinmeyen stok kodu.",
                    detail: $"Şu satırlardaki stok kodları katalogda yok: {string.Join(", ", unknown)}.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            ExportedFile file = exporter.Export(request, resolved);
            logger.LogInformation("Excel üretildi: {LineCount} satır, {ByteCount} bayt.", request.Lines.Count, file.Content.Length);

            return Results.File(file.Content, file.ContentType, file.FileName);
        })
.WithName("ExportOrder");
    }
}