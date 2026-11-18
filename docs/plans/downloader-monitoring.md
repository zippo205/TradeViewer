# Implementation Plan: Downloader Monitoring

## Status

```text
Epic:     Downloader Monitoring (P1 - Workstation)
Status:   Step 1 done 2026-11-18; real check when the Pi is reachable
Branch:   feature/downloader-monitoring
Next:     step 2
Baseline: docs as of commit db8d6db (main)
```

Update this section whenever a step is completed.

---

# Why This Epic Comes Now

- It is the last epic of V1 whose dependencies are met: Scenario Viewer and Scenario Analysis
  wait for the drawing files of EWTS (architecture, open point O1).
- It depends on Assets and Market Data only; the sync, the Downloader client, the copied
  contracts, the sync state and the gap markers of the chart exist.

---

# Knowledge Sources

```text
docs/specification.md
    -> DL1-DL4 (status page, header dot, coverage per asset, subscriptions, unreachable since),
       MD4 (gaps also in the asset's status), CP1 (the timeframe on an asset change), NF5
docs/decisions/0008-timeframes-by-trading-hours.md
    -> MD3 moved to the stock and Forex sources; the next larger timeframe on an asset change
docs/architecture.md
    -> chapter 4 (feature Downloader), 6 (flow, sync, gaps from the sync state), 7 (IDownloaderClient:
       GetSubscriptionsAsync and GetServiceStatusAsync come with this epic), 9 (/api/downloader/*,
       hub DownloaderStatusChanged), 10 (gap markers)
docs/knowledge/downloader-interface.md
    -> Control API: GET status, GET subscriptions, GET status/{source}/{symbol}; reasons of gaps
../Downloader/src/Downloader.Contracts
    -> the records to copy (read only)
```

---

# Found While Planning

- The copy of `ServiceStatusResponse` is out of date: the Downloader added `catalogs` (per source:
  refreshed at, symbol count, last error). It is copied again unchanged (step 1), and the
  interface document gets the field.
- `MarketDataSync` knows whether the Downloader is reachable (`DownloaderReachable`), not since
  when (DL4), and keeps it in memory only. The last symbol status per asset is in `sync_state`
  with the time it was read, so coverage and gaps can be shown when the Downloader is away.
- `TimeframeAggregator` builds only the current candle (MD8). Binance delivers all five
  timeframes, so MD3's built timeframes have no case among today's assets; the first is yahoo's
  missing 4h, which waits for the header format and trading sessions (roadmap, "Later").

---

# Scope

In scope:

- DL1 status page `/downloader`: reachable or not, version, started at, last cycle (start, end,
  result, jobs, message), next cycle, queue sizes, every source with its state, last success, last
  error and retry time, the symbol catalogs, the consistency check
- DL1 header dot: green, yellow (a source is rate limited, or the last cycle was partial or
  failed), red (not reachable), grey (switched off in the configuration); a link to the page
- DL2 per asset of the workspace: coverage per timeframe (wanted from, covered from and to),
  pending jobs, gaps with reason, last change, last error; as last read when the Downloader is
  away, with the time it was read
- DL3 all subscriptions of the Downloader, merged per symbol, the clients of each; TradeViewer's
  own client marked, the others read-only (the page changes nothing anyway)
- DL4 "not reachable since ..." on the page and in the dot's title; charts keep working (exists)
- MD4 confirmed gaps (`missingUpstream`) in the asset's status on the assets page
- CP1 another asset keeps the timeframe, otherwise the next larger one it has, otherwise the
  largest smaller one (decision 0008; today it takes the largest)

Out of scope:

- Any action at the Downloader from the page (DL1: it only shows data)
- The history of update cycles (`GET /api/v1/cycles`): DL1 names the last and the next one
- Sources other than binance (header format, sessions: roadmap "Later")
- MD3, building timeframes a source lacks: with the stock and Forex sources (decision 0008)

---

# Design

## Api

```text
GET /api/downloader/status                    the Downloader's state as TradeViewer sees it now
-> { "enabled": true, "reachable": false, "unreachableSince": "...", "checkedAt": "...",
     "service": { version, startedAt, lastCycle, nextCycleAt, queue, sources[], catalogs[],
                  consistencyCheck } | null,
     "light": "green" | "yellow" | "red" | "off" }
GET /api/downloader/subscriptions             all clients, merged per symbol; ownClient names TradeViewer's
GET /api/downloader/assets/{source}/{symbol}  coverage per timeframe, pending jobs, gaps, last change,
                                              last error, import errors; "readAt" and "live": false when
                                              it is the sync's last status because the Downloader is away
```

