# TradeViewer - Architecture

This document says **how** TradeViewer is built. What it does is in docs/specification.md
(requirement ids like AS1 refer to it); why a choice was made is in docs/decisions/.

---

## 1. Principles

- Clean Architecture, custom CQRS, feature folders, dependency injection
- C# everywhere; TypeScript only in `TradeViewer.Chart`
- Business logic is deterministic and testable without a database, a file system or a network
- TradeViewer renders and operates; it never analyses (decision 0004)

Not used: DDD, aggregates, bounded contexts, domain events, MediatR or another mediator,
FluentValidation, ORMs, migration frameworks.

---

## 2. System Overview

```text
                      Browser (any device in the home network)
   ┌───────────────────────────────────────────────────────────────┐
   │ TradeViewer.Web  (Blazor WebAssembly, C#)                     │
   │   pages, panels, dialogs, client state                        │
   │        │ JS interop (commands down, events up)                │
   │        ▼                                                      │
   │ TradeViewer.Chart  (TypeScript, Lightweight Charts)           │
   │   candles, indicators, drawings, overlays                     │
   └────────┬───────────────────────────────┬──────────────────────┘
            │ REST (JSON)                   │ SignalR (server to client)
   ┌────────▼───────────────────────────────▼──────────────────────┐
   │ TradeViewer.Api  (ASP.NET Core)                               │
   │   endpoints, hub, static files of Web, composition root       │
   ├───────────────────────────────────────────────────────────────┤
   │ Application  →  Domain                                        │
   │ Infrastructure: SQLite, CSV import, Downloader client, EWTS   │
   │                 client, scenario files, background services   │
   └───┬──────────────┬──────────────┬──────────────┬──────────────┘
       │ read only    │              │ HTTP         │ HTTP
   CSV files      marketdata.db   Downloader     EWTS instance(s)
   (Downloader    workspace.db    control API    API
    output root)
   scenario files
   (EWTS folder)
```

One process, one container. The browser calls only TradeViewer.Api; it never calls the
Downloader or an EWTS instance directly. That keeps one origin, one place for error handling
and one place that knows the addresses.

---

## 3. Solution Structure

```text
src/
├── TradeViewer.Api              ASP.NET Core host
├── TradeViewer.Application      features (queries, commands), ports
├── TradeViewer.Domain           models and calculations
├── TradeViewer.Infrastructure   SQLite, CSV, HTTP clients, background services
├── TradeViewer.Contracts        request and response records shared by Api and Web
├── TradeViewer.Web              Blazor WebAssembly client
└── TradeViewer.Chart            TypeScript chart module, built into Web/wwwroot/chart

tests/
├── TradeViewer.UnitTests
├── TradeViewer.IntegrationTests
├── TradeViewer.ArchitectureTests
└── fixtures/                    small CSV slices in the Downloader's layout, recorded responses

deploy/
├── Dockerfile
└── docker-compose.yml
```

`Directory.Build.props` sets the build settings of all C# projects: `net10.0`, nullable,
implicit usings, warnings as errors, `LangVersion` 12. `global.json` pins the SDK.

### Layers

```text
Api             endpoints (Minimal API, one class per feature), SignalR hub, configuration
                binding, registration of all services. No logic, no SQL, no file access.
Application     one folder per feature with its queries, commands, handlers and validators;
                the ports (interfaces) for everything outside the process.
Domain          models and pure calculations. No project or package references; no clock,
                no I/O, no randomness: time and data come in as parameters.
Infrastructure  implementations of the ports: SQLite stores, CSV reader, Downloader client,
                EWTS client, backup; the background services.
Contracts       plain records, no logic.
Web             all UI apart from the chart canvas; client state; typed API clients; the
                SignalR client; `ChartHost`, the C# wrapper of the chart module.
                No calculations: every number shown comes from the Api.
Chart           rendering and pointer interaction only.
```

### Dependency Rules

```text
Api            -> Application, Contracts, Infrastructure (registration only)
Application    -> Domain, Contracts
Infrastructure -> Application, Domain
Web            -> Contracts
Domain         -> nothing
Contracts      -> nothing
```

