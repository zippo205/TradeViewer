# TradeViewer

The website of a self-hosted setup for discretionary market analysis: charts with your own
drawings and with the analysis of [EWTS](../EWTS), and the user interface for operating EWTS
and the [Downloader](../Downloader). Runs on a Raspberry Pi 5, in Docker or on Linux.

Status: design phase. The solution does not exist yet; the commands below describe the target.

## Documentation

Every piece of information has one home:

| Document | Content |
|----------|---------|
| README.md | setup and commands |
| AGENTS.md | rules for coding agents |
| docs/specification.md | what TradeViewer does: requirements with ids |
| docs/architecture.md | how it is built: layers, data flow, interfaces, configuration |
| docs/decisions/ | why: one record per decision |
| docs/backlog.md | the epics, their order and the requirements they deliver |
| docs/roadmap.md | releases and the status of the epics |
| docs/plans/ | the implementation plan of each epic |
| docs/knowledge/ | the interfaces of the Downloader and EWTS as TradeViewer uses them |

## Prerequisites

- .NET SDK 10.0.403 (pinned by `global.json`), installed in `~/.dotnet`:

  ```bash
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
  ```

- Node.js 20 or later, at build time only (chart module)
- The Downloader's output root (or a frozen copy of it) readable on this machine; its control
  API is needed only when `MarketData:Downloader:Enabled` is true

## Build, Test, Run

```bash
dotnet build TradeViewer.sln
dotnet test TradeViewer.sln
dotnet test TradeViewer.sln --filter "FullyQualifiedName~<TestName>"   # a single test
npm test --prefix src/TradeViewer.Chart                                 # chart module tests
dotnet format TradeViewer.sln                                           # rules in .editorconfig
dotnet run --project src/TradeViewer.Api --launch-profile http          # http://localhost:8080
```

The first build also runs `npm ci` and bundles the chart module. Add `-p:SkipChartBuild=true`
when only C# changed.

## Configuration

The settings and their defaults are listed in docs/architecture.md, chapter Configuration.
Development values are in `src/TradeViewer.Api/appsettings.Development.json`. Two settings are
required:

```bash
dotnet run --project src/TradeViewer.Api -- --MarketData:RootPath=/home/admin/marketdata --Storage:DataPath=/home/admin/tradeviewer/data
MarketData__RootPath=/srv/marketdata Storage__DataPath=/srv/tradeviewer dotnet run --project src/TradeViewer.Api
```

## Docker

```bash
docker compose -f deploy/docker-compose.yml up -d --build
docker buildx build --platform linux/arm64,linux/amd64 -f deploy/Dockerfile -t tradeviewer .
```