- Feature `Downloader` (architecture chapter 4): `GetDownloaderStatusQuery`,
  `GetDownloaderSubscriptionsQuery`, `GetDownloaderAssetQuery`, handlers, contracts in
  `TradeViewer.Contracts/Downloader` (TradeViewer's own records, not the copied ones).
- The status asks the Downloader on request (architecture chapter 7), with the client's timeout.
  When it does not answer, the answer says so and since when; that is not an error (DL4): 200 with
  `reachable: false`. Switched off: `enabled: false`, nothing is asked.
- "Unreachable since": a port `IDownloaderReachability` (Abstractions/MarketData) that the sync
  and the status query both write and read: the first failure after a success sets the time, a
  success clears it. In memory: after a restart the first poll finds it out again.
- The light is decided in one place (`DownloaderStatusRules`, which already holds the shared
  rules of the Downloader's status), so dot and page agree.
- `IDownloaderClient` gains `GetServiceStatusAsync` and `GetSubscriptionsAsync`, mapped to
  Application models like the other calls; the copied records stay in Infrastructure.

## Web

- Page `Pages/Downloader.razor` at `/downloader`, linked from the header: three sections (service,
  assets, subscriptions). It reads the status and the subscriptions when opened and every
  10 seconds while open (question 2), and an asset's detail when it is unfolded.
- `Layout/DownloaderLight.razor` in the header: the dot with a title ("reachable, last cycle ok
  at ...", "not reachable since ..."); reads the status at start, on the hub event
  `DownloaderStatusChanged` and every 60 seconds (question 2).
- Assets page: a badge "n gaps" per asset with confirmed gaps, the gaps in its folded details
  (from the sync state, the same source as /api/gaps).

---

# Steps

```text
1  Contracts and client: copy ServiceStatusResponse again (catalogs), interface document;
   GetServiceStatusAsync, GetSubscriptionsAsync with their models; recorded answers as fixtures
   (from the Downloader's own tests and documentation, not from a live call); client tests       done
2  Reachability since (DL4): the port, written by the sync and the status query; tests          open
3  Feature Downloader: the three queries, contracts, endpoints, the light rule; unit and
   integration tests with the stub Downloader (reachable, unreachable, switched off, 404)       open
4  Web: the status page, the header dot, the gaps on the assets page; browser checks against
   the stub of the integration tests and the local container                                    open
5  CP1: the timeframe on an asset change as a rule with unit tests (Web), used by the watchlist
   panel; browser check                                                                         open
6  Check against the real Downloader (question 3), Docker image, architecture (chapters 4, 6, 7,
   9, 10), plan result; then the owner's look                                                    open
```

---

# Result of Step 1 (2026-11-18)

- `ServiceStatusResponse` copied again from `Downloader.Contracts` (the other seven copies are
  unchanged, compared file by file); the interface document has `catalogs` and `GET subscriptions`.
- Models (Abstractions/MarketData): `DownloaderServiceStatus`, `UpdateCycle`, `DownloaderQueue`,
  `SourceState`, `CatalogState`, `ConsistencyCheck`, `MergedSubscription`, `MergedTimeframe`;
  `TimeframeStatus` gained `LastChangedAt` (DL2's "last change") as a trailing optional field, so a
  status the sync stored before is still read (tested on a row in the old shape).
- `IDownloaderClient.GetServiceStatusAsync` and `GetSubscriptionsAsync`; timeframes TradeViewer does
  not know (1M) are left out, the rest smallest first; switched off, nothing is asked.
- Fixtures: the Downloader documents no complete answer of `status` or `subscriptions`, so
  `service-status.json`, `service-status-first-start.json` and `subscriptions-all.json` were
  written from its contract records (with a field it may add later, which is ignored); they are
  checked against the real Downloader in step 6.
- Tests: 6 client tests, 1 store test.

---

# Proposals (accepted unless the owner objects)

1. **No cycle history**: the page shows the last and the next cycle as DL1 says; `/cycles` is not
   called.
2. **Coverage of the workspace's assets only** (DL2 says "per asset"); the subscriptions list
   (DL3) shows every symbol of every client, without coverage, so other clients' symbols are
   visible but not inspected.
3. **The dot is grey when the Downloader is switched off** in the configuration, with the title
   saying so; DL1 names three colours for a configured Downloader.
4. **A failed status call is not an error of the page**: the last known state is kept on the page
   and marked with the time it was read.

---

# Questions for the Owner

Answers of 2026-11-18: (1) decision 0008: Forex builds 4h from 1h, stocks and ETFs have no 4h (the
button disabled), another asset takes the next larger timeframe; MD3 moves to the stock and Forex
sources, the timeframe rule comes into this epic; EWTS is told that 4h is no context or setup
timeframe for an asset with exchange hours. (2) the refresh as proposed. (3) the owner says when the
Pi can be reached; until then recorded answers.

1. **MD3 (building a timeframe the source lacks).** Binance delivers all five timeframes, so today
   nothing would use it; the first case is yahoo's 4h, which also needs trading sessions. Proposed:
   build it now for continuous sessions (crypto) in the Domain and the candle reader, tested with
   series that leave a timeframe out, and the sessions with the stocks epic. Or move MD3 out of
   this epic into the one that brings the stock sources?
2. **Refresh.** The page every 10 seconds while open, the header dot every 60 seconds and at once on
   the hub event. Fine?
3. **The real check.** The Downloader runs on the Pi; from WSL the development instance can read
   its status with the client `tradeviewer-dev` (all calls of this epic only read). That needs you
   at home with the Pi reachable. Until then steps 1 to 5 are built and tested against recorded
   answers. Fine?