Everything not listed is forbidden. `TradeViewer.ArchitectureTests` (NetArchTest) enforces the
rules.

---

## 4. Application Layer

### Features

```text
Features/
├── Assets            add, change, remove, lookup (AS1-AS3)
├── MarketData        candles, import, sync status, gaps (MD1-MD5)
├── Watchlists        (AS4, AS5)
├── Layouts           (LY1-LY3)
├── Indicators        calculation requests, indicator templates (CH5, CH6)
├── Drawings          drawings, Fibonacci presets (DR1-DR6)
├── Downloader        status and subscriptions for the monitoring page (DL1-DL4)
├── EwtsInstances     registration, status, observed assets, overview, details (EW1-EW6)
├── Overlays          (OV1-OV5)
├── TestRuns          (BT1-BT6)
├── Alerts            (AL1-AL4)
├── Scenarios         list and read scenario drawing files (SC1-SC9)
└── Workspace         backup and restore (WS3)
```

A feature folder contains only the subfolders it needs:

```text
Watchlists/
├── Commands        RenameWatchlistCommand
├── Queries         GetWatchlistsQuery
├── Handlers        RenameWatchlistCommandHandler, GetWatchlistsQueryHandler
├── Validators      RenameWatchlistCommandValidator
└── Services        helpers used by several handlers of the feature
```

Naming: `<Verb><Noun>Command`, `<Verb><Noun>Query`, the handler and the validator carry the
full name of their message plus `Handler` / `Validator`. One type per file. The acronym is
written `Ewts` in identifiers (`EwtsInstance`, `IEwtsClient`).

A feature may use `Assets` and `MarketData`; apart from that, features do not reference each
other.

### CQRS

```text
IQuery<TResult>     IQueryHandler<TQuery, TResult>
ICommand<TResult>   ICommandHandler<TCommand, TResult>
```

in `TradeViewer.Application/Abstractions/Messaging`.

- Handlers are registered in dependency injection and injected directly into the endpoint
- No pipeline behaviours
- A query never changes state; every write is a command
- An endpoint maps the request to a message, calls the handler and maps the result; nothing else

### Results, Validation and Errors

Handlers return `Result<T>` and do not throw for expected failures. A validator is a plain
class; the handler calls it first.

```text
ErrorType      HTTP    used for
Validation     400     invalid input (validation problem details with the fields)
NotFound       404     unknown asset, layout, instance, ...
Conflict       409     e.g. asset already exists
InvalidData    422     a file or an upstream answer breaks its contract
Unavailable    503     the Downloader or an EWTS instance cannot be reached
```

All error responses are problem details (RFC 9457). An unexpected exception is logged and
answered with 500 without internal details.

### Ports

```text
Abstractions/
├── Messaging     the CQRS interfaces
├── MarketData    ICandleStore, ICandleFileReader, IDownloaderClient
├── Ewts          IEwtsClient, IScenarioFileReader
├── Workspace     one repository interface per feature: IAssetRepository, IWatchlistRepository,
│                 ILayoutRepository, IDrawingRepository, IEwtsInstanceRepository, ...
└── Realtime      IWorkspaceNotifier (implemented by the Api on top of the SignalR hub)
```

The clock is the built-in `TimeProvider`.

---

## 5. Domain Model

```text
Asset               source, symbol, display name, asset class, tick size, session,
                    source timeframes
AssetClass          Crypto, Stock, Etf, Forex
Session             Continuous, Exchange (time zone, open, close), NewYorkClose
Timeframe           M15, H1, H4, D1, W1 (later M1, M5, H12, MN1); codes 15m, 1h, 4h, 1d, 1w
Candle              open time (UTC), open, high, low, close, volume
CandleSeries        ordered candles of one asset and timeframe
TimeframeAggregator builds a timeframe from a lower one along the session (MD3)
CandleFileRules     file name -> period, row validation (see downloader-api.md)

Indicator           Sma, Ema, BollingerBands, Rsi, Macd, Atr: candles + parameters -> series
IndicatorInstance   indicator type, parameters, style
IndicatorTemplate   name, indicator instances

Watchlist           name, ordered assets
Layout              name, grid (1, 2, 4), panes, sync options
ChartPane           asset, timeframe, price scale mode, indicator instances, hidden groups

Drawing             id, asset, layout, tool type, anchors, style, text, payload version
Anchor              time (UTC) + price, never a bar index
FibonacciPreset     name, kind (retracement, extension), levels with colour and label

EwtsInstance        id, name, base address
```

