# EWTS Interface

Everything TradeViewer needs from EWTS (../EWTS). How TradeViewer uses it is in
docs/architecture.md, chapter EWTS Integration; the decisions behind it are in
docs/decisions/0004-ewts-integration.md.

This document has two parts:

1. **Existing Interface**: what the EWTS API offers today, read from its code on 2026-11-11
   (branch `feature/setup-quality`).
2. **Required Interface**: what TradeViewer needs and EWTS does not have yet: an API (v1) and
   the scenario drawing files. It is a proposal
   for EWTS's Workbench epic and becomes binding when both repositories have accepted it.
   EWTS owns the interface; once it is built, EWTS's code is the original and this chapter is
   updated to match it.

---

# 1. Existing Interface

## General

```text
Address          http://localhost:5003 (development, http only)
Authentication   none
Versioning       none; all routes start with /api/
Format           JSON, property names camelCase, enumeration values as names (e.g. "Bullish")
Times            ISO 8601; a time without offset is UTC
Timeframes       M15, H1, H4, D1, W1 (not the Downloader's codes)
Assets           the symbol only, e.g. BTCUSDT; the source is fixed per instance
                 (MarketData:Source), the assets come from the instance's configuration
                 (MarketData:Assets)
OpenAPI          none; src/EWTS.Api/EWTS.Api.http holds example requests
Health           no health endpoint
Push             none
```

Errors are problem details:

```text
400  validation problem with the fields
404  unknown symbol or id, no data
409  conflict (journal rules)
422  invalid market data
```

Most analyses take `asOf` (optional): the instant of the analysis; default is the close of the
last stored candle. Every request recalculates; EWTS keeps no analysis state apart from its
journal.

## Analysis (stateless, GET)

```text
/api/market-structure/{symbol}/{timeframe}?from=&to=      swings, BOS / CHOCH, trend, support and resistance zones
/api/multi-timeframe-context/{symbol}/{timeframe}?asOf=   bias and structure of the higher timeframes
/api/elliott-wave/{symbol}/{timeframe}?asOf=              preferred and alternate counts per timeframe
/api/fibonacci/{symbol}/{timeframe}?asOf=                 levels per timeframe, confluence zones
/api/volume/{symbol}/{timeframe}?asOf=                    volume analysis
/api/conviction/{symbol}/{timeframe}?asOf=                conviction analysis
/api/setups/{symbol}/{timeframe}?asOf=                    wave 2 -> wave 3 setups with score, grade, targets
/api/setups/{symbol}/{timeframe}/watch?from=&asOf=        setup watch on the trigger timeframe (D1 or H4 context)
/api/setups/{symbol}/{timeframe}/trade?from=&asOf=        staged trade of the setups detected at from
/api/risk/{symbol}/{timeframe}?accountSize=&riskPercent=&asOf=    stop, position size, reward-risk, decision
```

What these answers contain that can be drawn (all positions are time + price):

```text
elliott-wave      timeframes[] { timeframe, role, preferred, alternates[], oneTwos[] }
                  count { pattern, direction, lastWave, nextWave, isComplete, invalidation { level, side, reason },
                          confidence { score, ... }, waves[] { label, start, end }, innerWaves[], formingWave }
                  start / end { timestamp, price, type }
market-structure  swings[] { timestamp, price, type, label, degree, isConfirmed }
                  events[] { type, direction, timestamp, close, brokenSwing }
                  supportZones[] / resistanceZones[] { lower, upper, touches, strength }
fibonacci         timeframes[].levels[] { kind, ratio, price, lower, upper, source }
                  support[] / resistance[] { lower, upper, side, strength, score, factors[] }
setups            setups[] { type, direction, status, grade, score, waveOne, waveTwo, entryReference,
                             invalidation, targets[] { zone }, obstacles[] { zone } }
setups/watch      watches[] { status, triggerLevel, plannedStop, trigger { time, close }, events[] { time, kind } }
```

## Journal and Trades

