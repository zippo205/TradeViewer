# Implementation Plan: Assets and Market Data

## Status

```text
Epic:     Assets and Market Data (P0 - Foundation)
Status:   Plan approved by the owner 2026-11-15 (answers at the end); in work
Branch:   feature/market-data
Next:     Epic Price Chart
Baseline: docs as of commit 42ecca5 (main)
```

Update this section whenever a step is completed.

---

# Why This Epic Comes Now

- Every later epic shows or uses candles: the chart, the watchlists, the indicators, the scenario
  viewer. None of them can be built or tried without assets and a filled candle store.
- The import is the heaviest part of TradeViewer (about 120,000 files for the 13 symbols of the
  frozen copy, 45 million rows for 100 assets). Its speed and the size of the store must be known
  before the chart is built on top of it (NF1, NF4, NF10).
- It is the first epic that talks to another service. The rules for that (results instead of
  exceptions, working without the Downloader, tests on recorded responses) are set here and reused
  for EWTS.

---

# Knowledge Sources

```text
docs/specification.md
    -> 4.1 (AS2-AS5), 4.2 (MD1, MD2, MD5, MD6, MD8), NF4, NF5, NF10; WS5 (default history)
docs/architecture.md
    -> chapter 5 (domain model), 6 (market data), 7 (Downloader integration), 9 (routes, hub),
       11 (workspace tables), 12 (MarketData settings), 14 (testing)
docs/decisions/0003-market-data.md
docs/knowledge/downloader-interface.md
    -> output layout, Binance spot format, control API; last compared 2026-11-11. Checked for this
       plan on 2026-11-15: the Downloader has since added a TradingView source for testing; the
       files and the API TradeViewer uses are unchanged
../Downloader/src/Downloader.Contracts
    -> the seven contract records to copy (Lookup, Subscriptions, Status)
/home/admin/marketdata (frozen copy, read only)
    -> synced from the Pi on 2026-11-15 09:48 UTC at the owner's request (the command is in
       ../Downloader/docs/runbooks/deploy.md: rsync -a --delete home8:/data/marketdata/ ...):
       13 Binance symbols, 126,096 CSV files, 754 MB; BTCUSDT 15m from 2019-01-01, 1d from
       2017-08-17; also the test sources yahoo and tradingview, which this epic does not read
docs/plans/workspace-persistence.md
    -> the pattern for SQLite access, schema scripts and their tests
```

---

# Scope

In scope:

- Domain: `Timeframe`, `AssetClass`, `Session`, `Asset`, `HistorySettings`, `Candle`,
  `CandleSeries`, `CandleFileRules` (file name to period, row validation) and the part of
  `TimeframeAggregator` that builds the current candle (MD8)
- CSV reader for the Binance spot format (12 columns, milliseconds and microseconds, optional
  header row); a file that breaks the rules is reported with file name and line (MD6)
- `marketdata.db`: series, candles, import log, sync state; import of a file replaces its period
  in one transaction; deleting the store loses nothing of the user (MD5)
- Workspace: the assets table (schema script 0002) with the history start per timeframe; the
  default history as a setting (AS3)
- Symbol catalog from `Symbols/binance.json`; add an asset, change its history, remove it,
  reload (rescan) and rebuild it; loading progress per timeframe (AS2-AS5)
- Downloader client with the copied contracts; working without the Downloader (NF5)
- Import service with a background queue, rescan at startup in the background (NF10)
- Sync service on the Downloader's change numbers, aligned to the candle close (MD2)
- The hub `/hubs/workspace` with `CandlesUpdated`, `AssetStatusChanged` and
  `DownloaderStatusChanged`
- Candle endpoint in column format with paging and the current candle (MD1, MD8)
- Web: an assets page (see the proposals) and the default history on the settings page
- A measurement on the frozen copy: import time, size of the store, read time of a candle page

Out of scope (later epics):

- The chart and its use of the candle endpoint and of `CandlesUpdated` (Price Chart)
- The five watchlists, the order of their rows, last close and change (Watchlists: AS1, AS6)
- Aggregating a timeframe the source lacks, gap markers, the Downloader status page (Downloader
  Monitoring: MD3, MD4, DL1-DL4). Binance provides all five timeframes