- Prices and indicator values are `double`.
- Analysis results of EWTS (overlays, setups, test runs, alerts) are **not** domain models. They
  are contract records that pass through the Application layer unchanged (see 8).
- Drawings refer to market data by asset and time only, never by a row of the candle store, so
  rebuilding the store cannot orphan a drawing.

---

## 6. Market Data

Decision 0003. Interface of the Downloader: docs/knowledge/downloader-api.md.

### Flow

```text
add asset     lookup -> PUT subscription (client "tradeviewer") -> store asset -> first import
sync          MarketDataSyncService, every PollSeconds per asset:
                symbol status -> lastChange greater than stored?
                  yes: changes?after=n -> import the named files -> store lastChange
                       complete == false -> rescan the asset
                -> IWorkspaceNotifier.CandlesUpdated
rescan        compare every *.csv of the asset (size, modified time) with the import log and
              import what differs; runs at the first import, on complete == false, and once at
              startup for every asset
remove asset  DELETE subscription -> delete the asset's rows from the candle store
```

- There is no file watcher. The Downloader's change numbers are the only change signal; the
  rescan is the fallback.
- When the Downloader is unreachable the sync logs it once, keeps the last known status and
  tries again at the next poll. Charts are not affected (NF5).
- A subscription that failed (503, timeout) is sent again by the sync.

### Without the Downloader

With `MarketData:Downloader:Enabled` false TradeViewer works on the files alone:

- adding an asset takes its metadata from `<source>/symbols.json` instead of the lookup and
  sends no subscription; an asset without a folder in the output root is rejected
