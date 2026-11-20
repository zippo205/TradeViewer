# EWTS Interface

Everything TradeViewer needs from EWTS (../EWTS). How TradeViewer uses it is in
docs/architecture.md, chapter EWTS Integration; the decisions behind it are in
docs/decisions/0004-ewts-integration.md.

This document has two parts:

1. **Existing Interface**: what the EWTS API offers today, read from its code on 2026-11-11
   (branch `feature/setup-quality`).
2. **Required Interface**: the API (v1) and the scenario files TradeViewer uses. EWTS owns the
   interface. Its original is `../EWTS/docs/architecture.md`, chapter 7 (API) and section
   "Scenario Files", with the requirements in `../EWTS/docs/specification.md`, chapter 4.9; once
   built, EWTS's code is the original. This part follows it and is updated when it changes.

---

# 1. Existing Interface

**Historical.** This part describes the API of the earlier implementation (`../EWTS_MVP`), which
the new EWTS does not carry over. It is kept only until the new interface is built.

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
SC1-SC9   scenarios                      the scenarios exist as YAML with a Pine overlay; the shape
                                         model exists in tools/scenario-to-pine.py but is not
                                         written as data
```

The scenarios are the only one of these that need no running EWTS instance.

---

# 2. Required Interface

Status: follows `../EWTS/docs/architecture.md`, chapter 7, and `../EWTS/docs/specification.md`,
chapter 4.9 (read 2026-11-14). TradeViewer proposed this interface on 2026-11-11; EWTS has taken it
over with changes. Where this part differs from EWTS's documents, they win.

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
  timeframes are `1d` or `4h`; for an asset with exchange hours (stocks, ETFs) only `1d`: EWTS
  refuses `4h` there with 422 (the request is well formed, the asset cannot be analysed on 4h),
  and its analysis never uses 4h for such an asset (TradeViewer's
  decision 0008; agreed with EWTS on 2026-11-18). Such an asset keeps 1h as the source delivers it: the last
  1h candle of a trading day covers half an hour (US exchanges 15:30 to 16:00 New York time), which
  an analysis reading candle sizes on 1h has to know (decision 0009).
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
{ "contextTimeframe": "1d", "historyFrom": "2019-01-01", "profile": "default" }
```

- Idempotent. Adding subscribes the asset at the Downloader under EWTS's client name and starts
  the observation; a change of the settings analyses the asset again.
- 422 when the Downloader does not know the symbol, 503 when the Downloader is unreachable;
  nothing is stored in both cases.
- The observation survives a restart of EWTS.

`GET /api/v1/assets`

```json
{ "assets": [
  { "source": "binance", "symbol": "BTCUSDT", "displayName": "BTC/USDT", "contextTimeframe": "1d",
    "historyFrom": "2019-01-01", "profile": "default",
    "state": "watching", "lastAnalysisAt": "2026-11-11T09:15:40Z", "lastError": null,
    "bestSetup": { "direction": "long", "state": "watching" },
    "dataQuality": "complete" } ] }
```

`state`: `pending` (not analysed yet), `noSetup`, `setup`, `watching`, `planOpen`, `error`.
`dataQuality`: `complete`, `gaps`, `loading`, `error`. The `contextTimeframe` (`1d` or `4h`) is the
top of a chain of three timeframes in EWTS: context, setup, trigger (`1d`, `4h`, `1h` or `4h`,
`1h`, `15m`). There is no grade or score yet; a record that gets them later holds them as
additional fields, and TradeViewer shows them only when they are present.

`GET /api/v1/assets/binance/BTCUSDT`: the overview row plus

```text
count           timeframe, pattern, direction, lastWave, nextWave, invalidation level, confidence
setups[]        id, direction, state (found, watching, cancelled, planOpen, closed), foundAt,
                waveOne, waveTwo (points), triggerLevel, stop, target, addLevel, entry, exit,
                resultR, endedAt, reasons[]
setupHistory[]  the same fields for setups that are no longer current
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

Decisions: docs/decisions/0010-expert-scenarios-made-in-tradeviewer.md and EWTS's
../EWTS/docs/decisions/0006-expert-scenarios-from-tradeviewer.md. Original:
`../EWTS/docs/architecture.md`, chapter 6, "TradeViewer: Scenario Folder", and chapter 7,
"Scenario Files" (read 2026-11-20).

```text
<Scenarios:RootPath>/                  the shared scenario folder
├── expert/<id>.json                   expert scenarios, written by TradeViewer's editor
└── test-runs/<run id>/
    ├── <id>.json                      drawing file of a test-run scenario, written by EWTS
    └── <id>.pine                      its TradingView Pine script, written by EWTS
