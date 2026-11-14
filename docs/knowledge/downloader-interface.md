# Downloader Interface

Everything TradeViewer needs from the Market Data Downloader (../Downloader): the files it
reads and the control API it calls. How TradeViewer uses them is in docs/architecture.md,
chapters Market Data and Downloader Integration.

The Downloader owns this interface. Its specification (../Downloader/docs/specification.md) is
the original; this document is TradeViewer's view of it, last compared on 2026-11-11.

Scope today: crypto with Binance as the only source in use. A `yahoo` source exists for
testing. Stocks, ETFs and Forex follow as further sources.

---

# Output Layout

```text
<RootPath>/                                 output root (MarketData:RootPath), read only
└── <source>/                               lower case: binance, yahoo
    ├── symbols.json                        metadata of the symbols of this source
    └── <SYMBOL>/                           upper case: BTCUSDT
        ├── manifest.json                   Downloader internal, never read
        ├── manifest.recent.json            Downloader internal, never read
        └── <timeframe>/                    15m, 1h, 4h, 1d, 1w (later 1m, 5m, 12h, 1M)
            └── <SYMBOL>-<timeframe>-<period>.csv
```

A missing symbol or timeframe folder means "no data". A missing root path or source folder
(e.g. the output root is not mounted) is a configuration error, never "no data".

## Rules for All Sources

- Only **closed** candles are written; the candle in progress never is.
- All times are UTC.
- Intraday candles carry the real open time of the bar. Daily, weekly and monthly candles open
  at 00:00 UTC of their trading date (weekly: the Monday, monthly: the 1st).
- Within a file, open times are strictly increasing without duplicates; low <= open, close <=
  high; volume >= 0; prices > 0. A violation is invalid data, reported with file name and line
  number, never skipped silently.
- A file contains exactly the candles whose open time lies in its period (UTC).
- Files are written atomically: first `<name>.csv.part`, then renamed. Read only `*.csv`.
- Files are rewritten as a whole: the file of the current period grows about every 15 minutes,
  a provisional file is replaced by the official one, a corporate action rewrites past files.
  Any file can change.
- Candles that never existed at the source (outages, days without trading) are missing. The
  Downloader never fills candles, and neither does TradeViewer.

## symbols.json

```json
{
  "version": 1,
  "symbols": {
    "BTCUSDT": {
      "assetClass": "crypto", "displayName": "BTC/USDT", "tickSize": 0.01, "source": "binance",
      "session": { "type": "continuous", "timeZone": "UTC" },
      "sourceTimeframes": ["15m", "1h", "4h", "1d", "1w"]
    }
  }
}
```

- `version`: additive changes keep it, an incompatible change raises it. A reader that does not
  know the version stops with a clear message.
- `assetClass`: `crypto`, `stock`, `etf`, `forex`.
- `session`: how candles are aligned when a timeframe is aggregated.
- `sourceTimeframes`: what the source provides, not what is downloaded.
- Unknown fields are ignored.

TradeViewer takes this metadata from the lookup of the control API when an asset is added;
`symbols.json` is the same information on disk.

---

# File Formats

## Binance Spot (source `binance`), in use

```text
intraday, 1d    <SYMBOL>-<tf>-<yyyy-MM-dd>.csv      one file per day        BTCUSDT-15m-2026-11-04.csv
1w              <SYMBOL>-<tf>-<yyyy-MM>.csv         one file per month      BTCUSDT-1w-2026-11.csv
```

The original Binance kline format: 12 columns, comma separated, no header row.

```text
 0 open time      3 low        6 close time                  9 taker buy base volume
 1 open           4 close      7 quote asset volume         10 taker buy quote volume
 2 high           5 volume     8 number of trades           11 ignore
```

TradeViewer reads columns 0 to 5.

- **Time unit:** milliseconds before 2025-01-01, **microseconds** from 2025-01-01. Rows the
  Downloader fills from the REST API are in milliseconds also after 2025, so one file can mix
  units. Detect the unit per value: >= 10^14 is microseconds.
- Decimals with `.` as separator; prices have up to 8 decimals.
- Session `continuous`, UTC.
- Recent days are provisional files from the REST API, replaced by the official files about a
  day later.