- Switching off the analysis at EWTS when an asset is removed (EWTS Instances: EW3); the place for
  it in the remove command is marked
- The header format of stocks, ETFs and Forex; the sources yahoo and tradingview
- Moving the history start back for a scenario (Scenario Viewer: SC8)
- Indicators and the in-memory cache of series (see the proposals)

---

# Steps

```text
 1  Domain: Timeframe (codes, duration, period start), AssetClass, Session, Candle, CandleSeries,
    Asset, HistorySettings; CandleFileRules for the Binance layout (name -> timeframe and period,
    row rules)                                                                                    done
 2  CSV reader: ICandleFileReader, Binance spot (columns 0-5, unit per value, header row skipped),
    invalid data with file and line; fixtures cut from the frozen copy plus broken files          done
 3  Candle store: shared schema runner for both databases, MarketDataDatabase, tables series,
    candles, import_log, sync_state; ICandleStore (replace a period, read a page, delete an
    asset, delete before a date); a damaged or unknown store is set aside and rebuilt            done
 4  Workspace: script 0002 (assets with history per timeframe), IAssetRepository; default history
    in the settings; ISymbolCatalog on Symbols/binance.json                                       done
 5  Import: file list of an asset from its history start, first import (newest files first),
    rescan against the import log, errors into the log; background queue with one worker and
    progress; rescan of all assets at startup in the background                                  done
 6  Downloader: copied contracts, IDownloaderClient / DownloaderClient (typed HttpClient, results
    for problem details, timeouts and network errors), tests on recorded responses               done
 7  Assets feature: add (catalog -> lookup -> subscribe -> store -> import), change history,
    remove, reload, rebuild, list and status with the loading progress; the mode without the
    Downloader; endpoints /api/symbols, /api/assets, /api/settings                               open
 8  Sync: due times (pure, TimeProvider), MarketDataSyncService (status -> changes -> import,
    complete == false -> rescan, subscriptions and unsubscriptions that failed are sent again,
    unreachable logged once), sync_state                                                         open
 9  Realtime: hub /hubs/workspace, IWorkspaceNotifier in the Api; events from import and sync     open
10  Candles: current candle (TimeframeAggregator), GetCandlesQuery, endpoint with to and limit,
    column arrays, "partial"                                                                     open
11  Web: assets page (add with symbol search, status and progress, history, reload, rebuild,
    remove with confirmation), default history on the settings page, hub client                  open
12  Measurement on the frozen copy (13 symbols): import time, store size, page read time; Docker
    build; architecture, README and runbooks updated; then, with the owner's go-ahead, the Pi
    with the Downloader switched on                                                              open
```

Each step has its tests: unit tests for the Domain and the pure rules (file names, row rules, due
times, current candle, loading progress), handlers with in-memory ports; integration tests with
temporary SQLite files, CSV fixtures in the Downloader's layout and recorded Downloader responses.
No test contacts the Downloader or reads the live output root.

---

# Decisions of This Epic

Proposed with the plan; the owner raised no objection on 2026-11-15. They are written into the
architecture when the step that needs them is done, and the owner may still change any of them.

- **An assets page until the watchlists exist.** The specification puts assets into watchlists
  (AS1), but that epic comes after the chart. This epic gets a plain page `/assets`: one table of
  all assets with source, status and progress per timeframe, and the actions add, history, reload,
  rebuild and remove. The Watchlists epic turns it into the five lists; the actions stay.
- **The hub is built here.** MD2 and the loading progress (AS4) need a signal from the server. The
  hub of chapter 9 is added with the three events this epic raises; the assets page is its first
  user, the chart follows.
- **The candle store is a cache and treated like one.** `synchronous=NORMAL` with WAL (a power
  loss can lose the last import, which the rescan repeats). A store that is damaged or has a
  newer schema than the build is renamed (`marketdata.db.broken-<time>`) and a new one is filled
  from the files; the start is never stopped by it, unlike workspace.db. The store is not checked
  with `quick_check` at the start: on 3 GB that would cost the 15 seconds of NF10. Damage shows as
  an error when the store is used; "rebuild" or deleting the file repairs it.
