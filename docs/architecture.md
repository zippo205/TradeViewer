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
   (EWTS folder; TradeViewer
   writes *.expert.json)
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
Api            -> Web (a build reference that brings the published files of the client to
                  serve; no type of Web is used)
Application    -> Domain, Contracts
Infrastructure -> Application, Domain, Contracts (the ports return the records of the Contracts)
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
├── Assets            symbol list, add, history, remove (AS2-AS5)
├── MarketData        candles, current candle, import, sync status, gaps (MD1-MD8)
├── Watchlists        the five fixed lists, order, row figures (AS1, AS6)
├── ChartSettings     settings per asset (CP2): so far "drawings hidden" (DR6)
├── Indicators        calculation requests, indicator templates (IN1, IN2)
├── Drawings          drawings, Fibonacci presets (DR1-DR6)
├── Downloader        status and subscriptions for the monitoring page (DL1-DL4)
├── EwtsInstances     registration, status, observed assets, overview, details (EW1-EW6)
├── Overlays          (OV1-OV5)
├── TestRuns          (BT1-BT7)
├── Alerts            (AL1-AL4)
├── Scenarios         list and read scenario drawing files (SC1-SC9), own analysis (SC10-SC13)
└── Workspace         settings (default history, last chart) and backup and restore (WS3, WS5)
```

A feature folder contains only the subfolders it needs:

```text
Watchlists/
├── Commands        ReorderWatchlistCommand
├── Queries         GetWatchlistsQuery
├── Handlers        ReorderWatchlistCommandHandler, GetWatchlistsQueryHandler
├── Validators      ReorderWatchlistCommandValidator
└── Services        helpers used by several handlers of the feature
```

Naming: `<Verb><Noun>Command`, `<Verb><Noun>Query`, the handler and the validator carry the
full name of their message plus `Handler` / `Validator`. One type per file. The acronym is
written `Ewts` in identifiers (`EwtsInstance`, `IEwtsClient`).

A feature never uses the types, handlers or services of another feature. What several
features need (reading an asset, a candle series, an EWTS instance, the settings) is a port in
`Abstractions`, implemented in Infrastructure. The architecture tests check that no feature
namespace depends on another.

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
NotFound       404     unknown asset, instance, ...
Conflict       409     e.g. asset already exists
InvalidData    422     a file or an upstream answer breaks its contract
Unavailable    503     the Downloader or an EWTS instance cannot be reached
```

All error responses are problem details (RFC 9457). An unexpected exception is logged and
answered with 500 without internal details.

### Ports

```text
Abstractions/
├── Messaging     the CQRS interfaces, Result, Error, FileContent
├── MarketData    ICandleStore, ICandleFileReader, IDownloaderClient, ISymbolCatalog
├── Ewts          IEwtsClient, IScenarioFileReader, IExpertAnalysisStore
├── Workspace     one repository interface per feature: IAssetRepository, IChartSettingsRepository, IDrawingRepository, IEwtsInstanceRepository, ...;
│                 ISettingsRepository, IWorkspaceBackups
└── Realtime      IWorkspaceNotifier (implemented by the Api on top of the SignalR hub)
```

The clock is the built-in `TimeProvider`.

---

## 5. Domain Model

```text
SymbolInfo          source, symbol, display name, asset class, tick size, session,
                    source timeframes: what a source says about a symbol
Asset               SymbolInfo, AssetHistory, position in its watchlist, subscription sent
AssetHistory        first day per timeframe, or none for the complete history (AS3)
HistorySettings     default history per timeframe: complete or a number of days (AS3)
AssetClass          Crypto, Stock, Etf, Forex, Commodity; also the watchlist of the asset
Session             Continuous, Exchange (time zone, open, close), NewYorkClose
Timeframe           15m, 1h, 4h, 1d, 1w (later 1m, 5m, 12h, 1M); the same codes everywhere
Candle              open time (Unix seconds, UTC), open, high, low, close, volume
SeriesKey           source, symbol, timeframe: the candles of one asset in one timeframe
TimeframeAggregator builds a timeframe, or only its current candle, from a lower one along the session (MD3, MD8);
                    built so far: the current candle for a continuous session
CandleFileRules     file name -> period, row validation (see downloader-interface.md)

Indicator           Sma, Ema, BollingerBands, Rsi, Macd, Atr: candles + parameters -> series
IndicatorInstance   indicator type, parameters, style
IndicatorTemplate   name, indicator instances

ChartSettings       asset, indicator instances, drawings hidden, selected EWTS instance, hidden groups
                    (the price scale mode is one setting for all charts, see chapter 9)

Drawing             id, asset, tool type, locked, anchors, style, text, payload version; what only
                    some tools have: levels (Fibonacci, copied from the preset when drawn), wave mark
                    (wave label), point labels and "line hidden" (polyline). DrawingRules refuses
                    what could not be read again; DrawingTools holds the rules of each tool
Anchor              time (UTC) + price, never a bar index; a wave label has no price (it sits at
                    the high or low of the candle that contains its time)
WaveMark            degree, label (1-5, A-E, W, X, Y) and side; WaveNotation gives the drawn form
FibonacciPreset     name, kind (retracement, extension), levels (ratios); the two defaults of DR4

EwtsInstance        id, name, base address
```

