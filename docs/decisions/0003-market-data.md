# 0003 - Market Data

Status: accepted (2026-11-11)

## Context

The Downloader writes one CSV file per day (or month) and timeframe: ten years of 15m candles
of one asset are about 3,650 files. A chart must appear within 150 ms (NF1). The Downloader has
no push channel; it numbers every file change and clients poll.

## Decision

1. TradeViewer does not download market data. It subscribes its assets at the Downloader
   under the client name `tradeviewer` and reads the files from the shared output root, which
   it never writes to.
2. The files are imported into a local candle store (`marketdata.db`). Charts read from the
   store, never from CSV.
3. Changes are detected by polling the Downloader's change numbers. A rescan of the asset's
   files (size and modified time against an import log) is the fallback.
4. An asset is identified by source and symbol everywhere.

## Reasons

- Reading thousands of small files per chart request is too slow, above all on a network
  mount. EWTS reads the files directly because it has no interactive latency target;
  TradeViewer has one.
- The change numbers name the changed files exactly and survive missed polls; a file watcher
  would add a second, less reliable signal (it does not work on network mounts and read-only
  container mounts on every platform).
- Other sources will reuse symbol names, so the symbol alone is not an identity.

## Rejected

- Asking the Downloader for candles over HTTP: its API is a control API by design; data is
  exchanged as files.
- File system watcher: see above.
- Reading CSV on every request, as EWTS does: misses NF1.

## Consequences

- Candles exist twice (files and store); the store is about 3 GB for 100 assets.
- New candles reach a chart after the poll interval plus the import, not instantly (MD2).
