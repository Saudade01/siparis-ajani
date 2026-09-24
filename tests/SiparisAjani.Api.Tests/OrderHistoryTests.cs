using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SiparisAjani.Contracts;

using SiparisAjani.Core.Abstractions;
using SiparisAjani.Core.Models;
using SiparisAjani.Infrastructure.Persistence;
using SiparisAjani.Infrastructure.Persistence.Entities;

namespace SiparisAjani.Api.Tests;

/// <summary>
/// Çözümleme geçmişinin uçtan uca kaydı. Ayrı bir fabrika kullanılıyor: bu sınıfın
/// veritabanına yalnızca kendi istekleri yazsın.
/// </summary>
public class OrderHistoryTests(SiparisAjaniApiFactory factory) : IClassFixture<SiparisAjaniApiFactory>
{
    private const string SampleOrder =
        "Merhaba Hasan abi, 5 kutu 4x40 sunta vidası, 2 koli şeffaf silikon. Tel: 0500 000 00 01";

    [Fact]
    public async Task Parse_CozumlemeyiVeritabaninaYazar()
    {
        using HttpClient client = factory.CreateClient();

        OrderDraftDto? draft = await (await client.PostAsJsonAsync(
            ApiRoutes.ParseOrder,
            new ParseOrderRequest(SampleOrder, OrderSource.Email))).Content.ReadFromJsonAsync<OrderDraftDto>();

        await using OrderAgentDbContext context = await CreateContextAsync();
        OrderDraftEntity saved = await context.OrderDrafts
          .Include(entity => entity.Lines)
          .SingleAsync(entity => entity.DraftId == draft!.DraftId);

        Assert.Equal(OrderSource.Email, saved.Source);
        Assert.Equal(ExtractorKind.RuleBased, saved.Extractor);
        Assert.Equal(2, saved.LineCount);
        Assert.Equal(saved.LineCount, saved.MatchedCount + saved.AmbiguousCount + saved.NotFoundCount);
        Assert.Equal(SampleOrder.Length, saved.CharacterCount);
        Assert.Contains("phone", saved.MaskedFields);
        Assert.True(saved.DurationMs >= 0);
        Assert.Equal("VD-4040-SNT", saved.Lines.Single(line => line.LineNo == 1).MatchedSku);
    }

    [Fact]
    public async Task Parse_VeritabaninaSiparisMetniVeKisiselVeriYazmaz()
    {
        using HttpClient client = factory.CreateClient();

        await client.PostAsJsonAsync(ApiRoutes.ParseOrder, new ParseOrderRequest(SampleOrder));

        await using OrderAgentDbContext context = await CreateContextAsync();
        List<OrderDraftEntity> drafts = await context.OrderDrafts.Include(entity => entity.Lines).ToListAsync();

        string dump = string.Join(
            " ",
            drafts.SelectMany(draft => new[]
            {
                draft.DraftId,
                draft.Source.ToString(),
                draft.Extractor.ToString(),
                string.Join(",", draft.MaskedFields),
                string.Join(",", draft.Lines.Select(line => $"{line.Status}|{line.MatchedSku}"))
            }));

        // KVKK kararı: geçmişte yalnızca ölçüm vardır; metin, isim ve telefon yoktur.
        Assert.DoesNotContain("Hasan", dump, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0500", dump, StringComparison.Ordinal);
        Assert.DoesNotContain("sunta vidası", dump, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("silikon", dump, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRecent_SonCozumlemeleriEnYenidenEskiyeDoner()
    {
        using HttpClient client = factory.CreateClient();

        await client.PostAsJsonAsync(ApiRoutes.ParseOrder, new ParseOrderRequest("4 adet menteşe"));
        await client.PostAsJsonAsync(ApiRoutes.ParseOrder, new ParseOrderRequest("2 kutu vida"));

        IOrderDraftLog log = factory.Services.GetRequiredService<IOrderDraftLog>();
        IReadOnlyList<OrderDraftSummary> recent = await log.GetRecentAsync(20);

        Assert.True(recent.Count >= 2);
        Assert.True(recent[0].CreatedAt >= recent[1].CreatedAt);
    }

    private async Task<OrderAgentDbContext> CreateContextAsync()
    {
        IDbContextFactory<OrderAgentDbContext> contextFactory =
            factory.Services.GetRequiredService<IDbContextFactory<OrderAgentDbContext>>();

        return await contextFactory.CreateDbContextAsync();
    }
}