```text
POST /api/journal/assessments              record a risk assessment
POST /api/journal/trades                   record a trade against an approved assessment
POST /api/journal/trades/{id}/stop         record a stop move
POST /api/journal/trades/{id}/add          record an add
POST /api/journal/trades/{id}/reduce       record a partial exit
POST /api/journal/trades/{id}/close        record the exit
GET  /api/journal/entries?symbol=&kind=&from=&to=&limit=
GET  /api/journal/entries/{id}
GET  /api/journal/trades?symbol=&status=
GET  /api/journal/trades/{id}
GET  /api/journal/statistics?symbol=&from=&to=
GET  /api/trades?symbol=&status=           trade overview
GET  /api/trades/exposure?accountSize=     open risk
GET  /api/trades/{id}/monitor?asOf=        management plan of a trade
```

TradeViewer does not use these today (architecture, open point O4).

## Backtest

`POST /api/backtests`

```json
{ "symbol": "BTCUSDT", "timeframe": "D1", "from": "2024-01-01", "to": "2025-12-31",
  "accountSize": 10000, "riskPercent": 1,
  "overrides": { "minSetupScore": null, "maxStopAtr": null, "minRewardRisk": null,
                 "feePercent": null, "slippagePercent": null, "longOnly": null } }
```

- `timeframe` is the context timeframe: D1 or H4. At most 20,000 candles per run.
- The request runs **synchronously** and returns the whole report; nothing is stored, there is
  no progress and no list of earlier runs.

Report (`BacktestReportDto`):

```text
symbol, timeframe, from, to, accountSize, riskPercent
settings      minSetupScore, maxStopAtr, minRewardRisk, longOnly, feePercent, slippagePercent
funnel        instants, setups, approved, watches, triggered, cancelled, expired, watching, opened,
              skippedTradeOpen, skippedIdeaWatched, skippedLongOnly, openAtEnd
statistics    gross, net, profitFactor, longestLosingStreak, maxDrawdownR, exits, totalFeesR,
              averageHoldingCandles, byGrade[], byDirection[], byPath[], byScoreBand[],
              equityCurve[] { closedAt, r }
trades[]      watchStartedAt, openedAt, direction, grade, score, path, entryPrice, stop, exit, closedAt,
              grossR, netR, netProfitLoss, maxFavourableR, events[] { time, kind, rule, price, share, description }
watches[]     startedAt, direction, grade, score, path, triggerLevel, plannedStop, status, reason, endedAt,
              points[] { label, time, price }
skipped[]     asOf, direction, waveOneStart, waveOneEnd, reason
```

## What Is Missing for TradeViewer

```text
Requirement (specification)              Missing in EWTS today
EW1, EW2  instance status                no health, version or status endpoint
EW3       observed assets                assets are fixed in the configuration; no add or remove
EW4, EW5  overview, details, history     nothing is observed or stored; every analysis is on request
OV1-OV5   overlays                       analysis answers exist, but no drawings as data, and no
                                         signal that an analysis changed
BT1-BT6   test runs                      one synchronous request; no progress, cancel, list or storage
AL1-AL4   alerts                         no alerts, no channels, no settings
SC1-SC9   scenario viewer                the scenarios exist as YAML with a Pine overlay; the shape
                                         model exists in tools/scenario-to-pine.py but is not
                                         written as data
```

The scenario viewer is the only one of these that needs no running EWTS instance.

---

# 2. Required Interface

Status: proposal (2026-11-11). Open for EWTS to change in detail; the capabilities are what
TradeViewer's specification needs.

## Conventions

The same as the Downloader's control API, so TradeViewer handles both alike:

- All paths start with `/api/v1/`. Within v1 changes are only additive; a client ignores
  unknown fields.
- JSON, camelCase names and enumeration values. Times ISO 8601 UTC with `Z`.
- Errors are problem details: 400 invalid input, 404 unknown asset or id, 409 conflict,
  422 invalid data, 503 a dependency (Downloader) is unreachable.
- No authentication (trusted home network).
- The request and response records live in a project `EWTS.Contracts` without dependencies,
  which TradeViewer copies unchanged, as it does with `Downloader.Contracts`.