- the rescan at startup and on request ("reload" in the asset's status) is the only import
- coverage is shown from the store; gaps and the Downloader status are not available

This is the mode of the tests and of development on a frozen copy of the output root.

### Candle Store (marketdata.db)

```text
series        id, source, symbol, timeframe                  unique (source, symbol, timeframe)
candles       series_id, open_time, open, high, low,         primary key (series_id, open_time)
              close, volume                                   WITHOUT ROWID
import_log    series_id, file, size, modified, imported_at   primary key (series_id, file)
sync_state    source, symbol, last_change, last_status_json, checked_at
```

- Importing a file replaces the candles of its period in one transaction: delete the period,
  insert the rows. A rewritten file can therefore add, change and remove candles.
- A file that breaks the format rules is not imported; the error is stored in the import log,
  shown in the asset's status (DL2) and tried again when the file changes.
- The store holds only the assets of TradeViewer and can be deleted at any time (MD5).
- Expected size for 100 assets, 10 years, five timeframes: about 45 million rows, 3 GB.

### Serving Candles

```text
GET /api/candles/{source}/{symbol}/{timeframe}?to={time}&limit={n}
-> { "t": [...], "o": [...], "h": [...], "l": [...], "c": [...], "v": [...] }
```

- Column arrays, times as Unix seconds. The chart module calls this endpoint directly.
- First page 2,000 candles, older pages 5,000 (CH4).
- A timeframe the source lacks is aggregated on read from the largest stored timeframe that
  divides it (MD3); aggregated candles are not stored.
- Indicators are calculated on request from the stored candles with the warm-up they need and
  returned in the same column format: `GET /api/indicators/{source}/{symbol}/{timeframe}`.
- The Api keeps a small in-memory cache of the most recently used series and indicator
  results; a `CandlesUpdated` for the asset removes its entries.

---

## 7. Downloader Integration

```text
IDownloaderClient                                   Abstractions/MarketData

LookupAsync(source, symbol)                         GET  sources/{source}/symbols/{symbol}
SubscribeAsync(source, symbol, request)             PUT  clients/{client}/subscriptions/...
UnsubscribeAsync(source, symbol)                    DELETE (404 counts as done)
GetSubscriptionsAsync()                             GET  subscriptions (all clients)
GetServiceStatusAsync()                             GET  status
GetSymbolStatusAsync(source, symbol)                GET  status/{source}/{symbol}
GetChangesAsync(source, symbol, after)              GET  status/{source}/{symbol}/changes
```

- `DownloaderClient` (Infrastructure) is a typed `HttpClient`. It maps problem details and
  network errors to `Result` errors, never to exceptions.
- The Downloader's contract records are copied unchanged from `Downloader.Contracts` into
  `TradeViewer.Infrastructure/MarketData/Downloader/Contracts`; every copied file starts with a
  "Do not edit" comment. The client maps them to Application models.
- The monitoring page (DL1-DL3) reads through TradeViewer.Api, which asks the Downloader on
  request; nothing of it is stored except `sync_state`.

---

## 8. EWTS Integration

Decision 0004. Interface: docs/knowledge/ewts-api.md.

```text
IEwtsClient                                         Abstractions/Ewts
every method takes the EwtsInstance it talks to

GetInstanceAsync                                    status, version, source
GetObservedAssetsAsync / AddObservedAssetAsync / RemoveObservedAssetAsync
GetAssetDetailsAsync                                count, setups, history
GetOverlaysAsync(symbol)                            drawings as data
StartTestRunAsync / GetTestRunsAsync / GetTestRunAsync / CancelTestRunAsync
GetAlertsAsync(after) / GetAlertSettingsAsync / UpdateAlertSettingsAsync
GetChangesAsync(after)                              what changed since change number n
```

- **Pass-through.** TradeViewer does not interpret analysis results. The records of the EWTS
  interface are copied into `TradeViewer.Contracts/Ewts` and travel from `EwtsClient` through
  the handler to the Web client unchanged. Overlays use a small vocabulary of generic shapes
  (label, line, level, zone, marker), so drawing them needs no Elliott Wave knowledge.
- **No storage.** TradeViewer stores only the registered instances and, per instance, the last
  change number it has seen. Observed assets, setups, test runs, alerts and alert settings live
  in EWTS. The last answer of each request is kept in memory to serve EW6; after a restart of
  TradeViewer with an unreachable instance there is nothing to show but "unreachable".
- **Change detection.** `EwtsPollingService` asks every instance for its changes every
  `PollSeconds` and publishes them through `IWorkspaceNotifier`. EWTS does not push.
- **Timeframes.** EWTS writes `M15, H1, H4, D1, W1`; TradeViewer's own API uses the
  Downloader's codes. `EwtsClient` converts.
- **Assets.** Adding an observed asset first makes sure the asset exists in TradeViewer
  (EW3), then calls the instance. EWTS subscribes at the Downloader under its own client name.

### Scenario Files

Decision 0006. EWTS writes one drawing file per scenario into a shared folder
(`Scenarios:RootPath`); the format is in docs/knowledge/ewts-api.md, Scenario Files.

```text
IScenarioFileReader                                 Abstractions/Ewts

ListAsync()                                         the head of every *.json below the root
ReadAsync(path)                                     one file with its groups and shapes
```

- TradeViewer only reads the folder and stores nothing. Every request reads the files, so a
  changed file is visible at the next reload (SC7). There is no watcher and no index.
- A scenario is addressed by its path relative to the root; the reader rejects a path that
  leaves the root.
- The shapes are the records of the analysis drawings and are drawn by the same primitives of
  the chart module (`setOverlays`).
- The scenario page is a chart pane outside the layouts: it takes asset, timeframe and time
  span from the file and saves nothing to the workspace.
- Without `Scenarios:RootPath` the feature is switched off and its menu entry is hidden.

---

## 9. API of TradeViewer

Routes are grouped per feature. Request and response records are in `TradeViewer.Contracts`.
Times are ISO 8601 UTC except in the column arrays of candles and indicators.

```text
/api/assets                                   list, add
/api/assets/{source}/{symbol}                 get, change subscription, remove
/api/assets/lookup/{source}/{symbol}          lookup at the Downloader
/api/candles/{source}/{symbol}/{timeframe}
/api/indicators/{source}/{symbol}/{timeframe}
/api/indicator-templates
/api/watchlists
/api/layouts
/api/drawings                                 by asset and layout
/api/fibonacci-presets
/api/downloader/status
/api/downloader/subscriptions
/api/downloader/assets/{source}/{symbol}      coverage, gaps, import errors
/api/ewts/instances
/api/ewts/instances/{id}/assets
/api/ewts/instances/{id}/assets/{symbol}
/api/ewts/instances/{id}/overlays/{symbol}
/api/ewts/instances/{id}/test-runs
/api/ewts/instances/{id}/alerts
/api/ewts/instances/{id}/alert-settings
/api/scenarios                                list
/api/scenarios/{path}                         one scenario with its shapes
/api/workspace/backups
/health
```

- Drawings, layouts and watchlists get their id (UUID) from the client and are written with
  an idempotent `PUT`, so the client can show a change at once and save it in the background
  (NF3, WS1).
- No OpenAPI document; `src/TradeViewer.Api/TradeViewer.Api.http` holds example requests.

### SignalR Hub `/hubs/workspace`

Server to client only; user actions go through REST.

```text
CandlesUpdated(source, symbol, timeframes)
AssetStatusChanged(source, symbol)
DownloaderStatusChanged(reachable)
EwtsInstanceStatusChanged(instanceId, reachable)
OverlaysChanged(instanceId, symbol)
TestRunChanged(instanceId, testRunId, state)
AlertReceived(instanceId, alert)
```

An event says what changed; the client then reads the data through REST. After a reconnect the
client reloads what it shows.

---

## 10. Chart Module

`TradeViewer.Chart` is a TypeScript module on top of Lightweight Charts (decision 0001). esbuild
bundles it into `TradeViewer.Web/wwwroot/chart/`; the first `dotnet build` runs `npm ci` and the
bundling through an MSBuild target (`-p:SkipChartBuild=true` skips it).

```text
TradeViewer.Chart/src/
├── index.ts               entry point exposed to Blazor
├── ChartPaneController    one chart: series, scales, crosshair, paging
├── data/                  fetching candles and indicators from the Api, page cache
├── indicators/            indicator series and panes (values come from the Api)
├── tools/                 interaction: hit testing, handles, tool state machine
├── primitives/            one series primitive per drawing type and per overlay shape
└── sync/                  crosshair sync between panes
```

### Interop Contract

```text
Blazor -> chart    createPane, setSeries(asset, timeframe), setIndicators, setDrawings,
                   setOverlays, setTool, setOptions, refresh, dispose
chart  -> Blazor   drawingCreated, drawingChanged, drawingDeleted, selectionChanged,
                   crosshairMoved, visibleRangeChanged, contextMenuRequested
```

- `ChartHost` (Web) wraps the calls in typed C# methods; events return through a
  `DotNetObjectReference`.
- Candles and indicator values never go through interop: Blazor passes asset and timeframe, the
  module fetches the column arrays itself.
- Web owns the list of drawings and saves it; the module owns the geometry while the user drags
  and reports the result. Undo and redo are client state in Web (DR2).
- Drawings and overlays are positioned by time and price. Switching the timeframe reloads
  candles and indicators only; a time between two candles is drawn at the candle that contains
  it.
- Zoom, pan and crosshair are handled in the browser without a request (NF2).

---

## 11. Persistence

Decision 0002.

```text
marketdata.db    candle store (see 6)      cache, may be deleted
workspace.db     what the user creates     backed up, upgraded, never deleted
```

Workspace tables: assets, watchlists, watchlist entries, layouts, panes, indicator templates,
drawings, Fibonacci presets, EWTS instances, settings.

- Microsoft.Data.Sqlite with plain SQL
- Schema changes are numbered SQL scripts in Infrastructure, applied at startup in one
  transaction each; the version is `PRAGMA user_version`. Before an upgrade the database is
  backed up (WS4)
- WAL mode; every command writes at once (WS1)
- Layout panes, indicator instances and drawings are stored as JSON with a type discriminator
  and a payload version, so a new drawing type needs no schema change
- Daily backup with the SQLite online backup API into `backups/`, keeping `KeepCount` copies;
  restoring replaces workspace.db after backing up the current one (WS3)

---

## 12. Configuration

Every value can be set in appsettings, by environment variable (`__` for `:`) or as a start
parameter.

```text
MarketData:RootPath                          required; the Downloader's output root, read only
MarketData:Downloader:Enabled                false   (tests keep false)
MarketData:Downloader:BaseUrl                http://localhost:8081
MarketData:Downloader:Client                 tradeviewer   (Development: tradeviewer-dev)
MarketData:Downloader:PollSeconds            30
MarketData:Downloader:RequestTimeoutSeconds  30
Ewts:Enabled                                 false   (tests keep false)
Ewts:PollSeconds                             30
Ewts:RequestTimeoutSeconds                   30
Scenarios:RootPath                           none; the folder of EWTS's scenario drawing files, read only
Storage:DataPath                             required; holds marketdata.db, workspace.db, backups/
Storage:Backup:KeepCount                     14
Storage:Backup:Time                          03:00   (local time of the host)
```

With `Enabled` false the client is not called and the background service does not start;
TradeViewer then works with the files and databases it has (for market data see chapter 6,
Without the Downloader).

Development starts on a frozen copy of the output root with the Downloader switched off
(`/home/admin/marketdata`, owner decision 2026-11-11). The Downloader is switched on in
development when the sync of the Assets and Market Data epic is built. A missing root path or data path
stops the start with a clear message. EWTS instances are not configured here; the user
registers them (EW1).

---

## 13. Deployment

```text
deploy/Dockerfile          node (chart build) -> dotnet sdk 10.0 (publish) -> aspnet 10.0
deploy/docker-compose.yml  one service

volumes
  /data/marketdata    the Downloader's output root, mounted read-only
  /data/scenarios     EWTS's scenario folder, mounted read-only (optional)
  /data/app           Storage:DataPath, read-write
```

- One image for linux/arm64 and linux/amd64; the .NET output is framework-dependent and has no
  runtime identifier
- Port 8080, http only, no authentication (NF8)
- Logs go to the console (container log) through `ILogger`
- The databases belong on an SSD, not on the SD card of the Pi

---

## 14. Testing

```text
UnitTests           Domain: aggregation, indicators (against reference values), file rules,
                    drawing payloads. Application: handlers with in-memory ports.
IntegrationTests    Api with WebApplicationFactory, temporary SQLite files, CSV fixtures;
                    DownloaderClient and EwtsClient against recorded responses.
ArchitectureTests   dependency rules, feature rules.
Chart (Vitest)      tool geometry, hit testing, time-to-candle mapping.
```

- xUnit for the C# tests
- Tests never contact the Downloader or an EWTS instance and never read the live output root

---

## 15. Code Style

The target framework is .NET 10, but code is written in a .NET 8 style:

- `LangVersion` stays at 12, so the compiler rejects C# 13 and 14 features. Raising it is an
  owner decision.
- Prefer readable, widely known constructs over concise or new syntax.
- New .NET 10 library APIs may be used when they make the code simpler or safer.
- Formatting rules are in `.editorconfig`.

---

## 16. Open Points

Not decided yet; each must be closed before the epic that needs it starts.

```text
O1  EWTS has to accept and build the interface of ewts-api.md, chapter "Required Interface":
    the scenario drawing files for the Scenario Viewer, the API for the epics EWTS Instances,
    Analysis Overlays, Test Runs and Alerts. The owner aligns this with the EWTS agent by a
    prompt written from that chapter.
O2  Default parameters and colours of the indicators (CH5) and the colour of each overlay
    role are not specified; they are set in the plans of the Indicators and Scenario Viewer
    epics.
```