- Prices and indicator values are `double`.
- Analysis results of EWTS (overlays, setups, test runs, alerts) and scenario files are **not**
  domain models. They are contract records that pass through the Application layer unchanged
  (see 8). The same holds for the expert analysis file TradeViewer writes (SC12).
- Drawings refer to market data by asset and time only, never by a row of the candle store, so
  rebuilding the store cannot orphan a drawing.

---

## 6. Market Data

Decision 0003. Interface of the Downloader: docs/knowledge/downloader-interface.md.

### Flow

```text
add asset     symbol from ISymbolCatalog -> lookup -> PUT subscription (client "tradeviewer") -> store asset -> first import
              nothing is stored unless the Downloader accepted the subscription (AS2)
sync          MarketDataSyncService, per asset, aligned to the clock (MD2):
                due at every close of the asset's smallest timeframe + 30 s (the Downloader's
                cycle starts at :00:30, :15:30 ... UTC); then every PollSeconds until the symbol
                status shows a greater lastChange and the next poll shows no further one (a
                cycle writes several timeframes one after the other), at most
                PollWindowMinutes; all the time while the Downloader still downloads history
                for the asset; one check at startup and with every poll while the Downloader
                is unreachable
                no sync state yet (new asset, new candle store)? rescan, store lastChange
                lastChange greater than stored?
                  yes: changes?after=n -> import the named files -> store lastChange
                       complete == false -> rescan the asset
                -> IWorkspaceNotifier.CandlesUpdated
rescan        compare every *.csv of the asset (size, modified time) with the import log and
              import what differs; runs at the first import, on complete == false, and once at
              startup for every asset
rebuild       delete the rows of the asset from the candle store, then the first import as when
              adding; the asset shows as loading meanwhile (MD5)
change history PUT the subscription with the new start -> rescan: it imports what is wanted now
              and removes the candles of files before a later start. When the Downloader is
              unreachable the change is kept and the sync sends the subscription again
remove asset  switch off its analysis at EWTS if on -> DELETE subscription -> delete its
              drawings, chart settings and rows from the candle store and its sync state.
              It works without the Downloader: an unsubscription that fails is remembered in
              workspace.db and sent again by the sync
```

- Only files from the asset's history start per timeframe are imported, although the folder
  can hold more (another client, e.g. EWTS, may have subscribed a longer history). Opening a
  scenario whose time span is not stored moves the history start back to it (SC8).
- The symbols offered when adding an asset come from `ISymbolCatalog`: today a file per
  source shipped with the application (embedded in Infrastructure,
  `MarketData/Symbols/<source>.json`, the format of the Downloader's `symbols.json`, first
  `binance.json` with the 13 symbols of `~/marketdata/binance/symbols.json`), later the
  Downloader's symbol catalog. The default history is a user setting in the workspace (AS3): days
  per timeframe or complete. The start days of an asset are fixed when it is added. A complete
  history is sent to the Downloader as the start 1970-01-01, which it clips to the first day the
  source has (without a date its own defaults would apply).
- All writes of candles are done by one background worker (`ImportService`) in the order they
  were asked for: rescan, rebuild, import of named files, delete. Per asset the large timeframes
  come first and within a timeframe the newest files, 200 files per transaction, so a timeframe
  can be charted after seconds while the long history follows.
- Loading (AS4): a timeframe counts as loading while work for the asset is queued or running and
  not all of its wanted files are imported, or while the Downloader has jobs for it or reports a
  part of it as `pending`. The progress per timeframe is imported files of wanted files. A
  timeframe can be charted once it has candles. Without the Downloader only the import counts.
