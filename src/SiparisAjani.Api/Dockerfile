# syntax=docker/dockerfile:1
# SiparisAjani.Api - çok aşamalı derleme (build context: repo kökü)
# Örnek kullanım: docker build -f src/SiparisAjani.Api/Dockerfile -t siparisajani-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyalarını kopyala; katman önbelleği restore adımını hızlandırır.
COPY Directory.Build.props global.json ./
COPY src/SiparisAjani.Contracts/SiparisAjani.Contracts.csproj src/SiparisAjani.Contracts/
COPY src/SiparisAjani.Core/SiparisAjani.Core.csproj src/SiparisAjani.Core/
COPY src/SiparisAjani.Infrastructure/SiparisAjani.Infrastructure.csproj src/SiparisAjani.Infrastructure/
COPY src/SiparisAjani.Api/SiparisAjani.Api.csproj src/SiparisAjani.Api/
RUN dotnet restore src/SiparisAjani.Api/SiparisAjani.Api.csproj

COPY src/SiparisAjani.Contracts/ src/SiparisAjani.Contracts/
COPY src/SiparisAjani.Core/ src/SiparisAjani.Core/
COPY src/SiparisAjani.Infrastructure/ src/SiparisAjani.Infrastructure/
COPY src/SiparisAjani.Api/ src/SiparisAjani.Api/

RUN dotnet publish src/SiparisAjani.Api/SiparisAjani.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Sağlık kontrolü curl ile yapılır; aspnet çalışma zamanı imajında hazır gelmez.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# .NET 10 çalışma zamanı imajı 1000/1000 kullanıcı-grup çiftini zaten tanımlıyor
# olabileceğinden kendi kullanıcımızı oluşturmuyoruz; imajın hazır $APP_UID kullanıcısını kullanırız.
COPY --from=build /app/publish .
# Sentetik katalog verisi imaja gömülür (data/ yalnızca sentetik veri içerir).
COPY data/katalog/ ./data/katalog/

RUN mkdir -p /app/App_Data \
    && chown -R $APP_UID:$APP_UID /app

USER $APP_UID

ENV ASPNETCORE_URLS=http://+:5080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Catalog__SeedPath=/app/data/katalog/hirdavat-ornek.csv \
    Storage__AliasPath=/app/App_Data/aliases.json

VOLUME ["/app/App_Data"]

EXPOSE 5080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS http://localhost:5080/api/health || exit 1

ENTRYPOINT ["dotnet", "SiparisAjani.Api.dll"]