- Real outages exist (e.g. 2021-08-13 02:00-06:15 UTC) and are reported as `missingUpstream`.

## Header Format (stocks, ETFs, Forex; source `yahoo` for testing), later

```text
intraday        <SYMBOL>-<tf>-<yyyy-MM-dd>.csv      one file per day
1d              <SYMBOL>-<tf>-<yyyy-MM>.csv         one file per month
1w, 1M          <SYMBOL>-<tf>-<yyyy>.csv            one file per year
```

```text
timestamp,open,high,low,close,volume
2026-10-01T13:30:00Z,415.2,416.0,414.9,415.7,1203400
```

- Header row; `timestamp` ISO 8601 UTC with `Z`; optional columns by name (`open_interest`,
  `funding_rate`, `bid_volume`, `ask_volume`).
- Session `exchange` (stocks, ETFs): exchange time zone with open and close; intraday candles
  follow the session open; no candles outside trading hours, at weekends and on holidays, and
  these are not gaps. Splits rewrite past files.
- Session `newYorkClose` (Forex): the trading day runs 17:00 to 17:00 America/New_York; a daily
  candle is labelled with its closing date; daily and 4h candles are built from the 1h files.

Binance futures files (not provided yet) have the 12 columns of spot **with** a header row and
stay in milliseconds; a reader skips a header row and detects the unit per value.

---

# Control API

HTTP and JSON, no authentication. All paths start with `/api/v1/`; within v1 changes are only
additive, so a client ignores unknown fields. Times are ISO 8601 UTC with `Z`, dates
`yyyy-MM-dd`, timeframes as codes (`15m`, `1w`), names and enum values camelCase.

Errors are problem details (RFC 9457):

```text
400  invalid input
404  unknown client, subscription or symbol
422  symbol unknown at the source
503  source unreachable
```

The request and response records are defined in the Downloader's `Downloader.Contracts`
project and copied unchanged into TradeViewer (architecture, Downloader Integration).

```text
GET    /api/v1/sources/{source}/symbols/{sourceSymbol}            look up one symbol, without subscribing
PUT    /api/v1/clients/{client}/subscriptions/{source}/{symbol}   subscribe or replace the subscription
DELETE /api/v1/clients/{client}/subscriptions/{source}/{symbol}   unsubscribe: 204, 404 if not subscribed
GET    /api/v1/clients/{client}/subscriptions                     the client's subscriptions
GET    /api/v1/subscriptions                                      all clients, merged per (source, symbol)
GET    /api/v1/status                                             service state, cycles, queue, sources
GET    /api/v1/cycles?limit={n}                                   update cycles, newest first (default 20, max 200)
GET    /api/v1/status/{source}/{symbol}                           coverage per timeframe, gaps, last change
GET    /api/v1/status/{source}/{symbol}/changes?after={n}         files changed after change number n
GET    /health                                                    liveness
```

Planned by the Downloader, not available yet: `GET /api/v1/sources/{source}/symbols?q=...`
(symbol catalog, for a symbol picker).

## Lookup

`GET /api/v1/sources/binance/symbols/BTCUSDT` (`SymbolLookupResponse`):

```json
{ "source": "binance", "symbol": "BTCUSDT", "sourceSymbol": "BTCUSDT", "assetClass": "crypto",
  "displayName": "BTC/USDT", "tickSize": 0.01, "exchangeTimezone": null,
  "session": { "type": "continuous", "timeZone": "UTC", "open": null, "close": null },
  "sourceTimeframes": ["15m", "1h", "4h", "1d", "1w"],
  "earliest": { "1d": "2017-08-17" } }
```

`symbol` is the folder name the Downloader suggests; `sourceSymbol` is the ticker at the source
(Binance: the same; Yahoo e.g. `EURUSD` and `EURUSD=X`). `earliest` holds the first available
date per timeframe. 422 for an unknown symbol, 503 when the source is unreachable.

## Subscribe

`PUT /api/v1/clients/tradeviewer/subscriptions/binance/BTCUSDT` (`SubscriptionRequest`):

```json
{ "sourceSymbol": "BTCUSDT", "timeframes": ["15m", "1h", "4h", "1d", "1w"],
  "historyFrom": null, "historyFromByTimeframe": { "15m": "2023-01-01" },
  "assetClass": null }
```

