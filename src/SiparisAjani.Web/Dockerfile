# syntax=docker/dockerfile:1
# SiparisAjani.Web - çok aşamalı derleme (build context: repo kökü)
# Örnek kullanım: docker build -f src/SiparisAjani.Web/Dockerfile -t siparisajani-web .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props global.json ./
COPY src/SiparisAjani.Contracts/SiparisAjani.Contracts.csproj src/SiparisAjani.Contracts/
COPY src/SiparisAjani.Web/SiparisAjani.Web.csproj src/SiparisAjani.Web/
RUN dotnet restore src/SiparisAjani.Web/SiparisAjani.Web.csproj

COPY src/SiparisAjani.Contracts/ src/SiparisAjani.Contracts/
COPY src/SiparisAjani.Web/ src/SiparisAjani.Web/

RUN dotnet publish src/SiparisAjani.Web/SiparisAjani.Web.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# .NET 10 çalışma zamanı imajı 1000/1000 kullanıcı-grup çiftini zaten tanımlıyor
# olabileceğinden kendi kullanıcımızı oluşturmuyoruz; imajın hazır $APP_UID kullanıcısını kullanırız.
COPY --from=build /app/publish .

# Şu an kullanılmıyor; ileride oturum/ön bellek dosyaları için ayrılmış (API ile tutarlılık).
RUN mkdir -p /app/App_Data \
    && chown -R $APP_UID:$APP_UID /app

USER $APP_UID

ENV ASPNETCORE_URLS=http://+:5090 \
    ASPNETCORE_ENVIRONMENT=Production \
    Api__BaseUrl=http://api:5080

VOLUME ["/app/App_Data"]

EXPOSE 5090

ENTRYPOINT ["dotnet", "SiparisAjani.Web.dll"]
