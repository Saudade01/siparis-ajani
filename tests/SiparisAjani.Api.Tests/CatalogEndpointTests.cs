using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using SiparisAjani.Contracts;

namespace SiparisAjani.Api.Tests;

public class CatalogEndpointTests(SiparisAjaniApiFactory factory) : IClassFixture<SiparisAjaniApiFactory>
{
    [Fact]
    public async Task Catalog_BosSorgudaLimitKadarDoner()
    {
        using HttpClient client = factory.CreateClient();

        List<CatalogItemDto>? items = await client.GetFromJsonAsync<List<CatalogItemDto>>($"{ApiRoutes.Catalog}?limit=5");

        Assert.NotNull(items);
        Assert.Equal(5, items.Count);
    }

    [Fact]
    public async Task Catalog_AramaSonucuDoner()
    {
        using HttpClient client = factory.CreateClient();

        List<CatalogItemDto>? items =
            await client.GetFromJsonAsync<List<CatalogItemDto>>($"{ApiRoutes.Catalog}?q=sunta%20vidasi&limit=20");

        Assert.NotNull(items);
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Contains("Sunta", item.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Catalog_LimitiYuzIleSinirlar()
    {
        using HttpClient client = factory.CreateClient();

        List<CatalogItemDto>? items = await client.GetFromJsonAsync<List<CatalogItemDto>>($"{ApiRoutes.Catalog}?limit=5000");

        Assert.NotNull(items);
        Assert.True(items.Count <= 100);
    }

    [Fact]
    public async Task CatalogImport_GecerliCsvYukler()
    {
        using HttpClient client = factory.CreateClient();
        using MultipartFormDataContent form = CreateCsv("sku,name,unit,barcode,category\nT-1,Test Vida 4x40,kutu,869,vida");

        HttpResponseMessage response = await client.PostAsync(ApiRoutes.CatalogImport, form);
        CatalogImportResultDto? result = await response.Content.ReadFromJsonAsync<CatalogImportResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(1, result.Imported);
        Assert.Equal(0, result.Skipped);

        // Katalogu tekrar örnek veriye çevir ki diğer testler etkilenmesin.
        using MultipartFormDataContent restore = CreateCsv(
            await File.ReadAllTextAsync(Path.Combine(FindRoot(), "data", "katalog", "hirdavat-ornek.csv")));
        await client.PostAsync(ApiRoutes.CatalogImport, restore);
    }

    [Fact]
    public async Task CatalogImport_DosyaYoksa400Doner()
    {
        using HttpClient client = factory.CreateClient();
        using MultipartFormDataContent form = [];

        HttpResponseMessage response = await client.PostAsync(ApiRoutes.CatalogImport, form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        Assert.Equal(400, problem!.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
    }

    [Fact]
    public async Task CatalogImport_BozukCsvde400DonerVeKatalogKorunur()
    {
        using HttpClient client = factory.CreateClient();
        using MultipartFormDataContent form = CreateCsv("kod,ad\nA-1,Vida");

        HttpResponseMessage response = await client.PostAsync(ApiRoutes.CatalogImport, form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        HealthDto? health = await client.GetFromJsonAsync<HealthDto>(ApiRoutes.Health);
        Assert.True(health!.CatalogCount > 0, "Bozuk dosya katalogu boşaltmamalı.");
    }

    private static MultipartFormDataContent CreateCsv(string content)
    {
        ByteArrayContent file = new(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { file, "file", "katalog.csv" } };
    }

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "data", "katalog")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("data/ bulunamadı.");
    }
}