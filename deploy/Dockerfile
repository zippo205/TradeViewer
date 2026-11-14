# TradeViewer image for linux/arm64 (Raspberry Pi 5) and linux/amd64.
# Build from the repository root:
#   docker buildx build --platform linux/arm64,linux/amd64 -f deploy/Dockerfile -t tradeviewer .

# 1) Chart module (TypeScript). Runs on the build machine; the bundle is plain JavaScript and portable.
FROM --platform=$BUILDPLATFORM node:24-slim AS chart
WORKDIR /src/src/TradeViewer.Chart
COPY src/TradeViewer.Chart/package.json src/TradeViewer.Chart/package-lock.json ./
RUN npm ci
COPY src/TradeViewer.Chart/ ./
# build.mjs writes to ../TradeViewer.Web/wwwroot/chart/
RUN CHART_CONFIGURATION=Release npm run build

# 2) .NET publish on the build machine. Framework-dependent and portable (no runtime identifier):
#    the same output runs on arm64 and amd64.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/TradeViewer.Api/TradeViewer.Api.csproj src/TradeViewer.Api/
COPY src/TradeViewer.Application/TradeViewer.Application.csproj src/TradeViewer.Application/
COPY src/TradeViewer.Domain/TradeViewer.Domain.csproj src/TradeViewer.Domain/
COPY src/TradeViewer.Infrastructure/TradeViewer.Infrastructure.csproj src/TradeViewer.Infrastructure/
COPY src/TradeViewer.Contracts/TradeViewer.Contracts.csproj src/TradeViewer.Contracts/
COPY src/TradeViewer.Web/TradeViewer.Web.csproj src/TradeViewer.Web/
RUN dotnet restore src/TradeViewer.Api/TradeViewer.Api.csproj
COPY src/ src/
COPY --from=chart /src/src/TradeViewer.Web/wwwroot/chart/ src/TradeViewer.Web/wwwroot/chart/
RUN dotnet publish src/TradeViewer.Api/TradeViewer.Api.csproj \
        -c Release --no-restore -p:UseAppHost=false -p:SkipChartBuild=true -o /app

# 3) Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl for the health check only
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# /data/marketdata is intentionally NOT created here and NOT declared as VOLUME: without the mounted
# output root of the Downloader the service must stop at startup (a missing mount), not run on nothing.
RUN mkdir -p /data/app && chown -R app:app /data/app

COPY --from=build /app ./

ENV ASPNETCORE_HTTP_PORTS=8080 \
    MarketData__RootPath=/data/marketdata \
    Storage__DataPath=/data/app

EXPOSE 8080
USER app

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "TradeViewer.Api.dll"]