- The assets of the workspace are the truth (AS7). At every start and after a restore
  (`WorkspaceRestoreSignal`, raised by the restore) two things are brought in line with them. The
  import worker deletes from the candle store what belongs to no asset and rescans every asset.
  The sync reads the subscriptions of its own client (`GET clients/{client}/subscriptions`), ends
  those without an asset and marks assets without a subscription as not sent; after a restore it
  marks all of them, because the history may have changed. The sync also subscribes an asset
  whose symbol status does not name TradeViewer among the subscribers or is 404. Without the
  Downloader only the candle store is reconciled. Subscriptions of other clients are never touched.
- There is no file watcher. The Downloader's change numbers are the only change signal; the
  rescan is the fallback.
- When the Downloader is unreachable the sync logs it once, keeps the last known status and
  tries again at the next poll. Charts are not affected (NF5).
- A subscription that failed (503, timeout) is sent again by the sync.

### Without the Downloader

With `MarketData:Downloader:Enabled` false TradeViewer works on the files alone:

- adding an asset takes its metadata from the shipped symbol file instead of the lookup and
  sends no subscription; an asset without a folder in the output root is rejected
- the rescan at startup and on request ("reload" in the asset's status) is the only import
- coverage is shown from the store; gaps and the Downloader status are not available

This is the mode of the tests and of development on a frozen copy of the output root.

### Candle Store (marketdata.db)

```text
series        id, source, symbol, timeframe                  unique (source, symbol, timeframe)
candles       series_id, open_time, open, high, low,         primary key (series_id, open_time)
              close, volume                                   WITHOUT ROWID
import_log    series_id, file, size, modified, candles,        primary key (series_id, file)
              error, imported_at
sync_state    source, symbol, last_change, last_status_json, checked_at
```

- Importing a file replaces the candles of its period in one transaction: delete the period,
  insert the rows. A rewritten file can therefore add, change and remove candles.
- A file that breaks the format rules is not imported; the error is stored in the import log,
  shown in the asset's status (MD6, DL2) and tried again when the file changes.
- The store holds only the assets of TradeViewer and can be deleted at any time (MD5).
- It is treated as the cache it is: WAL with `synchronous=NORMAL`; times as Unix seconds, prices
  and volume as REAL. A store that cannot be opened or has a newer schema than the build is
  renamed to `marketdata.db.broken-<time>` and a new one is filled from the files; it never stops
  the start and is not checked with `quick_check` (NF10).
- Measured on WSL with the 13 symbols of the frozen copy, complete history (2.8 million candles
  in 121,277 files): import 11 s, store 277 MB, a page of 2,000 candles 2 to 17 ms, startup rescan
  0.9 s. Expected for 100 assets, 10 years, five timeframes: about 45 million rows, 3 to 4 GB.

### Serving Candles

```text
GET /api/candles/{source}/{symbol}/{timeframe}?to={time}&limit={n}
-> { "t": [...], "o": [...], "h": [...], "l": [...], "c": [...], "v": [...] }
```

- Column arrays, times as Unix seconds. The chart module calls this endpoint directly.
- First page 2,000 candles, older pages 5,000 (CH4): `limit` defaults to 2,000 and is at most
  5,000; `to` is exclusive, so an older page asks with the time of the oldest candle it has.
  An unknown asset is 404, a timeframe the asset does not have 422.
- A timeframe the source lacks is aggregated on read from the largest stored timeframe that
  divides it (MD3); aggregated candles are not stored.
- The current candle (MD8) is built on read from the closed candles of the smallest stored
  timeframe after the last closed candle of the requested one and appended as the last
  element; the response carries `"partial": true` then. It is not stored. `CandlesUpdated`
  of the smallest timeframe also tells open charts of higher timeframes to reload their last
  candle. Indicators use the same series including the current candle.
- Indicators are calculated on request from the stored candles with the warm-up they need and
  returned in the same column format: `GET /api/indicators/{source}/{symbol}/{timeframe}`.
- A cache of the most recently used series is not built: a page comes from SQLite in 2 to 17 ms
  (see the measurement above). It is added in the Price Chart epic only if NF1 is missed on the Pi.
  `limit` counts closed candles; the current candle comes on top.

---

## 7. Downloader Integration

```text
IDownloaderClient                                   Abstractions/MarketData

LookupAsync(source, symbol)                         GET  sources/{source}/symbols/{symbol}
SubscribeAsync(source, symbol, history)             PUT  clients/{client}/subscriptions/...
UnsubscribeAsync(source, symbol)                    DELETE (404 counts as done)
GetOwnSubscriptionsAsync()                          GET  clients/{client}/subscriptions (404: none)
GetSubscriptionsAsync()                             GET  subscriptions (all clients)   with Downloader Monitoring
GetServiceStatusAsync()                             GET  status                        with Downloader Monitoring
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

Decision 0004. Interface: docs/knowledge/ewts-interface.md.

```text
IEwtsClient                                         Abstractions/Ewts
every method takes the EwtsInstance it talks to

GetStatusAsync                                      identity, state, version, sources
GetObservedAssetsAsync / AddObservedAssetAsync(source, symbol, ...) / RemoveObservedAssetAsync(source, symbol)
GetAssetDetailsAsync(source, symbol)                 count, setups, history
GetOverlaysAsync(source, symbol)                    drawings as data
StartTestRunAsync / GetTestRunsAsync / GetTestRunAsync
CancelTestRunAsync / DeleteTestRunAsync              POST .../cancel and DELETE
GetProfilesAsync                                    parameter profiles for switching analysis on (EW3)
GetAlertsAsync(after) / GetAlertSettingsAsync / UpdateAlertSettingsAsync
GetChangesAsync(after)                              what changed since change number n
```

- **Pass-through.** TradeViewer does not interpret analysis results. The records of the EWTS
  interface are copied into `TradeViewer.Contracts/Ewts` and travel from `EwtsClient` through
  the handler to the Web client unchanged. Overlays use a small vocabulary of generic shapes
  (label, line, level, zone, marker, vertical line), so drawing them needs no Elliott Wave knowledge.
- **No storage.** TradeViewer stores only the registered instances and, per instance, the last
  change number it has seen and the number of the last alert the user has read (AL3). Observed assets, setups, test runs, alerts and alert settings live
  in EWTS. The last answer of each request is kept in memory to serve EW6; after a restart of
  TradeViewer with an unreachable instance there is nothing to show but "unreachable".
- **Change detection.** `EwtsPollingService` asks every instance for its changes every
  `PollSeconds` and publishes them through `IWorkspaceNotifier`. EWTS does not push.
- **Timeframes.** The EWTS API and its files use the Downloader's codes, so TradeViewer
  converts nothing.
- **Assets.** Analysis is switched on for an asset that is already in a watchlist (EW3);
  TradeViewer then calls the instance, which subscribes at the Downloader under its own
  client name. Which instance analyses an asset is known from the instance's own asset list,
  not stored in TradeViewer.

### Scenario Files

Decisions 0006 and 0007. EWTS writes one drawing file per scenario into a shared folder
(`Scenarios:RootPath`; TradeViewer writes an expert file only for a scenario the user has
reviewed and changed); the format is in docs/knowledge/ewts-interface.md, Scenario Files.

```text
IScenarioFileReader                                 Abstractions/Ewts

ListAsync()                                         the head of every scenario below the root, joined with its expert file
ReadAsync(path)                                     one file with its groups and shapes

IExpertAnalysisStore                                Abstractions/Ewts

SaveAsync(scenarioPath, file)                       write <id>.expert.json beside the scenario
CreateAsync(file)                                   write expert/<id>.expert.json (SC13)
```

- TradeViewer reads every request from the files, so a changed file is visible at the next
  reload (SC7). There is no watcher and no stored index; the parsed heads are kept in memory by file size and modified time, so a list request reads only files that changed. It writes only `*.expert.json` files, and only when
  the user has changed something (SC12, SC13); viewing a scenario creates nothing, atomically (`.part`, then rename), and never a file of EWTS. The list joins a
  scenario with its expert file by the id in the name.
- An expert file holds the analysis of the user twice: `groups` with shapes (for EWTS and for
  display) and `drawings` with the records of the drawing tools (to edit again), both written
  by the same handler.
- A scenario is addressed by its path relative to the root; the reader rejects a path that
  leaves the root.
- The shapes are the records of the analysis drawings and are drawn by the same primitives of
  the chart module (`setOverlays`).
- The scenario page is a chart apart from the chart page: it takes asset, timeframe and time
  span from the file. Its own analysis is saved in the expert file, not in the workspace.
- The scenarios of a test run are ordinary scenario files in the folder
  `test-runs/<run id>/` below the root. The test run names that folder; TradeViewer lists it
  with the same reader (BT4, BT5). Nothing about a trade is drawn from the report itself.
- The figures of a scenario are name and value pairs written by EWTS; TradeViewer shows them
  as columns and in the panel without knowing their meaning (SC4).
- Without `Scenarios:RootPath` the feature is switched off and its menu entry is hidden; test
  runs then show statistics only.

---

## 9. API of TradeViewer

Routes are grouped per feature. Request and response records are in `TradeViewer.Contracts`.
Times are ISO 8601 UTC except in the column arrays of candles and indicators.

```text
/api/symbols/{source}?q=                      symbols that can be added
/api/assets                                   list, add
/api/assets/{source}/{symbol}                 get (loading progress, available timeframes), remove; /history put changes the history
/api/assets/{source}/{symbol}/rescan          reload the files; /rebuild deletes the candles and imports again (MD5)
/api/candles/{source}/{symbol}/{timeframe}    column arrays; 422 when the timeframe can be neither read nor built (MD3)
/api/gaps/{source}/{symbol}/{timeframe}       the gaps the source confirmed, within the history of the asset (MD4)
/api/indicators/{source}/{symbol}/{timeframe}
/api/indicator-templates
/api/watchlists                               the five lists with their rows and figures (AS6)
/api/watchlists/{assetClass}/order            put: all assets of one list in their new order; 409 when
                                              the list has other assets than the order names
/api/chart-settings/{source}/{symbol}         get, put: the settings per asset (CP2); the defaults when never
                                              saved; 404 for an unknown asset
/api/settings                                 default history, last chart, price scale mode of all charts
                                              (AS3, CP3, CP2); get, put (a part that is null stays)
/api/drawings/{source}/{symbol}               the drawings of an asset in the order they were made
/api/drawings/{id}                            put stores (new or changed), delete; 400 with the fields when
                                              the record breaks the rules of its tool, 404 unknown asset,
                                              409 when the id is a drawing of another asset
/api/fibonacci-presets                        list; /{id} put (levels kept sorted), delete; 409 for the last
                                              preset of a kind
/api/downloader/status
/api/downloader/subscriptions
/api/downloader/assets/{source}/{symbol}      coverage, gaps, import errors
/api/ewts/instances
/api/ewts/instances/{id}/assets
/api/ewts/instances/{id}/assets/{symbol}      put switches analysis on, delete off, get details
/api/ewts/instances/{id}/profiles
/api/ewts/instances/{id}/overlays/{symbol}
/api/ewts/instances/{id}/test-runs            list, start; /{runId} get, delete; /{runId}/cancel
/api/ewts/instances/{id}/test-runs/{runId}/scenarios.zip   ZIP of the run's scenario files, built on request (BT6)
/api/ewts/instances/{id}/alerts
/api/ewts/instances/{id}/alert-settings
/api/alerts                                   history across all instances, filters (AL2); /read marks them read (AL3)
/api/scenarios                                list; post creates an expert scenario (SC13)
/api/scenarios/{path}                         one scenario with its shapes
/api/scenarios/{path}/expert                  get, put the analysis of the user (SC12)
/api/workspace/backups                        list; post makes a backup now; /{name} download; /{name}/restore;
                                              /restore restores the uploaded file (the body, at most 256 MB)
/health
```

- Drawings get their id (UUID) from the client and are written with an idempotent `PUT`, so the
  client can show a change at once and save it in the background (NF3, WS1). Chart settings,
  settings and the order of a watchlist are replaced as a whole.
- A request that changes drawings names its window in the header `X-TradeViewer-Window` (a
  UUID per browser window); the hub event `DrawingsChanged` carries it back, so that window
  ignores its own change.
- The lock of a drawing (DR2) is kept by the chart, not checked by the Api: the client sends only
  the last of several changes of one drawing, so "unlock, then move" arrives as one save that must
  not be refused.
- A ZIP and a backup download are file results: a handler returns `Result<FileContent>` (stream,
  name, media type) and the endpoint streams it; the ZIP is built in memory from the scenario
  files of the run.
- No OpenAPI document; `src/TradeViewer.Api/TradeViewer.Api.http` holds example requests.

### SignalR Hub `/hubs/workspace`

Server to client only; user actions go through REST.

```text
CandlesUpdated(source, symbol, timeframes)    timeframes as codes
AssetStatusChanged(source, symbol)
DownloaderStatusChanged(reachable)
DrawingsChanged(source, symbol, window)        a drawing of the asset was saved or deleted by that window
EwtsInstanceStatusChanged(instanceId, reachable)
OverlaysChanged(instanceId, symbol)
TestRunChanged(instanceId, testRunId, state)
AlertReceived(instanceId, alert)
```

An event says what changed; the client then reads the data through REST. After a reconnect the
client reloads what it shows. The scenario list has no event: the scenario page asks
`/api/scenarios` every 30 seconds (SC7).

---

## 10. Chart Module

`TradeViewer.Chart` is a TypeScript module on top of Lightweight Charts (decision 0001). esbuild
bundles it into `TradeViewer.Web/wwwroot/chart/`; the first `dotnet build` runs `npm ci` and the
bundling through an MSBuild target (`-p:SkipChartBuild=true` skips it).

Lightweight Charts is pinned to exactly 5.3.0 and part of the bundle (NF6). Version 5 has panes and
pane primitives next to series primitives; both kinds were proved in a browser (attached, drawn,
hit-tested through `hoveredObjectId`, detached). An update of the library is a deliberate step with
the chart checked by hand. Its attribution logo stays switched on (licence, decision 0001).

```text
TradeViewer.Chart/src/
├── index.ts               entry point exposed to Blazor: createPane; panes() for measurements
├── chartPane.ts           one chart (ChartPane): series, scales, crosshair legend, paging, last-close line,
│                          jump to latest (CH5), the message shown instead of a chart without data
├── range.ts               the rules of the visible range as pure functions (CH3, CH5)
├── format.ts, theme.ts    price format from the tick size, UTC times (MD7), colours for light and dark
├── data/                  fetching candles from the Api (later indicators and gaps), the page cache
│                          (SeriesData), when to load older candles (CH4)
├── indicators/            indicator series and panes (values come from the Api)
├── tools/                 interaction: time-to-index mapping, geometry and hit testing, dragging,
│                          pointer input taken before the chart, tool state machine
└── primitives/            the drawing layer (one series primitive holding all drawings, one class
                           per drawing type on it) and the overlay shapes
```

### Interop Contract

```text
Blazor -> chart    createPane, setSeries(asset, timeframe), setIndicators, setDrawings,
                   setOverlays, setTool(tool, style), setDrawingOptions(magnet, hidden),
                   selectDrawing, setOptions, refresh, dispose
chart  -> Blazor   drawingCreated, drawingChanged, selectionChanged, toolChanged
```

Deleting a drawing is an action of Web (the bar of the selected drawing, the Delete key): Web
removes it from its list and calls setDrawings, so the module reports no deletion. There is no
context menu; the bar of the selected drawing does what one would.

- `ChartHost` (Web) wraps the calls in typed C# methods; events return through a
  `DotNetObjectReference`. Built so far: createPane, setSeries (asset, timeframe, tick size),
  setOptions (price scale mode), refresh, dispose; the other calls come with their epics.
- The chart page is `/chart/{source}/{symbol}/{timeframe}` (CP1); changing asset or timeframe
  changes the address without a page load. `/` opens the last chart (CP3), or the assets page when
  there is none. The chart stays alive across such changes: only the series is replaced.
- The watchlists (AS1, AS6) are a panel at the right of the chart page, as in TradingView: the five
  lists as tabs, a row per asset with last close, change and marks; choosing a row shows the asset
  in this window in the same timeframe (CP1). The panel can be folded away; that and the open list
  are kept in the browser (`localStorage`), not in the workspace. The order is changed on the
  assets page only (drag and drop, or arrow buttons), which shows the same five lists with the
  management actions folded behind each asset. Rows are read again when the hub reports new
  candles or a changed state, once for many events.
- Row figures: the last close is the close of the newest closed candle of the smallest timeframe
  (never the candle in progress); the change compares it with the last daily candle that ended
  before its day, the day cut at 00:00 UTC (`RowFigures` in the Domain). The warning of a row names
  files that were not imported, and nothing else: a gap of the source never goes away, so a mark
  for it would always be on. The rules about the Downloader's status that several features share
  are in `DownloaderStatusRules`.
- Gaps (MD4) are shown in the chart: a series primitive (`GapMarkers`) draws a small icon at the
  bottom of the pane between the last candle before a gap and the first one after it; pointing at
  it shows from when to when candles are missing and why. The module reads them from `/api/gaps`
  after the candles of a series, without waiting for them. The Api takes them from the last status
  the Downloader reported (sync state), only those with the reason `missingUpstream` and only
  within the history of the asset: the Downloader also reports gaps of history that another client
  wanted. Without the Downloader no gaps are known.
- The module draws what belongs to the canvas itself: the legend with the values under the
  crosshair, the jump-to-latest button and the message in place of a chart without data. Volume is
  a histogram in the lower fifth of the candle pane on its own hidden scale, as in TradingView;
  the current candle is drawn paler (MD8). The chart follows the light or dark setting of the system.
- Visible range (CH3): the number of visible candles is kept. At the latest candles the view stays
  there with its space to the right; scrolled back within one asset the time at the right edge is
  kept; another asset always shows its latest candles. A new chart shows 150 candles and 8 bars of
  space.
- Paging (CH4): first page 2,000 candles; a page of 5,000 older ones is requested when fewer than
  500 are left to the left of the view, and added in front without moving the view.
- Live update (MD2): the hub client stays in Web; on `CandlesUpdated` for the shown asset
  `ChartHost.RefreshAsync` lets the module read the newest page again. When only the end changed,
  the last candles are updated in place; the view never moves. After a reconnect of the hub the
  page does the same.
- Measured on WSL in a headless Chromium: click to painted chart 112 to 123 ms at worst, median
  76 ms (NF1: 150 ms); zoom, pan and crosshair 60 frames per second with no request (NF2).
- Candles and indicator values never go through interop: Blazor passes asset and timeframe, the
  module fetches the column arrays itself.
- Web owns the list of drawings and saves it; the module owns the geometry while the user drags
  and reports the result. Undo and redo are client state in Web (DR2).
- Drawings in the module: one series primitive, the drawing layer, draws all drawings of the chart
  and the preview of the one being placed; each tool is one shape class in `primitives/shapes/`
  (geometry, drawing, hit test, handles, reshape). The state machine (`tools/interaction.ts`)
  places a drawing with click, move, click and then returns to the pointer; selects the topmost
  drawing under the pointer; moves it by whole bars, reshapes it by a handle of the selected one;
  Escape cancels. A locked drawing is selected but never moved or reshaped. The magnet (on by
  default) snaps a placed or reshaped anchor to the open, high, low or close of its candle that is
  nearest in pixels. A press the module takes never reaches the library (`mousedown` in the
  capture phase on the element around the chart), so the chart does not pan; any other press
  goes to the chart. A horizontal line shows its price on the price scale.
- Drawings and overlays are positioned by time and price. Switching the timeframe reloads
  candles and indicators only; a time between two candles is drawn at the candle that contains
  it.
- Zoom, pan and crosshair are handled in the browser without a request (NF2). The module keeps
  the number of visible candles and the anchor itself when the timeframe or asset changes (CH3).
- The drawing layer of the scenario page uses the same calls; Web saves it to the expert file
  instead of the workspace. Toolbars, the instance selector and the corner display (OV5) are
  Blazor components next to the canvas.

---

## 11. Persistence

Decision 0002.

```text
marketdata.db    candle store (see 6)      cache, may be deleted
workspace.db     what the user creates     backed up, upgraded, never deleted
```

Workspace tables: assets (with their position in the watchlist, their history per timeframe and
the unsubscriptions still to be sent), chart settings per asset,
indicator templates, drawings, Fibonacci presets, EWTS instances (with the last seen change
and alert numbers), settings (default history, last chart).

- Microsoft.Data.Sqlite with plain SQL
- Schema changes are numbered SQL scripts in Infrastructure (`Workspace/Scripts/NNNN_name.sql`,
  embedded), applied at startup in one transaction each; the version is `PRAGMA user_version`.
  Before an upgrade the database is backed up (WS4). A workspace of a newer version than the build
  knows, and a damaged one (`PRAGMA quick_check`), stop the start; nothing is changed
- Script 0001 writes `PRAGMA application_id` ("TVWS"), which marks a file as a workspace
- WAL mode with `synchronous=FULL`: every command writes at once (WS1) and a committed change
  survives a power loss
- Every use of workspace.db goes through `WorkspaceDatabase.OpenAsync`, which hands out a pooled
  connection together with a shared lease. Code holding one must not ask for a second
- Chart settings, indicator instances and drawings are stored as JSON with a type discriminator
  and a payload version, so a new drawing type needs no schema change
- Backups are made with the SQLite online backup API into `backups/`: once a day at
  `Storage:Backup:Time`, at once after a start when the backup of the day is missing, before a
  schema upgrade, and when the user asks for one. The name tells time (UTC) and kind:
  `workspace-<yyyyMMdd-HHmmss>-daily.db`, `-manual.db`, `-before-v<n>.db`. The `KeepCount` newest
  are kept, whatever their kind (WS3)
- A restore, of a backup or an uploaded file, works on a temporary copy first: integrity check, is a
  workspace, version not newer than the build, then the schema upgrade. Only a copy that passed
  replaces workspace.db: the restore takes the exclusive lease (it waits for the shared ones,
  later requests wait for it), closes the pooled connections and renames the copy over the file.
  No restart; a refused restore changes nothing; there is no safety copy of the replaced workspace

---

## 12. Configuration

Every value can be set in appsettings, by environment variable (`__` for `:`) or as a start
parameter.

```text
MarketData:RootPath                          required; the Downloader's output root, read only
MarketData:Downloader:Enabled                false   (tests keep false)
MarketData:Downloader:BaseUrl                http://localhost:8081
MarketData:Downloader:Client                 tradeviewer   (Development: tradeviewer-dev)
MarketData:Downloader:PollSeconds            20   (interval of the checks after a due time)
MarketData:Downloader:PollWindowMinutes      10   (a due check gives up after this)
MarketData:Downloader:RequestTimeoutSeconds  30
Ewts:Enabled                                 false   (tests keep false)
Ewts:PollSeconds                             30
Ewts:RequestTimeoutSeconds                   30
Scenarios:RootPath                           none; the folder of EWTS's scenario drawing files; TradeViewer writes only its *.expert.json
Storage:DataPath                             required; holds marketdata.db, workspace.db, backups/
Storage:Backup:KeepCount                     14
Storage:Backup:Time                          03:00   (UTC)
```

With `Enabled` false the client is not called and the background service does not start;
TradeViewer then works with the files and databases it has (for market data see chapter 6,
Without the Downloader).

Development starts on a frozen copy of the output root with the Downloader switched off
(`/home/admin/marketdata`, owner decision 2026-11-11). The Downloader is switched on in
development when the sync of the Assets and Market Data epic is built. A missing root path or data path
stops the start with a clear message, and so does an output root or a scenario folder that
does not exist (a missing mount; the output root is never created), and a data path that is,
or lies inside, the output root or the scenario folder. A path may start with `~` (home
folder); a relative path is resolved against the content root of the Api. A `Scenarios:RootPath`
that is not writable switches off only SC10 to SC13; the viewer still works. EWTS instances are not configured here; the user
registers them (EW1).

---

## 13. Deployment

```text
deploy/Dockerfile          node (chart build) -> dotnet sdk 10.0 (publish) -> aspnet 10.0
deploy/docker-compose.yml  one service

volumes
  /data/marketdata    the Downloader's output root, mounted read-only
  /data/scenarios     EWTS's scenario folder, read-write for the expert files (optional)
  /data/app           Storage:DataPath, read-write
```

- One image for linux/arm64 and linux/amd64; the .NET output is framework-dependent and has no
  runtime identifier
- Port 8080, http only, no authentication (NF8)
- The container reaches the Downloader (8081) and the EWTS instances (8082-8089, several are normal)
  through `http://host.docker.internal:<port>`; the compose file maps the name with `extra_hosts`
  (`host-gateway`), which the Docker engine on the Pi needs
- The compose file limits the memory to 512 MB (NF9), checks `/health`, and runs the container as
  a configurable user (`user:`), so the expert files written into the scenario folder belong
  to the owner of that folder and EWTS can read them
- Logs go to the console (container log) through `ILogger`
- The databases belong on an SSD, not on the SD card of the Pi

---

## 14. Testing

```text
UnitTests           Domain: aggregation, indicators (against reference values), file rules,
                    drawing payloads, the due times of the sync with a fake `TimeProvider`
                    (MD2), client state of Web such as undo and redo (DR2). Application:
                    handlers with in-memory ports.
IntegrationTests    Api with WebApplicationFactory, temporary SQLite files, CSV fixtures;
                    DownloaderClient and EwtsClient against recorded responses; scenario files
                    read from fixtures, expert files written to a temporary folder (atomic,
                    never touching a file of EWTS).
ArchitectureTests   dependency rules, feature rules.
Chart (Vitest)      tool geometry, hit testing, time-to-candle mapping.
Benchmark           first candle page over 10 years of 15m candles (NF1); run on request,
                    not with the normal tests.
```

- xUnit for the C# tests
- No browser tests in V1: the pages are checked by hand, the chart by its Vitest tests
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
O1  EWTS has to accept and build the interface of ewts-interface.md, chapter "Required Interface":
    the scenario drawing files for the Scenario Viewer, the API for the epics EWTS Instances,
    Analysis Overlays, Test Runs and Alerts, and the reading of the `*.expert.json` files
    TradeViewer writes (decision 0007). The owner aligns this with the EWTS agent by a
    prompt written from that chapter.
```
