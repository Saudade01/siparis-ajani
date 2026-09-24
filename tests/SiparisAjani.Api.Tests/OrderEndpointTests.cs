using System.Net;
using System.Net.Http.Json;

using SiparisAjani.Contracts;

namespace SiparisAjani.Api.Tests;

public class OrderEndpointTests(SiparisAjaniApiFactory factory) : IClassFixture<SiparisAjaniApiFactory>
{
    private const string SampleOrder =
        "Merhaba Hasan abi, 5 kutu 4x40 sunta vidası, 2 koli şeffaf silikon. Tel: 0500 000 00 01";

    [Fact]
    public async Task Parse_SiparisiCozumler()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            ApiRoutes.ParseOrder,
            new ParseOrderRequest(SampleOrder));
        OrderDraftDto? draft = await response.Content.ReadFromJsonAsync<OrderDraftDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(draft);
        Assert.Equal(ExtractorKind.RuleBased, draft.Extractor);
        Assert.Equal(2, draft.Lines.Count);
        Assert.Equal("VD-4040-SNT", draft.Lines[0].BestMatch!.Sku);
        Assert.Equal(LineStatus.Matched, draft.Lines[0].Status);
    }

    [Fact]
    public async Task Parse_KisiselVeriyiYanittaDondurmez()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            ApiRoutes.ParseOrder,
            new ParseOrderRequest(SampleOrder));
        string body = await response.Content.ReadAsStringAsync();
        OrderDraftDto? draft = await response.Content.ReadFromJsonAsync<OrderDraftDto>();

        Assert.DoesNotContain("0500", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Hasan", body, StringComparison.Ordinal);
        Assert.Contains("phone", draft!.MaskedFields);
    }

    [Fact]
    public async Task Parse_EnumlariMetinOlarakDoner()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            ApiRoutes.ParseOrder,
            new ParseOrderRequest(SampleOrder, OrderSource.Email));
        string body = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"Matched\"", body, StringComparison.Ordinal);
        Assert.Contains("\"RuleBased\"", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Parse_BosMetinde400Doner(string text)
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(ApiRoutes.ParseOrder, new ParseOrderRequest(text));
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(400, problem!.Status);
    }

    [Fact]
    public async Task Parse_YirmiBinKarakterUstunde413Doner()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            ApiRoutes.ParseOrder,
            new ParseOrderRequest(new string('a', 20_001)));
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(413, problem!.Status);
    }

    [Fact]
    public async Task Parse_TamYirmiBinKarakteriKabulEder()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            ApiRoutes.ParseOrder,
            new ParseOrderRequest(new string('a', 20_000)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Corrections_DuzeltmeyiOgrenir()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage saved = await client.PostAsJsonAsync(
            ApiRoutes.Corrections,
            new CorrectionRequest("japon yapıştırıcı", "YP-HZL-20"));

        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);

        OrderDraftDto? draft = await (await client.PostAsJsonAsync(
            ApiRoutes.ParseOrder,
            new ParseOrderRequest("3 tane japon yapıştırıcı"))).Content.ReadFromJsonAsync<OrderDraftDto>();

        Assert.Equal(LineStatus.Matched, draft!.Lines[0].Status);
        Assert.Equal("YP-HZL-20", draft.Lines[0].BestMatch!.Sku);
    }

    [Fact]
    public async Task Corrections_BilinmeyenStokKodunda400Doner()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            ApiRoutes.Corrections,
            new CorrectionRequest("bir şey", "YOK-999"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Corrections_EksikAlanda400Doner()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            ApiRoutes.Corrections,
            new CorrectionRequest("  ", "VD-4040-SNT"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Export_ExcelDosyasiDoner()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            ApiRoutes.ExportOrder,
            new ExportOrderRequest([new ExportLineDto("VD-4040-SNT", 5m, "kutu")], "CARI-001"));
        byte[] content = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType!.MediaType);

        string? fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        Assert.NotNull(fileName);
        Assert.StartsWith("siparis-", fileName, StringComparison.Ordinal);
        Assert.EndsWith(".xlsx", fileName, StringComparison.Ordinal);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);

        // PK imzası: gerçek bir xlsx (zip) paketi.
        Assert.True(content.Length > 0);
        Assert.Equal((byte)'P', content[0]);
        Assert.Equal((byte)'K', content[1]);
    }

    [Fact]
    public async Task Export_BilinmeyenStokKodunda400DonerVeSatiriSoyler()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            ApiRoutes.ExportOrder,
            new ExportOrderRequest(
            [
                new ExportLineDto("VD-4040-SNT", 1m, "kutu"),
                new ExportLineDto("YOK-123", 2m, "adet")
            ]));
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(400, problem!.Status);
        Assert.Contains("2. satır", problem.Detail, StringComparison.Ordinal);
        Assert.Contains("YOK-123", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_SatirYoksa400Doner()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            ApiRoutes.ExportOrder,
            new ExportOrderRequest([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}