- Timeframes are the Downloader's codes (`15m`, `1h`, `4h`, `1d`, `1w`) in the API and in the
  scenario files; EWTS converts to its own enumeration at that boundary. The setup and context
  timeframes are `1d` or `4h`.
- The existing unversioned routes are not used by TradeViewer and may stay as they are.

```text
GET    /health                                 liveness
GET    /api/v1/status                          identity and state of the instance
GET    /api/v1/profiles                        parameter profiles an asset or a test run can use
GET    /api/v1/assets                          observed assets, one overview row each
PUT    /api/v1/assets/{source}/{symbol}        observe an asset or change its settings
DELETE /api/v1/assets/{source}/{symbol}        stop observing: 204, 404 if not observed
GET    /api/v1/assets/{source}/{symbol}        details: count, setups, setup history
GET    /api/v1/assets/{source}/{symbol}/drawings  analysis drawings as data
POST   /api/v1/test-runs                       start a test run: 202
GET    /api/v1/test-runs?source=&symbol=             test runs, newest first, without reports
GET    /api/v1/test-runs/{id}                  state, progress and, when finished, the report
POST   /api/v1/test-runs/{id}/cancel           cancel a running test: 204, 409 if it is not running
DELETE /api/v1/test-runs/{id}                  delete a finished run and its scenarios: 204, 409 while it runs
GET    /api/v1/alerts?after={n}&limit={n}      alerts after alert number n, oldest first
GET    /api/v1/alert-settings                  channels and kinds
PUT    /api/v1/alert-settings                  replace the settings
GET    /api/v1/changes?after={n}               what changed after change number n
```

## Status

`GET /api/v1/status`

```json
{ "instanceId": "pi-binance", "state": "running", "version": "2.0.0", "startedAt": "2026-11-11T06:00:00Z",
  "sources": ["binance"], "observedAssets": 13, "lastAnalysisAt": "2026-11-11T09:15:40Z",
  "lastChange": 9120,
  "downloader": { "reachable": true, "since": "2026-11-11T06:00:05Z" } }
```

`instanceId` is `Journal:InstanceId`. `state` is `running` as in the Downloader's status. `sources`
lists the sources the instance handles (today one). Every asset is addressed by `source` and
`symbol`, as in the Downloader. A source the instance does not handle is answered with 422 on
PUT and POST and with 404 on the other requests, so an instance can become source independent
later without a change of the interface. TradeViewer offers only matching instances.

## Observed Assets

`PUT /api/v1/assets/binance/BTCUSDT`

```json
{ "setupTimeframe": "1d", "historyFrom": "2019-01-01", "profile": "default" }
```

- Idempotent. Adding subscribes the asset at the Downloader under EWTS's client name and starts
  the observation; a change of the settings analyses the asset again.
- 422 when the Downloader does not know the symbol, 503 when the Downloader is unreachable;
  nothing is stored in both cases.
- The observation survives a restart of EWTS.

`GET /api/v1/assets`

```json
{ "assets": [
  { "source": "binance", "symbol": "BTCUSDT", "displayName": "BTC/USDT", "setupTimeframe": "1d",
    "historyFrom": "2019-01-01", "profile": "default",
    "state": "watching", "lastAnalysisAt": "2026-11-11T09:15:40Z", "lastError": null,
    "bestSetup": { "direction": "long", "grade": "B", "score": 71.5 },
    "dataQuality": "complete" } ] }
```

`state`: `pending` (not analysed yet), `noSetup`, `setup`, `watching`, `planOpen`, `error`.

`GET /api/v1/assets/binance/BTCUSDT`: the overview row plus

```text
count           timeframe, pattern, direction, lastWave, nextWave, invalidation level, confidence score
setups[]        id, foundAt, direction, status, grade, score, entry (trigger level), add, stop,
                targets[], reason
setupHistory[]  the same fields for setups that are no longer current, with endedAt and outcome
```

## Drawings

`GET /api/v1/assets/BTCUSDT/drawings`

The current analysis of the asset as shapes a chart can draw without knowing what they mean.
Every position is a time and a price, so the shapes fit every timeframe.