```

- One writer per folder: TradeViewer never writes into `test-runs/`; EWTS never writes or deletes
  anything in `expert/`. Deleting a run removes its folder.
- Both write atomically (`<name>.part`, then rename).
- TradeViewer reads the `*.json` files; `.pine` and other files are ignored. A file TradeViewer
  cannot read (unknown `format` or `version`, broken JSON) is listed with its error instead of
  being hidden.
- All times are UTC.

### Test-Run Scenario

`test-runs/<run id>/<id>.json`, `version` 1. The id is `<symbol>-<context tf>-<yyyy-MM>-T<n>`:
lower case except `T`, the month of the end of wave 2 when the scenario is first written, `n`
counting the run's scenarios; unique only within its run folder and never changed during the run.

```json
{ "version": 1,
  "id": "ethusdt-4h-2026-08-T3", "source": "binance", "symbol": "ETHUSDT",
  "contextTimeframe": "4h", "direction": "long", "verdict": "trade-win",
  "reason": "Wave 1 and a zigzag wave 2 at 73.3 % (log) by wick. ...",
  "chart": null, "from": "2026-08-01T16:00:00Z", "to": "2026-11-11T00:00:00Z",
  "generatedAt": "2026-11-11T10:02:11Z",
  "figures": [ { "name": "Result (R)", "value": 0.45 }, { "name": "Exit", "value": "TargetThenStop" } ],
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
          "from": "2026-08-11T12:00:00Z", "to": "2026-11-11T00:00:00Z", "text": "161.8 %" } ] } ] }
```

- `version` is the format version: additive changes keep it, an incompatible change raises it.
- `source` is the source the candles come from.
- `verdict`: `trade-win`, `trade-loss`, `no-trade`, or `draft` for a plan still open at the end
  of the run.
- `from` and `to` are the time span of the drawing; TradeViewer positions the chart on it.
- `groups` and `shapes` use the vocabulary of Drawings above. The groups are `waves`,
  `lowerDegree`, `levels` and `trade`.
- `figures` (optional) are named values to show as columns of a list and in the panel of the
  viewer: what the engine measured and how the trade ended. A value is a text or a number.
  TradeViewer shows them in the given order and sorts by them; it does not interpret them.
  The scenarios of one test run carry the same names.

## Expert Scenario Files

`expert/<id>.json`, written by TradeViewer's editor (specification SC10 to SC17); EWTS's
acceptance tests read them from a copy in the EWTS repository. Named points, not shapes; no
`expected` section: the expert scenario is the truth.

```text
head       format ("ewts-expert-scenario"), version (1), id, source, symbol,
           contextTimeframe (1d | 4h), direction (long), verdict (trade-win | trade-loss |
           no-trade), draft, reason, notes, from, to, createdAt, updatedAt
waves      timeframe; wave1Start, wave1End, wave2End                   point { time, price, why }
wave2      timeframe, pattern (zigzag | flat | expanded-flat | running-flat | triangle |
           complex); aEnd, bEnd, cEnd                                   optional
trade      timeframe; entry, stop { price, why }, add, stopMoves[],
           exits[] { time, price, share, kind (target | stop), why }   for trade-win, trade-loss
