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
   writes expert/*.json)
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
├── ChartSettings     settings per asset (CP2): indicators, "drawings hidden" (DR6); indicator templates (IN2)
├── Indicators        calculation requests (IN1)
├── Drawings          drawings, Fibonacci presets (DR1-DR6)
├── Downloader        status and subscriptions for the monitoring page (DL1-DL4)
├── EwtsInstances     registration, status, observed assets, overview, details (EW1-EW6)
├── Overlays          (OV1-OV5)
├── TestRuns          (BT1-BT7)
├── Alerts            (AL1-AL4)
├── Scenarios         scenario editor (SC7-SC16, SC20, SC21) and test-run viewer (SC17-SC19)
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
├── MarketData    ICandleStore, ICandleSeriesReader (a page as the chart shows it, with warm-up), ICandleFileReader,
│                 IDownloaderClient, ISymbolCatalog
├── Ewts          IEwtsClient, IScenarioFileReader, IExpertScenarioStore
├── Workspace     one repository interface per feature: IAssetRepository, IChartSettingsRepository, IIndicatorTemplateRepository,
│                 IDrawingRepository, IEwtsInstanceRepository, ...;
│                 ISettingsRepository, IWorkspaceBackups
└── Realtime      IWorkspaceNotifier (implemented by the Api on top of the SignalR hub)
```

The clock is the built-in `TimeProvider`.

---

## 5. Domain Model

```text
SymbolInfo          source, symbol (the folder name), ticker at the source (sourceSymbol, for
                    binance the same), display name, asset class, tick size, session, source
                    timeframes: what a source says about a symbol
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

Indicator           Sma, Ema, BollingerBands, Rsi, Macd, Atr: candles + parameters -> named lines, one
                    double? per candle, as TradingView's built-in scripts calculate them (seeds included);
                    WarmUp says how many candles before a page a calculation needs
IndicatorParameters one record per type with the defaults of IN1; IndicatorRules: allowed values
IndicatorInstance   id, parameters, style per line (colour, width), hidden
IndicatorTemplate   id, name, indicator instances

ChartSettings       asset, indicator instances, drawings hidden, selected EWTS instance, hidden groups
                    (the price scale mode is one setting for all charts, see chapter 9)

Drawing             id, asset, tool type, locked, anchors, style, text, payload version; what only
                    some tools have: levels (Fibonacci, copied from the preset when drawn), wave mark
                    (wave label), point labels and "line hidden" (polyline). DrawingRules refuses
                    what could not be read again; DrawingTools holds the rules of each tool
Anchor              time (UTC) + price, never a bar index; a wave label has no price (it sits at
                    the high or low of the candle that contains its time)
WaveMark            degree, label (1-5, A-E, W, X, Y, Z) and side; WaveNotation gives the drawn form
FibonacciPreset     name, kind (retracement, extension), levels (FibonacciLevel: ratio, on or off); the
                    two defaults of DR4, with further levels switched off

EwtsInstance        id, name, base address
```

- Prices and indicator values are `double`.
- Analysis results of EWTS (overlays, setups, test runs, alerts) and scenario files are **not**
  domain models. They are contract records that pass through the Application layer unchanged
  (see 8). The same holds for the expert scenario file TradeViewer writes (SC13); its form
  checks (SC15) are rules of TradeViewer in Domain/Scenarios.
- Drawings refer to market data by asset and time only, never by a row of the candle store, so
  rebuilding the store cannot orphan a drawing.

---

## 6. Market Data

Decision 0003. Interface of the Downloader: docs/knowledge/downloader-interface.md.

### Flow

```text
add asset     ticker picked from the catalog -> lookup -> PUT subscription (client "tradeviewer",
              folder name in the path, ticker in the body) -> store asset -> first import
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
  scenario whose time span is not stored moves the history start back to it (SC6).
- The symbols offered when adding an asset come from the Downloader's symbol catalog
  (`IDownloaderClient.SearchAsync`), searched while the user types (250 ms after the last key,
  `limit` 50, "type more" when the answer is truncated); only the sources in
  `SymbolSources.Chartable` are offered (binance, until the header format and trading sessions
  can be read). Without the Downloader they come from `ISymbolCatalog`, a file per source shipped
  with the application (embedded in Infrastructure, `MarketData/Symbols/<source>.json`, the
  format of the Downloader's `symbols.json`, `binance.json` with the 13 symbols of the frozen
  copy). Picking a symbol looks it up at the Downloader by its ticker before it is added. The
  asset keeps the ticker (`assets.source_symbol`) and sends it with every subscription (AS7).
- The default history is a user setting in the workspace (AS3): days per timeframe or complete. The start days of an asset are fixed when it is added. A complete
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
  tries again at the next poll. Charts are not affected (NF5). Since when it is unreachable (DL4)
  is kept by `IDownloaderReachability` (Abstractions/MarketData), one per application and in memory:
  the sync and the monitoring queries report every answer and failure to it, the first failure of a
  run sets the time, an answer clears it, and a change is sent once as `DownloaderStatusChanged`.
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
  divides it (MD3), for assets that trade around the clock or through the week; an asset with
  exchange hours has no 4h (decision 0008). Aggregated candles are not stored. Built with the
  stock and Forex sources; binance delivers every timeframe.
- The current candle (MD8) is built on read from the closed candles of the smallest stored
  timeframe after the last closed candle of the requested one and appended as the last
  element; the response carries `"partial": true` then. It is not stored. `CandlesUpdated`
  of the smallest timeframe also tells open charts of higher timeframes to reload their last
  candle. Indicators use the same series including the current candle.
- Indicators are calculated on request from the stored candles and returned in the same column
  format: `GET /api/indicators/{source}/{symbol}/{timeframe}?type=&length=&deviation=&fast=&slow=&signal=&price=&to=&limit=`
  -> `{ "t": [...], "lines": { "basis": [...], ... }, "partial": ... }`. The candles are read exactly
  as for /api/candles (`ICandleSeriesReader`, the same `to` and `limit`, the current candle), so `t`
  is the candle page; the warm-up candles before the page are read with it, enough that a page
  equals a calculation over the whole history (EMA and RMA to 1e-10 of their start). A value is
  null where the indicator is not defined yet. Parameters left out take the defaults of IN1; one the
  type does not have is 400. Nothing is cached: 2,000 values of RSI on 15m take 14 ms.
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
  request; nothing of it is stored except `sync_state`. It only reads: no call of the feature
  Downloader changes anything at the Downloader. The colour of the status dot is decided in
  `DownloaderStatusRules.LightOf`: off (switched off, or not asked yet), red (no answer), yellow (a
  source not ok, the last cycle partial, failed or interrupted, or an answer that could not be read),
  green.

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

Decisions 0006, 0010, 0011 and 0012. The scenario folder (`Scenarios:RootPath`) holds two kinds of scenario,
each with one writer; the formats are in docs/knowledge/ewts-interface.md, "Scenario Files" and
"Expert Scenario Files".

```text
<root>/expert/<id>.json               expert scenarios: named points, written by TradeViewer
<root>/test-runs/<run id>/<id>.json   test-run scenarios: shapes, written by EWTS (and <id>.pine)

IScenarioFileReader                   Abstractions/Ewts

ListRunsAsync()                       the run folders below test-runs/ with their summary (SC17)
ListRunAsync(runId)                   the head and figures of every scenario of one run
ReadAsync(runId, id)                  one test-run scenario with its groups and shapes

IExpertScenarioStore                  Abstractions/Ewts

ListAsync()                           the head of every expert scenario (SC7)
ReadAsync(id)                         one expert scenario, with the fields TradeViewer does not know
SaveAsync(scenario)                   write expert/<id>.json; the first save takes the next free id
DeleteAsync(id)                       delete expert/<id>.json (SC16)
```

- TradeViewer reads every request from the files, so a changed file is visible at the next
  reload (SC5). There is no watcher and no stored index; the parsed heads are kept in memory by
  file size and modified time, so a list request reads only files that changed.
- It writes only in `expert/`, and only when the user has changed something (SC13); viewing a
  scenario creates nothing. Files are written atomically (`<name>.part`, then rename). Nothing in
  `test-runs/` is ever written or deleted (SC19).
- The expert file is the only record of an expert scenario, nothing is in the workspace. Besides
  the named points it holds TradeViewer's section `drawings` (decision 0012): the free drawings
  of the scenario, each a drawing record of the workspace without the asset plus its level
  (context, setup, trigger), checked by `DrawingRules` like any drawing. The store keeps the
  fields it does not know (`legacy`, later additions) as raw JSON and writes them back unchanged.
- The three levels come from the context timeframe (1d -> 4h -> 1h, 4h -> 1h -> 15m; Domain,
  `ScenarioLevels`). Each section of the named points is written with the timeframe of its level.
- A point placed or moved in the editor is snapped by the Api, not the browser: Web sends where
  it was let go (time of the chart candle, price), the feature reads the candles of the level's
  timeframe and applies the snap rules of the format (Domain, `ScenarioSnap`), and answers with
  the scenario, its shapes and its form problems. Opening a scenario never moves a point.
- The id is taken on the first save under a lock in the store, so two windows cannot take the same
  number; it never changes afterwards (SC14). With several windows the last save wins (WS1).
- An expert scenario is drawn by building shapes from its named points (Application, feature
  Scenarios) in the vocabulary of the analysis drawings; a test-run scenario brings its shapes.
  Both are drawn by the same primitives of the chart module (`setOverlays`).
- An expert scenario is addressed by its id, a test-run scenario by run id and id; both are
  checked against the file name rules, so no request leaves its folder.
- Two pages, the scenario editor and the test-run viewer (decision 0011), share one Blazor
  component for the scenario chart, list and panel. The chart is apart from the chart page: it
  takes asset, timeframe and time span from the file.
- The scenarios of a test run lie in the folder `test-runs/<run id>/`, which the test run names
  (BT4, BT5). Nothing about a trade is drawn from the
  report itself. The figures are name and value pairs written by EWTS; TradeViewer shows them as
  columns and in the panel without knowing their meaning (SC18). `.pine` files are not read; they
  are part of a run's ZIP (BT6).
- Without `Scenarios:RootPath` the feature is switched off and its menu entry is hidden; test
  runs then show statistics only.

---

## 9. API of TradeViewer

Routes are grouped per feature. Request and response records are in `TradeViewer.Contracts`.
Times are ISO 8601 UTC except in the column arrays of candles and indicators.

```text
/api/symbols/{source}?q=                      symbols that can be added: the Downloader's catalog, or the
                                              fixed list without the Downloader; answers truncated
/api/symbols/{source}/{sourceSymbol}          look one up at the Downloader: tick size, session, earliest dates
/api/assets                                   list, add
/api/assets/{source}/{symbol}                 get (loading progress, available timeframes, confirmed gaps per timeframe as
                                              /api/gaps gives them), remove; /history put changes the history
/api/assets/{source}/{symbol}/rescan          reload the files; /rebuild deletes the candles and imports again (MD5)
/api/candles/{source}/{symbol}/{timeframe}    column arrays; 422 when the timeframe can be neither read nor built (MD3)
/api/gaps/{source}/{symbol}/{timeframe}       the gaps the source confirmed, within the history of the asset (MD4)
/api/indicators/{source}/{symbol}/{timeframe}  one indicator for the candle page that to and limit name (see 6)
/api/indicator-defaults                       a new indicator of each kind with the defaults of IN1, ids empty
/api/indicator-templates                      list by name; /{id} put (new or replaced), delete; 409 for a name
                                              another template has, whatever the case
/api/watchlists                               the five lists with their rows and figures (AS6)
/api/watchlists/{assetClass}/order            put: all assets of one list in their new order; 409 when
                                              the list has other assets than the order names
/api/chart-settings/{source}/{symbol}         get, put: the settings per asset (CP2): drawings hidden and the
                                              indicators; the defaults when never saved; 404 for an unknown
                                              asset; a put without indicators (null) keeps the saved ones
/api/settings                                 default history, last chart, price scale mode and magnet of all
                                              charts (AS3, CP3, CP2, DR2); get, put (a part that is null stays)
/api/drawings/{source}/{symbol}               the drawings of an asset in the order they were made
/api/drawings/{id}                            put stores (new or changed), delete; 400 with the fields when
                                              the record breaks the rules of its tool, 404 unknown asset,
                                              409 when the id is a drawing of another asset
/api/fibonacci-presets                        list; /{id} put (levels kept sorted), delete; 409 for the last
                                              preset of a kind
/api/downloader/status                        the Downloader's state and the light; always 200, also when it does
                                              not answer (reachable false, since when: DL4)
/api/downloader/subscriptions                 all clients merged per symbol, TradeViewer's client named; 503
                                              when the Downloader does not answer or is switched off
/api/downloader/assets/{source}/{symbol}      coverage, jobs, all gaps it holds, import errors; the sync's last
                                              status (live false, read at) when it does not answer; 404 for an
                                              asset not in the workspace
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
/api/scenarios/expert                         list (SC7); post saves a new expert scenario and returns its id (SC14)
/api/scenarios/expert/{id}                    get (points, drawings and the shapes built from the points), put, delete (SC13, SC16)
/api/scenarios/expert/snap                    snap a placed or moved point; returns the scenario, its shapes and
                                              problems without writing (SC10, also before the first save)
/api/scenarios/test-runs                      the run folders (SC17)
/api/scenarios/test-runs/{runId}              the scenarios of a run
/api/scenarios/test-runs/{runId}/{id}         one test-run scenario with its shapes (SC18)
/api/workspace/backups                        list; post makes a backup now; /{name} download; /{name}/restore;
                                              /restore restores the uploaded file (the body, at most 256 MB)
/health
```

- Drawings get their id (UUID) from the client and are written with an idempotent `PUT`, so the
  client can show a change at once and save it in the background (NF3, WS1). Chart settings,
  settings and the order of a watchlist are replaced as a whole.
- A request that changes drawings or chart settings names its window in the header
  `X-TradeViewer-Window` (a UUID per browser window); the hub events `DrawingsChanged` and
  `ChartSettingsChanged` carry it back, so that window ignores its own change.
- The lock of a drawing (DR2) is kept by the chart, not checked by the Api: the client sends only
  the last of several changes of one drawing, so "unlock, then move" arrives as one save that must
  not be refused.
- A ZIP and a backup download are file results: a handler returns `Result<FileContent>` (stream,
  name, media type) and the endpoint streams it; the ZIP is built in memory from the scenario
  files of the run.
- No OpenAPI document and no file of example requests: the routes are listed here, the records are
  in `TradeViewer.Contracts`, and the integration tests call every endpoint.

### SignalR Hub `/hubs/workspace`

Server to client only; user actions go through REST.

```text
CandlesUpdated(source, symbol, timeframes)    timeframes as codes
AssetStatusChanged(source, symbol)
DownloaderStatusChanged(reachable)
DrawingsChanged(source, symbol, window)        a drawing of the asset was saved or deleted by that window
ChartSettingsChanged(source, symbol, window)   the chart settings of the asset (indicators included) were saved
EwtsInstanceStatusChanged(instanceId, reachable)
OverlaysChanged(instanceId, symbol)
TestRunChanged(instanceId, testRunId, state)
AlertReceived(instanceId, alert)
```

An event says what changed; the client then reads the data through REST. After a reconnect the
client reloads what it shows. The scenario lists have no event: both scenario pages ask
for their list every 30 seconds (SC5).

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
├── data/                  fetching candles and gaps from the Api, the page cache (SeriesData), when to load
│                          older candles (CH4)
├── indicators/            the indicators (IN1): their values from /api/indicators paged with the candles
│                          (IndicatorValues), series and panes (IndicatorSet), band fill, legend lines
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
chart  -> Blazor   drawingCreated, drawingChanged, selectionChanged, toolChanged, indicatorAction(id, action)
```

Deleting a drawing is an action of Web (the bar of the selected drawing, the Delete key): Web
removes it from its list and calls setDrawings, so the module reports no deletion. There is no
context menu; the bar of the selected drawing does what one would.

- `ChartHost` (Web) wraps the calls in typed C# methods; events return through a
  `DotNetObjectReference`. Built so far: createPane, setSeries (asset, timeframe, tick size),
  setOptions (price scale mode), refresh, dispose, the drawing calls and events, and setIndicators
  with indicatorAction; the calls for overlays come with their epic. Four helpers of the module serve
  the page: `bindDrawingEvents` (the events of a pane to ChartHost), `bindIndicatorActions` (hide,
  settings, remove from a legend line), `bindPageKeys` (Delete, Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z on the
  page, not while typing in a field) and `warnWhenLeaving`.
- The chart page is `/chart/{source}/{symbol}/{timeframe}` (CP1); changing asset or timeframe
  changes the address without a page load. `/` opens the last chart (CP3), or the assets page when
  there is none. The chart stays alive across such changes: only the series is replaced.
- The watchlists (AS1, AS6) are a panel at the right of the chart page, as in TradingView: the five
  lists as tabs, a row per asset with last close, change and marks; choosing a row shows the asset
  in this window in the same timeframe, otherwise the next larger one it has, otherwise the largest
  smaller one (CP1, decision 0008; `TimeframeChoice` in Web). The panel can be folded away; that and the open list
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
  wanted (`DownloaderStatusRules.ConfirmedGaps`). The assets page shows the same gaps per
  timeframe: a badge with their number and the list in the asset's details. Without the Downloader
  no gaps are known.
- The Downloader page (`/downloader`, DL1-DL4) reads the status and the subscriptions when opened
  and every 10 seconds, and an asset's coverage when it is unfolded; a read that fails keeps what is
  shown, with the time it was read. Its coverage shows all gaps the Downloader holds (DL2). The
  status dot in the header (`DownloaderLight`) links to it and is read at start, every 60 seconds,
  on `DownloaderStatusChanged` and after a reconnect; both take their words from
  `DownloaderStatusText`.
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
  Dragging a line with 200 drawings of all tools on the chart: 60 frames per second, no frame
  over 16.8 ms and no long task, the same as on an empty chart (NF3).
- Other windows follow (CP1): a window that shows an asset reads its drawings again when the hub
  reports `DrawingsChanged` for it from another window, so a line drawn on 4h appears in the 1d
  window beside it, and its chart settings on `ChartSettingsChanged`, so its next save does not undo
  the other window's indicators. The window that made the change ignores its own event.
- Indicators (IN1, IN2): `setIndicators` takes the records of the chart settings unchanged; an
  unchanged one keeps its values, a changed parameter fetches again, a changed style redraws. SMA,
  EMA and BB are drawn in the price pane, every RSI, MACD and ATR in a pane of its own below, in the
  order added, with a quarter of the price pane's height; these panes stay linear when the price
  scale is logarithmic. Their values are fetched in parallel with the candles of a new series and
  drawn together with them; an older candle page fetches the same page of every indicator; a
  refresh asks for the newest 500. Legend lines: the price indicators below the candle legend, the
  others at the top of their pane, placed from `paneSize` whenever a pane element resizes (the
  library has no event for it); hide, settings and remove show on hover. Web has a bar above the
  chart with "Indicators" (the six, with the defaults read from /api/indicator-defaults) and
  "Templates" (apply replaces with copies, save, delete), and a settings dialog per indicator. Every
  change saves the chart settings at once; until they are read, a save leaves the indicators out.
- Drawing tools act in the price pane only: a press below it goes to the library, so in an
  indicator pane the chart pans as on an empty chart.
- Measured with the six indicators on the chart (WSL, headless Chromium): click to painted
  candles and indicators worst 128 ms, median 95 ms (NF1: 150 ms); the indicators arrive before the
  candles and are painted in the same frame. Zoom, pan and crosshair 60 frames per second with no
  request (NF2).
- Candles and indicator values never go through interop: Blazor passes asset and timeframe, the
  module fetches the column arrays itself.
- Web owns the list of drawings and saves it; the module owns the geometry while the user drags
  and reports the result. Undo and redo are client state in Web (DR2).
- Drawings in Web (`TradeViewer.Web/Drawings`, one of each per window): `DrawingSession` holds the
  drawings of the asset shown, the selection and an undo history per asset; it refuses to change or
  delete a locked drawing, apart from unlocking it. `DrawingSaveQueue` sends each change at once
  in the background: one request per drawing at a time, only the newest of several changes, a
  failed one tried again after 1, 2, 5 and then every 10 seconds; while a save fails or was refused
  the tool rail shows "not saved", and while a change is unconfirmed closing the window asks first.
  Saves go out with the browser's keep-alive flag, so a save that has started survives closing the
  window. Reading the drawings again lays the unconfirmed changes over the answer of the server.
- The chart page has a tool rail at the left (pointer, the tools, magnet, hide all, undo, redo)
  and, while a drawing is selected, a bar above the chart (colour, width, line style, fill, text of
  a note, lock, delete). Hide all is saved per asset in the chart settings, the magnet for all
  charts in the settings.
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
- A polyline takes points until a double-click on its last point or Enter (Escape drops it); it
  can hide its line and show only the wave labels on its points. A wave label has one anchor
  without a price: the layer draws it at the high (above) or the low (below) of the candle that
  contains its time, so it follows the candle on every timeframe; it is placed on the side of the
  candle the click is on and moved to another candle and side as a whole. The wave label tool
  stays in use after each label, and Web moves its chooser on to the next label (1, 2, 3 ...), so a
  count is clicked label after label. The notation is a small table in the module
  (`primitives/waveNotation.ts`, for drawing) and in Web (`Drawings/WaveLabels`, for the chooser),
  both equal to `WaveNotation` in the Domain; tests keep them equal.
- Wave patterns (DR5): one group button of the rail, as in TradingView, opens the five patterns
  (`Drawings/WavePatterns` in Web) and, at its bottom, the single wave label and the free polyline,
  for counts that fit no pattern; the button shows the entry used last. A pattern is the polyline
  tool with the extras `pattern` (its labels) and `degree`: the module ends it after the start and
  one point per label, and gives the points their labels, highs above and lows below
  (`primitives/wavePatterns.ts`). The record is a plain polyline with point labels, so it is
  reshaped, relabelled and has its line hidden like any polyline; when a point is reshaped, the
  labels take their sides from the prices again. The bar changes the degree of all labels of a
  count at once.
- Fibonacci (DR4): a retracement has level 0 at its second anchor and 1 at its first; an extension
  lays the move from its first to its second anchor, times the ratio, from its third. A level is a
  ratio that is on or off (`FibonacciLevel`); only the levels on are drawn, each from the first to
  the last anchor, left to right, with ratio and price at its left end, as in TradingView. The
  module interpolates the levels in pixels: on a linear scale that is linear in price, on a
  logarithmic scale linear in the logarithm of the price, which is what DR4 asks; the price shown
  is read back from the scale. A new drawing copies all levels, on and off, of the preset chosen
  beside the tool rail; the bar of the drawing switches them, the settings page switches, adds
  and removes those of the presets. Levels stored as plain numbers, before they could be switched
  off, are read as on; script 0005 gave the unchanged default presets their levels that are off.
- Exact anchor times: an anchor that is placed or reshaped on the high or low of its candle, and a
  wave label, gets the time of the candle of the asset's smallest timeframe that made that price
  (Web passes the timeframes with candles in setSeries). The module reads that candle's period in
  the smallest timeframe (one `/api/candles` request per anchor, never while dragging) and reports
  the drawing once, with the exact times. On the timeframe it was drawn on nothing moves; on every
  smaller one the point sits on the candle that made the extreme. A drawing moved as a whole keeps
  the times it was moved to. If the drawing changes during the lookup, its latest state is
  reported and the lookup is dropped; a failed lookup keeps the times.
- Drawings and overlays are positioned by time and price. Switching the timeframe reloads
  candles and indicators only; a time between two candles is drawn at the candle that contains
  it.
- Zoom, pan and crosshair are handled in the browser without a request (NF2). The module keeps
  the number of visible candles and the anchor itself when the timeframe or asset changes (CH3).
- The scenario editor has point tools (SC10) beside the drawing tools of the chart page (SC20).
  A point tool is offered only on the timeframe of its level; while a point is dragged only the
  point follows the pointer, and when it is let go Web asks the Api to snap it and redraws the
  shapes from the answer (chapter 8). The free drawings use the same interaction as on the chart
  page; Web saves them, with their level, into the expert file instead of the workspace. What a
  level holds is drawn on its own timeframe and the lower ones (SC21). Toolbars, the instance selector and the corner display (OV5) are
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
indicator templates (script 0007, name unique whatever the case), drawings, Fibonacci presets, EWTS instances (with the last seen change
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
  and a payload version, so a new drawing type needs no schema change. An indicator carries its
  type code and only the parameters of its type; one a build cannot read is left out, the others
  of the list stay
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
Scenarios:RootPath                           none; the scenario folder; TradeViewer writes only in expert/
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
that is not writable switches off only the editor's writing (SC9 to SC16, SC20); both pages still show the scenarios. EWTS instances are not configured here; the user
registers them (EW1).

---

## 13. Deployment

```text
deploy/Dockerfile          node (chart build) -> dotnet sdk 10.0 (publish) -> aspnet 10.0
deploy/docker-compose.yml  one service

volumes
  /data/marketdata    the Downloader's output root, mounted read-only
  /data/scenarios     the scenario folder, read-write for expert/ (optional)
  /data/app           Storage:DataPath, read-write
```

- One image for linux/arm64 and linux/amd64; the .NET output is framework-dependent and has no
  runtime identifier
- Port 8080, http only, no authentication (NF8)
- Over http in the home network the browser has no secure context: Web APIs that need one
  (`crypto.randomUUID`, the clipboard API, service workers) are not used
- The files of the client (page, chart module, Blazor files) are answered with
  `Cache-Control: no-cache`: the browser asks with the ETag on every load, so after an update it
  never runs an old chart module against a new client
- The container reaches the Downloader (8081) and the EWTS instances (8082-8089, several are normal)
  through `http://host.docker.internal:<port>`; the compose file maps the name with `extra_hosts`
  (`host-gateway`), which the Docker engine on the Pi needs
- The compose file limits the memory to 512 MB (NF9), checks `/health`, and runs the container as
  a configurable user (`user:`), so the expert scenarios written into the scenario folder belong
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
                    read from fixtures (converted expert scenarios of EWTS, test-run
                    files in EWTS's format), expert scenarios written to a temporary
                    folder (atomic, unknown fields kept, never touching test-runs/).
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
O1  EWTS has to build the interface of ewts-interface.md, chapter "Required Interface": the
    drawing files of test-run scenarios (EWTS's Scenarios epic) and the API for the epics EWTS
    Instances, Analysis Overlays, Test Runs and Alerts. The scenario folder and both file
    formats are agreed (decision 0010). The owner aligns this with the EWTS agent by a
    prompt written from that chapter.
```
