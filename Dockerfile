# syntax=docker/dockerfile:1

# Base-images er pinnet til digest (uforanderlige). Tagget står med for læsbarhed,
# og så Dependabot kan finde og opdatere dem.

# ---------- Build: restore + publish ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS build
WORKDIR /src

# Kun projektfiler først, så restore-laget caches, indtil afhængighederne ændres
COPY .config/dotnet-tools.json .config/
COPY ["Risk.Api/Risk.Api.csproj", "Risk.Api/"]
COPY ["Risk.Application/Risk.Application.csproj", "Risk.Application/"]
COPY ["Risk.Domain/Risk.Domain.csproj", "Risk.Domain/"]
COPY ["Risk.Infrastructure/Risk.Infrastructure.csproj", "Risk.Infrastructure/"]
RUN dotnet tool restore && dotnet restore "Risk.Api/Risk.Api.csproj"

COPY . .
RUN dotnet publish "Risk.Api/Risk.Api.csproj" -c Release --no-restore -o /app/publish /p:UseAppHost=false

# ---------- Migration bundle ----------
FROM build AS bundle
# Bundlen er self-contained og skal derfor have restore-assets for linux-x64
RUN dotnet restore "Risk.Api/Risk.Api.csproj" --runtime linux-x64
RUN dotnet ef migrations bundle \
      --project Risk.Infrastructure/Risk.Infrastructure.csproj \
      --startup-project Risk.Api/Risk.Api.csproj \
      --configuration Release \
      --runtime linux-x64 \
      --self-contained \
      --output /app/efbundle

# ---------- Migrator (engangs-container) ----------
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled-extra@sha256:b9f2fd17cfd23c997453e83adeaf2f2d20561f9754eb3ed5dbc97bd06867c377 AS migrator
WORKDIR /app
# Single-file bundles udpakker til $HOME som standard. /tmp er den eneste
# skrivbare sti, når containeren kører read-only.
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp
COPY --from=bundle /app/efbundle ./efbundle
USER $APP_UID
ENTRYPOINT ["./efbundle"]

# ---------- API (standard-target, skal stå sidst) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:3ed6558056b54d5c9dfefb7ff473728e022ff2bf1d976cbf8ee07b05ec594374 AS api
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Risk.Api.dll"]
