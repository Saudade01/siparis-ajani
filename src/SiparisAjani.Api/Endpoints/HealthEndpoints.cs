using System.Reflection;

using SiparisAjani.Contracts;

using SiparisAjani.Core.Abstractions;

namespace SiparisAjani.Api.Endpoints;

/// <summary>Web arayüzünün sunucuya ulaşıp ulaşmadığını gösteren uç nokta.</summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Health, (ICatalogStore catalog, IOrderExtractor extractor) =>
            Results.Ok(new HealthDto("ok", extractor.Kind, catalog.Count, Version)))
.WithName("GetHealth");
    }

    private static string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
}