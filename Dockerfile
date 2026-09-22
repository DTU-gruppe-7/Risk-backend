FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Kopiér projektfiler for at udnytte Docker layer caching
COPY ["Risk.Api/Risk.Api.csproj", "Risk.Api/"]
COPY ["Risk.Application/Risk.Application.csproj", "Risk.Application/"]
COPY ["Risk.Domain/Risk.Domain.csproj", "Risk.Domain/"]
COPY ["Risk.Infrastructure/Risk.Infrastructure.csproj", "Risk.Infrastructure/"]

RUN dotnet restore "Risk.Api/Risk.Api.csproj"

# Kopiér resten og byg release
COPY . .
WORKDIR "/src/Risk.Api"
RUN dotnet publish "Risk.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime container
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "Risk.Api.dll"]