```json
{ "source": "binance", "symbol": "BTCUSDT", "analysedAt": "2026-11-11T09:15:40Z", "asOf": "2026-11-11T00:00:00Z",
  "groups": [
    { "key": "count.preferred.1d", "title": "Preferred count 1d", "kind": "count", "timeframe": "1d",
      "shapes": [
        { "type": "line",  "role": "bullish",
          "points": [ { "time": "2026-08-05T00:00:00Z", "price": 49000 },
                      { "time": "2026-09-14T00:00:00Z", "price": 66500 } ] },
        { "type": "label", "role": "bullish", "time": "2026-09-14T00:00:00Z", "price": 66500,
          "text": "(1)", "side": "above" },
        { "type": "level", "role": "invalidation", "price": 49000,
          "from": "2026-08-05T00:00:00Z", "to": null, "text": "Invalidation" } ] },
    { "key": "setup.1", "title": "Setup long B", "kind": "setup", "timeframe": "1d",
      "shapes": [
        { "type": "zone",   "role": "target", "lower": 78000, "upper": 79500,
          "from": "2026-11-01T00:00:00Z", "to": null, "text": "Target 1" },
        { "type": "marker", "role": "entry", "time": "2026-11-03T12:00:00Z", "price": 61200,
          "text": "Trigger" } ] } ] }
```

```text
group.kind    count, alternateCount, setup, plan, fibonacci, structure
              scenario files: waves, lowerDegree, levels, trade
shape.type    line          points[] (two or more), drawn as connected segments
              label         time, price, text, side (above, below)
              level         price, from, to (null: open to the right), text
              zone          lower, upper, from, to (null: open to the right), text
              marker        time, price, text
              verticalLine  time, text
shape.role    neutral, bullish, bearish, wave, context, lowerDegree, invalidation, entry, add,
              stop, target, exit, support, resistance
optional on every shape
              detail        a longer text shown when the user points at the shape
              style         solid (default), dotted, dashed
              weight        normal (default), strong
```

- TradeViewer chooses colours from `role`; EWTS sends no colours.
- `key` is stable for the same thing across analyses, so a chart keeps the user's choice of
  hidden groups.
- New values of `kind`, `type` and `role` may be added; a client ignores shapes it does not
  know.

## Scenario Files

Decision: docs/decisions/0006-scenario-viewer.md. The scenario format itself is described in
../EWTS/docs/knowledge/scenarios.md; TradeViewer does not read it.

Next to every scenario (`<id>.yaml`, `<id>.pine`) EWTS writes a drawing file `<id>.json`. It is
generated by the tool that writes the Pine overlay, from the same shape model (`overlay()` in
`tools/scenario-to-pine.py`), so both always show the same.

```text
<Scenarios:RootPath>/                       e.g. ../EWTS/docs/scenarios, read only for TradeViewer
├── expert/<id>.json                        expert scenarios
├── analysis/<case kind>/<id>.json          analysis cases, e.g. analysis/initial-stop/
└── test-runs/<run id>/<id>.json            scenarios of a test run, written by the instance
```

The first two folders are in the EWTS repository and written by its tools; `test-runs` is
written by a running instance. For TradeViewer to see both, the instance's scenario output and
the repository folder must lie below one root (e.g. `test-runs` as a mounted folder).

- TradeViewer reads every `*.json` below the root; the folder path is shown as the kind. A
  file `<id>.expert.json` is not a scenario of its own when `<id>.json` exists beside it: it is
  the analysis of the user on that scenario (Expert Analysis Files).
  Other files (`.yaml`, `.pine`, `.md`) are ignored.
- Files are written atomically (`<name>.json.part`, then renamed).
- A file TradeViewer cannot read (unknown `version`, broken JSON) is listed with its error
  instead of being hidden.