- **Storage format.** Times as Unix seconds (INTEGER), prices and volume as REAL (`double`, as in
  the Domain). One schema runner for both databases, so marketdata.db gets numbered scripts too.
- **Import order.** Per asset 1w and 1d first, then 4h, 1h, 15m; within a timeframe the newest
  files first. So a timeframe can be charted after seconds (AS4) and the long 15m history fills in
  behind. One import worker for the whole application: SQLite has one writer, and the Pi stays
  responsive.
- **Loading progress** per timeframe is the share of the wanted files that are imported, combined
  with the Downloader's coverage (`coveredFrom` against `wantedFrom`, pending jobs) while it still
  downloads. Without the Downloader only the import counts.
- **Default history** (AS3: 1d and 1w complete, 4h two years, 1h one year, 15m 90 days) is one
  setting `defaultHistory` in the workspace: per timeframe either "complete" or a number of days.
  The start date of an asset is fixed when it is added (today minus the days) and stored with the
  asset; "complete" is stored as no date and sent to the Downloader as 1970-01-01, which it clips
  to the first day the source has (without a date its own defaults would apply, e.g. 4h two years).
- **Adding fails as a whole.** The asset is stored only after the Downloader accepted the
  subscription (422 and 503 are shown with the Downloader's message, AS2). Only a later change of
  the history that fails is kept and sent again by the sync.
- **Removing works without the Downloader.** The asset and its data are deleted at once; when the
  unsubscription fails, it is remembered in workspace.db and sent again by the sync, so the
  Downloader does not keep updating a symbol nobody wants. (It must be the workspace: the candle
  store can be deleted.)
- **Changing the history to a later start** deletes the candles before it from the store (chapter
  6) but never a drawing: drawings refer to time and price only.
- **Symbol file.** `Symbols/binance.json` is a copy of the frozen `symbols.json` with its 13
  symbols, embedded in Infrastructure. With the Downloader on, the lookup still decides (AS2).
- **No series cache yet.** Chapter 6 names an in-memory cache of the most recently used series.
  It is added in the Price Chart epic if the measurement of step 12 shows that a page read from
  SQLite misses NF1; unmeasured, it would only be a place for stale data.
- **Limits of the candle endpoint.** `limit` defaults to 2,000 and is capped at 5,000; `to` is
  exclusive, in Unix seconds; an unknown asset is 404, a timeframe the asset does not have 422.
- **Development and the Pi.** Development and the tests stay on the frozen copy with the
  Downloader off. The sync against a real Downloader is proved in step 12: on WSL with the local
  Downloader if the owner starts it, and on the Pi, where switching it on subscribes assets under
  the client `tradeviewer`. Both need the owner's go-ahead.

---

# Answers of the Owner (2026-11-15)

1. The plain assets page is fine as the interim place for AS2 to AS5.
2. The Downloader on the Pi already holds subscriptions for the 13 symbols (of another client).
   For the proof in step 12 TradeViewer on the Pi adds BTCUSDT, which creates its own subscription
   under the client `tradeviewer`.
3. Development continues on WSL on the frozen copy, which was synced with the Pi first (see
   Knowledge Sources). Not answered yet: whether the measurement of step 12 imports the 13 symbols
   with complete history; it is asked again before that step (the WSL disk had 1.7 GB free on
   2026-11-15, the store of that measurement is estimated at 0.4 GB).

---

# Measurements on the Way

```text
2026-11-15  step 2  all 121,277 Binance files of the frozen copy read by the reader: 2,806,898
                    candles, no invalid row, 20 s (WSL)
2026-11-15  step 3  BTCUSDT, complete history in five timeframes: 12,117 files, 365,824 candles
                    imported in 1.7 s (200 files per transaction, files in the page cache), store
                    40 MB; first page of 2,000 15m candles 2 ms, an older page of 5,000 4 ms (WSL)
```

# Notes on the Way

- Step 6: the responses in tests/fixtures/downloader are written from the examples of
  downloader-interface.md, not recorded from a running Downloader (the tests must not contact it,
  and the agent does not call the Pi's Downloader without being asked). The proof against the
  real service is part of step 12. `GetSubscriptionsAsync` and `GetServiceStatusAsync` of chapter 7
  come with the epic Downloader Monitoring, which is their first user.