Response 200 (`SubscriptionResponse`):

```json
{ "source": "binance", "symbol": "BTCUSDT", "sourceSymbol": "BTCUSDT", "assetClass": "crypto",
  "sourceTimeframes": ["15m", "1h", "4h", "1d", "1w"],
  "timeframes": [
    { "timeframe": "15m", "status": "accepted", "historyFrom": "2023-01-01", "clipped": false },
    { "timeframe": "1d",  "status": "accepted", "historyFrom": "2017-08-17", "clipped": true } ] }
```

- Idempotent; replaces this client's subscription to the symbol completely. Other clients are
  not affected. A client needs no registration.
- `historyFrom` applies to all timeframes, `historyFromByTimeframe` overrides it. Without a
  date the Downloader's defaults apply: 1d and 1w complete history, 4h and 1h 2 years, 15m
  1 year. There is no end date. More history is a later PUT with an earlier date.
- `status` per timeframe: `accepted` or `unsupported` (the source does not provide it; the
  client aggregates it). `clipped: true`: the requested start lay before what the source
  provides; `historyFrom` is then the effective start.
- The symbol is checked at the source synchronously: 422 or 503, nothing is stored then.
- The Downloader downloads what is missing on its own, recent data first.

## Unsubscribe

`DELETE /api/v1/clients/tradeviewer/subscriptions/binance/BTCUSDT`: 204, or 404 if there was no
subscription. Updates stop when no client subscribes any more; files are never deleted.

## Service Status

`GET /api/v1/status` (`ServiceStatusResponse`):

```text
state               running
startedAt, version
lastCycle           startedAt, finishedAt, result, jobsPlanned, message
                    result: running, ok, partial, failed, interrupted, skipped
nextCycleAt
queue               recent, backfill, retryMissing, running, failed
sources[]           source, state, lastSuccessAt, lastError, lastErrorAt, retryAt
                    state: ok, rateLimited (until retryAt), unavailable (after 3 failed requests)
consistencyCheck    lastRunAt, problems (weekly file check; null before the first run)
```

## Symbol Status

`GET /api/v1/status/binance/BTCUSDT` (`SymbolStatusResponse`):

```json
{ "source": "binance", "symbol": "BTCUSDT", "subscribedBy": ["tradeviewer", "ewts"], "lastChange": 48213,
  "timeframes": [
    { "timeframe": "15m", "wantedFrom": "2019-01-01",
      "coveredFrom": "2019-01-01T00:00:00Z", "coveredTo": "2026-11-04T15:15:00Z",
      "lastChange": 48213, "lastChangedAt": "2026-11-04T15:30:31Z",
      "latestFile": "15m/BTCUSDT-15m-2026-11-04.csv", "pendingJobs": 0,
      "gaps": [ { "from": "2021-08-13T02:00:00Z", "to": "2021-08-13T06:30:00Z", "reason": "missingUpstream" } ],
      "lastError": null } ] }
```

- `coveredTo` and gap `to` are exclusive.
- Gap reasons: `missingUpstream` (confirmed by the source, never filled), `unavailable` (before
  the source's history limit), `pending` (not downloaded yet).
- `lastChange` of the symbol is what a client polls.

## Changes

Every write that changes a file gets a service-wide, strictly increasing change number.

`GET /api/v1/status/binance/BTCUSDT/changes?after=48100` (`SymbolChangesResponse`):

```json
{ "lastChange": 48213, "complete": true,
  "changes": [
    { "change": 48150, "timeframe": "1d",  "file": "1d/BTCUSDT-1d-2026-11-03.csv",   "kind": "replaced", "at": "2026-11-04T02:00:12Z" },
    { "change": 48213, "timeframe": "15m", "file": "15m/BTCUSDT-15m-2026-11-04.csv", "kind": "updated",  "at": "2026-11-04T15:30:31Z" } ] }
```

- `file` is relative to the symbol folder.
- `kind`: `new`, `updated` (the current period grew), `replaced` (a past file was rewritten).
- Changes are kept 30 days. For an older `after` the response has `complete: false`: the client
  treats every file of the symbol as changed.
- There are no notifications; clients poll.