```json
{ "version": 1,
  "id": "ethusdt-h4-2026-08-long", "source": "binance", "symbol": "ETHUSDT",
  "contextTimeframe": "4h", "direction": "long", "verdict": "trade-win", "draft": false,
  "reason": "Wave 1 and a zigzag wave 2 at 73.3 % (log) by wick. ...",
  "notes": "The prices here are the Binance wick extremes. ...",
  "knownAt": "2026-08-19T14:45:00Z", "chart": null,
  "from": "2026-08-01T16:00:00Z", "to": "2026-11-11T00:00:00Z",
  "generatedAt": "2026-11-11T10:02:11Z",
  "figures": [ { "name": "Grade", "value": "B" }, { "name": "Score", "value": 71.5 },
               { "name": "Result (R)", "value": 0.45 }, { "name": "Exit", "value": "TargetThenStop" } ],
  "groups": [
    { "key": "waves", "title": "Waves", "kind": "waves", "timeframe": "4h",
      "shapes": [
        { "type": "line", "role": "wave", "weight": "strong",
          "points": [ { "time": "2026-08-01T16:00:00Z", "price": 1822.06 },
                      { "time": "2026-08-07T12:00:00Z", "price": 1943.02 } ] },
        { "type": "label", "role": "wave", "time": "2026-08-07T12:00:00Z", "price": 1943.02,
          "text": "1", "side": "above", "detail": "1: 1943.02 at 2026-08-07 12:00 UTC" } ] },
    { "key": "levels", "title": "Levels (log)", "kind": "levels", "timeframe": "4h",
      "shapes": [
        { "type": "level", "role": "target", "price": 2056.77, "style": "dotted",
          "from": "2026-08-11T12:00:00Z", "to": "2026-11-11T00:00:00Z", "text": "161.8 % (EM11)" },
        { "type": "verticalLine", "role": "neutral", "style": "dotted",
          "time": "2026-08-19T14:45:00Z", "text": "known_at" } ] } ] }
```

- `version` is the format version: additive changes keep it, an incompatible change raises it.
- `source` is the source the candles come from (the `MarketData:Source` of the tools).
- `from` and `to` are the time span of the drawing; TradeViewer positions the chart on it.
- All times are UTC, whatever `timezone` the scenario file uses.
- `groups` and `shapes` use the vocabulary of Drawings above. The groups of a scenario are
  `waves`, `lowerDegree`, `levels` and `trade`, as the switches of the Pine overlay.
- `verdict`: `trade-win`, `trade-loss`, `no-trade`; `draft` is true for a `# DRAFT` verdict.
- `figures` (optional) are named values to show as columns of a list and in the panel of the
  viewer: what the engine measured and how the trade ended. A value is a text or a number.
  TradeViewer shows them in the given order and sorts by them; it does not interpret them.
  The scenarios of one test run carry the same names.

## Expert Analysis Files

Decision: docs/decisions/0007-scenario-analysis.md. TradeViewer writes two kinds of file into
the scenario root; EWTS reads them and merges them into the expert analysis section of its
scenario.

```text
<id>.expert.json            the analysis of the user on the scenario <id>.json beside it
expert/<id>.expert.json     a new expert scenario made in TradeViewer (no <id>.json beside it)
```

- The format is that of a scenario file (above): head, `verdict`, `reason` and `notes` (the
  remarks of the user), `groups` with shapes. The id in the file name is the reference to the
  scenario; the groups hold only the analysis of the user.
- Besides `groups` (the shapes, for EWTS and for display) the file holds `drawings`: the
  drawing records of TradeViewer's tools, so the user can edit the analysis again. EWTS ignores
  `drawings`. TradeViewer writes both in one go.
- Written atomically by TradeViewer, which is their only writer. EWTS never writes or deletes
  them.

## Test Runs

`POST /api/v1/test-runs`: the body of today's `POST /api/backtests` plus `"kind": "backtest"`
(later `"walkForward"`) and an optional `"profile"`. The timeframe is a code (`1d`, `4h`). Answer 202:

```json
{ "id": "pi-binance-17", "state": "queued" }
```

The run id is `<instanceId>-<n>` (e.g. `pi-binance-17`): the instance's unique `instanceId` and a
run number that only increases and survives restarts. Ids of different instances therefore never
collide in the shared scenario folder. The `instanceId` of every instance must be unique within
all instances that write to the same scenario root.

`GET /api/v1/test-runs/{id}`

