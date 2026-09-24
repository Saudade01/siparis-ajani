using System.Net;
using System.Net.Http.Json;

using SiparisAjani.Contracts;

namespace SiparisAjani.Api.Tests;

public class HealthEndpointTests(SiparisAjaniApiFactory factory) : IClassFixture<SiparisAjaniApiFactory>
{
    [Fact]
    public async Task Health_AlanlariDoldurur()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(ApiRoutes.Health);
        HealthDto? health = await response.Content.ReadFromJsonAsync<HealthDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(health);
        Assert.Equal("ok", health.Status);
        Assert.Equal(ExtractorKind.RuleBased, health.Extractor);
        Assert.True(health.CatalogCount > 0, "Başlangıç kataloğu yüklenmedi.");
        Assert.False(string.IsNullOrWhiteSpace(health.Version));
    }

    [Fact]
    public async Task Health_AnahtarsizModdaKuralTabanliCalisir()
    {
        using HttpClient client = factory.CreateClient();

        HealthDto? health = await client.GetFromJsonAsync<HealthDto>(ApiRoutes.Health);

        Assert.Equal(ExtractorKind.RuleBased, health!.Extractor);
    }

    [Fact]
    public async Task BilinmeyenRota_404Doner()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/yok");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}