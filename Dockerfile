# syntax=docker/dockerfile:1

# Base-images er pinnet til digest (uforanderlige). Tagget står med for læsbarhed,
# og så Dependabot kan finde og opdatere dem.

# ---------- Build: restore + publish ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:8c6edfb26c85153f236d26efebd9ad349aec403583f343f201ee67838b23658d AS build
WORKDIR /src

# Kun projektfiler og lockfiler først, så restore-laget caches, indtil afhængighederne ændres
COPY .config/dotnet-tools.json .config/
COPY Directory.Build.props ./
COPY ["Risk.Api/Risk.Api.csproj", "Risk.Api/packages.lock.json", "Risk.Api/"]
COPY ["Risk.Application/Risk.Application.csproj", "Risk.Application/packages.lock.json", "Risk.Application/"]
COPY ["Risk.Domain/Risk.Domain.csproj", "Risk.Domain/packages.lock.json", "Risk.Domain/"]
COPY ["Risk.Infrastructure/Risk.Infrastructure.csproj", "Risk.Infrastructure/packages.lock.json", "Risk.Infrastructure/"]
# --locked-mode: fejler, hvis en pakke ikke passer med lockfilen, i stedet for stille at hente en anden version
RUN dotnet tool restore && dotnet restore "Risk.Api/Risk.Api.csproj" --locked-mode

COPY . .
RUN dotnet publish "Risk.Api/Risk.Api.csproj" -c Release --no-restore -o /app/publish /p:UseAppHost=false

# ---------- Migration bundle ----------
FROM build AS bundle
# Bundlen er self-contained og skal derfor have restore-assets for linux-x64
RUN dotnet restore "Risk.Api/Risk.Api.csproj" --runtime linux-x64 --locked-mode
RUN dotnet ef migrations bundle \
      --project Risk.Infrastructure/Risk.Infrastructure.csproj \
      --startup-project Risk.Api/Risk.Api.csproj \
      --configuration Release \
      --runtime linux-x64 \
      --self-contained \
      --output /app/efbundle

# ---------- Migrator (engangs-container) ----------
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled-extra@sha256:1c22aec177579fd929b21a7f2cd520f459f33ff588556890a168bc8acffa4fdf AS migrator
WORKDIR /app
# Single-file bundles udpakker til $HOME som standard. /tmp er den eneste
# skrivbare sti, når containeren kører read-only.
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp
COPY --from=bundle /app/efbundle ./efbundle
USER $APP_UID
ENTRYPOINT ["./efbundle"]

# ---------- API (standard-target, skal stå sidst) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:c8dd08a50d8ffd19bd8da72edee05a7ac15b486a5b1869cf46fdb370bf1fbc76 AS api
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Risk.Api.dll"]