```json
{ "id": "pi-binance-17", "kind": "backtest", "state": "running",
  "request": { "source": "binance", "symbol": "BTCUSDT", "timeframe": "1d", "from": "2024-01-01", "to": "2025-12-31" },
  "createdAt": "2026-11-11T09:20:00Z", "startedAt": "2026-11-11T09:20:01Z", "finishedAt": null,
  "progress": { "done": 212, "total": 731 }, "error": null,
  "statistics": { }, "scenarioFolder": "test-runs/pi-binance-17",
  "report": null }
```

- `state`: `queued`, `downloadingData` (waiting for the Downloader), `checkingData`, `running`,
  `finished`, `dataError`, `failed`, `cancelled`. `error` says why for `dataError` and `failed`.
  TradeViewer shows the state as it comes and computes the elapsed time from `startedAt` and
  `finishedAt`.
- `progress` and `statistics` (the statistics so far, in the shape of the report's
  `statistics`) are updated while the run is going.
- `report` is today's backtest report, set when the run has finished.
- `scenarioFolder` is the folder of the run's scenario drawing files, relative to the scenario
  root: `test-runs/<id>`. EWTS writes one scenario for every setup the run watched or traded,
  as the run reaches it, in the format of Scenario Files with `figures`. Every entry of the
  report's `trades[]` and `watches[]` names its scenario (`scenario`: the id). TradeViewer
  draws nothing from the report itself.
- Deleting a run deletes its folder.
- Runs are stored by EWTS and survive a restart. EWTS runs one test at a time; further ones
  wait in the queue.
- The list (`GET /api/v1/test-runs`) returns the same records without `report`, with the
  headline figures (`trades`, `netR`, `winRate`, `maxDrawdownR`) for finished runs.

## Alerts

EWTS creates and sends alerts. Every alert gets an instance-wide, strictly increasing number.

`GET /api/v1/alerts?after=530&limit=100`  (`limit`: default 100, maximum 500, on every list)

```json
{ "lastAlert": 532,
  "alerts": [
    { "alert": 531, "at": "2026-11-11T09:15:41Z", "source": "binance", "symbol": "BTCUSDT", "kind": "setupFound",
      "title": "BTCUSDT long setup B", "text": "Wave 2 complete at 58,400. Trigger above 61,200.",
      "deliveries": [ { "channel": "telegram", "sentAt": "2026-11-11T09:15:42Z", "error": null } ] } ] }
```

`kind`: `setupFound`, `watchTriggered`, `watchCancelled`, `planEvent`, `error`. Further kinds
may be added.

`GET` / `PUT /api/v1/alert-settings`

```json
{ "kinds": { "setupFound": true, "watchTriggered": true, "watchCancelled": false,
             "planEvent": true, "error": true },
  "minGrade": "B",
  "channels": [ { "type": "telegram", "enabled": true, "chatId": "123456", "botTokenSet": true } ] }
```

A secret (the bot token) is written with `"botToken": "..."` in the PUT and never returned; the
GET only says whether it is set.

## Changes

One number series per instance, as in the Downloader. Every event below gets the next change
number.

`GET /api/v1/changes?after=9100`

```json
{ "lastChange": 9120, "complete": true,
  "changes": [
    { "change": 9118, "kind": "assetAnalysed", "source": "binance", "symbol": "BTCUSDT", "at": "2026-11-11T09:15:40Z" },
    { "change": 9119, "kind": "alert", "source": "binance", "symbol": "BTCUSDT", "alert": 531, "at": "2026-11-11T09:15:41Z" },
    { "change": 9120, "kind": "testRun", "testRunId": "pi-binance-17", "state": "finished",
      "at": "2026-11-11T09:31:07Z" } ] }
```

- `kind`: `assetAdded`, `assetRemoved`, `assetAnalysed`, `testRun`, `alert`, `alertSettings`.
- Changes are kept 30 days, as in the Downloader; for an older `after` the answer has `complete: false`
  and the client reloads everything it shows.
- There is no push; TradeViewer polls this endpoint. `lastChange` is also part of
  `GET /api/v1/status`.
