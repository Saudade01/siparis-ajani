using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Models;
using SiparisAjani.Infrastructure.Persistence;
using SiparisAjani.Infrastructure.Persistence.Entities;

namespace SiparisAjani.Infrastructure.Storage;

/// <summary>
/// Çözümleme geçmişini SQLite'a yazar. Yazılan her şey ölçümdür: metin, müşteri bilgisi
/// ve maskelenen değerler kaydedilmez (<see cref="OrderDraftSummary"/>).
/// </summary>
public sealed class EfOrderDraftLog(
    IDbContextFactory<OrderAgentDbContext> contextFactory,
    ILogger<EfOrderDraftLog> logger) : IOrderDraftLog
{
    public async Task RecordAsync(OrderDraftSummary summary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);

        try
        {
            await using OrderAgentDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

            OrderDraftEntity draft = new()
            {
                DraftId = summary.DraftId,
                CreatedAt = summary.CreatedAt,
                Source = summary.Source,
                Extractor = summary.Extractor,
                LineCount = summary.LineCount,
                MatchedCount = summary.MatchedCount,
                AmbiguousCount = summary.AmbiguousCount,
                NotFoundCount = summary.NotFoundCount,
                WarningCount = summary.WarningCount,
                DurationMs = summary.DurationMs,
                CharacterCount = summary.CharacterCount,
                MaskedFields = [.. summary.MaskedFields]
            };

            foreach (OrderDraftLineSummary line in summary.Lines)
            {
                draft.Lines.Add(new OrderDraftLineEntity
                {
                    LineNo = line.LineNo,
                    Status = line.Status,
                    MatchedSku = line.MatchedSku,
                    Confidence = line.Confidence,
                    AlternativeCount = line.AlternativeCount,
                    Quantity = line.Quantity
                });
            }

            context.OrderDrafts.Add(draft);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or SqliteException)
        {
            // Geçmiş kaydı bir yan etkidir: yazılamazsa çözümleme isteği yine de başarılı olmalı.
            logger.LogWarning("Taslak özeti kaydedilemedi: {Reason}", exception.GetType().Name);
        }
    }

    public async Task<IReadOnlyList<OrderDraftSummary>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        int take = Math.Clamp(limit, 1, 100);

        await using OrderAgentDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        List<OrderDraftEntity> drafts = await context.OrderDrafts
.AsNoTracking()
.Include(draft => draft.Lines)
.OrderByDescending(draft => draft.CreatedAt)
.ThenByDescending(draft => draft.Id)
.Take(take)
.ToListAsync(cancellationToken);

        return [.. drafts.Select(ToSummary)];
    }

    private static OrderDraftSummary ToSummary(OrderDraftEntity draft)
    {
        return new OrderDraftSummary(
            draft.DraftId,
            draft.CreatedAt,
            draft.Source,
            draft.Extractor,
            draft.LineCount,
            draft.MatchedCount,
            draft.AmbiguousCount,
            draft.NotFoundCount,
            draft.WarningCount,
            draft.DurationMs,
            draft.CharacterCount,
            [.. draft.MaskedFields],
            [.. draft.Lines
.OrderBy(line => line.LineNo)
.Select(line => new OrderDraftLineSummary(
                    line.LineNo,
                    line.Status,
                    line.MatchedSku,
                    line.Confidence,
                    line.AlternativeCount,
                    line.Quantity))]);
    }
}