legacy     only in converted scenarios: the old fields with no place here; ignored by EWTS
```

- Id `<symbol>-<context tf>-<yyyy-MM>-X<n>` (`btcusdt-1d-2024-03-X1`): lower case except `X`,
  the month of `wave2End`, `n` the next free number of that prefix. The file name is `<id>.json`.
- `from` and `to` are the span EWTS runs over and TradeViewer shows; for `no-trade`, `to` means
  "no trade until here". A `draft` is checked by EWTS but is no acceptance test.
- Each section names its timeframe, because a reading may mix them (1d waves, 1h internals).
- Snap rules: a wave point is the open time of its candle on the section's timeframe and that
  candle's wick extreme (the low for a low, the high for a high). `entry` and `add` are the open
  time and the close of the candle that closes beyond the level. An exit is the open time of the
  candle that reaches the level, at the level's price. `stop` and `stopMoves[].price` are prices;
  a stop move's time is the candle whose close led to it.
- `aEnd` and `bEnd` are required for a zigzag and the flats. Without `cEnd`, the end of C is
  `wave2End`. `add` and `stopMoves` are optional; the `share` of the exits, in percent, adds up
  to 100.
- TradeViewer rebuilds its drawing from the points and stores no drawing records in the file. It
  keeps `legacy` and any field it does not know unchanged when it saves.
- Rule checks (is a point a real extreme, does the entry follow the strategy) are EWTS's;
  TradeViewer checks only the form.
- Agreed with EWTS on 2026-11-20, not yet in its architecture: the id is fixed at the first save
  and stays when `wave2End` later moves into another month; a file may be incomplete (saved at
  every change), and EWTS reports it as incomplete instead of failing; `why` is optional text.

## Test Runs

`POST /api/v1/test-runs` takes `kind` (`backtest`, later `walkForward`), `source`, `symbol`,
`contextTimeframe` (`1d` or `4h`), `from`, `to`, `profile`, `accountSize`, `riskPercent` and
`overrides { parameters, increments }` (names to values, names to flags). The names and their
defaults come from `GET /api/v1/profiles`: `profiles[] { name, parameters[] { key, label, type,
default, min, max }, increments[] { key, label, default } }`, so the start form is generic.
Answer 202:

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
  "request": { "source": "binance", "symbol": "BTCUSDT", "contextTimeframe": "1d", "from": "2024-01-01", "to": "2025-12-31" },
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
- `report` is set when the run has finished: `parameters`, `funnel`, `statistics`, `plans[]`,
  `watches[]`, `skipped[]` and the known data gaps. The statistics hold `trades`, `winRate`,
  `averageR`, `totalR` (gross and net), `profitFactor`, `maxDrawdownR`, `longestLosingStreak`,
  `exits`, `costsR`, `averageHolding` and `equityCurve[]`.
- `scenarioFolder` is the folder of the run's scenario drawing files, relative to the scenario
  root: `test-runs/<id>`. EWTS writes one scenario for every setup the run watched or traded,
  as the run reaches it, in the format of Test-Run Scenario (Scenario Files) with `figures`. Every entry of the
  report's `plans[]` and `watches[]` names its scenario (`scenario`: the id). TradeViewer
  draws nothing from the report itself.
- Deleting a run deletes its folder.
- Runs are stored by EWTS and survive a restart. EWTS runs one test at a time; further ones
  wait in the queue.
- The list (`GET /api/v1/test-runs`) returns the same records without `report`, with the
  headline figures of the statistics for finished runs.

## Alerts

EWTS creates and sends alerts. Every alert gets an instance-wide, strictly increasing number.

`GET /api/v1/alerts?after=530&limit=100`  (`limit`: default 100, maximum 500, on every list)

```json
{ "lastAlert": 532,
  "alerts": [
    { "alert": 531, "at": "2026-11-11T09:15:41Z", "source": "binance", "symbol": "BTCUSDT", "kind": "setupFound",
      "title": "BTCUSDT long setup", "text": "Wave 2 complete at 58,400. Trigger above 61,200.",
      "deliveries": [ { "channel": "telegram", "sentAt": "2026-11-11T09:15:42Z", "error": null } ] } ] }
```

`kind`: `setupFound`, `watchStarted`, `watchTriggered`, `watchCancelled`, `planEvent`, `error`. Further kinds
may be added.

`GET` / `PUT /api/v1/alert-settings`

```json
{ "kinds": { "setupFound": true, "watchStarted": true, "watchTriggered": true, "watchCancelled": false,
             "planEvent": true, "error": true },
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
