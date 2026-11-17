# Implementation Plan: Symbol Catalog

## Status

```text
Epic:     Symbol Catalog (P1 - Workstation)
Status:   Done: all 6 steps; approved by the owner 2026-11-17, merged into main, deployed on the Pi
Branch:   feature/symbol-catalog
Next:     nothing; the next epic is Indicators
Baseline: docs as of commit 1e299e7 (main)
```

Update this section whenever a step is completed.

---

# Why This Epic Comes Now

- The Downloader has built and deployed its symbol catalog (its epic Symbol Catalog, decision
  0008 of the Downloader, on home8 since 2026-11-17). AS2 says TradeViewer uses it as soon as it
  exists; the roadmap lists it under Later.
- It is small and comes before Indicators, at the owner's wish.

---

# Knowledge Sources

```text
Prompt of the Downloader's agent (2026-11-17)          checked below, not copied
../Downloader/docs/specification.md                     chapters Symbol Catalog, Subscriptions (read only)
../Downloader/src/Downloader.Contracts/Catalog/CatalogResponse.cs   to copy
../Downloader/src/Downloader.Api/Endpoints/*.cs        routes checked (read only)
docs/knowledge/downloader-interface.md                 to update with the catalog
docs/specification.md AS2, AS3, AS7                    to update
```

---

# Check of the Prompt

Correct, checked against the Downloader's specification and code:

- `GET /api/v1/sources/{source}/symbols?q=&assetClass=&limit=`, sources binance, yahoo,
  tradingview; `q` up to 40 characters; limit 1 to 500, default 50; 404 for an unknown source, an
  empty list with `refreshedAt: null` while a source has no list yet, 400 for invalid parameters.
- The answer and the DTO file `CatalogResponse.cs` (to copy unchanged, like the other contracts).
- The lookup `GET /api/v1/sources/{source}/symbols/{sourceSymbol}` takes the ticker at the source
  (URL-encoded: `NYSE:BRK.B`); `truncated` means "type more"; no caching, no paging.

To correct:

- "Subscribe exactly as today with `sourceSymbol`" is not what the Downloader does: the path of
  `PUT .../subscriptions/{source}/{symbol}` takes the **folder name** (`symbol`), and the ticker
  goes in the body as `sourceSymbol` (`SubscriptionRequest.SourceSymbol`, default: the symbol of
  the path). Checked in `SubscriptionEndpoints.cs` and `SubscriptionRequest.cs`. For binance both
  are the same, which is why TradeViewer never needed the difference.
- TradeViewer keys an asset by source and folder name and reads its files from that folder; the
  folder name is not "only a suggestion" for TradeViewer. The asset has to keep the ticker
  (`sourceSymbol`) as well, for lookups and subscriptions (AS7 sends subscriptions again at every
  start).

Missing, and decisive for the scope:

- yahoo and tradingview write the **header format** (`timestamp,open,...`, other file periods),
  and their symbols have **trading sessions** (`exchange` for stocks and ETFs, `newYorkClose` for
  forex). TradeViewer reads only the Binance spot format and builds the current candle only for a
  continuous session; yahoo has no 4h, which TradeViewer would have to build along the session.
  A stock found in the catalog could be added and subscribed, but not charted. That work is the
  item "Stocks, ETFs, Forex and commodities" under Later in the roadmap, an epic of its own.

---

# Scope (proposal)

In scope:

- The assets page searches the Downloader's catalog while the user types (250 ms after the last
  key), shows name, ticker, class and exchange, and "type more" when the list is truncated.
- Picking an entry looks it up at the Downloader and shows tick size, session and the earliest
  date per timeframe before the user adds it; adding subscribes with the folder name in the path
  and the ticker in the body.
- The asset keeps its ticker (`sourceSymbol`), stored in the workspace and sent with every
  subscription (AS7).
- Only the sources TradeViewer can chart are offered: binance. yahoo and tradingview come with
  the epic for stocks, ETFs and forex, which then only has to add them to the list of sources.
- Without the Downloader (`MarketData:Downloader:Enabled` false, development, tests), the fixed
  list `Symbols/binance.json` stays the source of the picker, as today.

Out of scope: reading the header format, trading sessions, building missing timeframes, a cache
of the catalog, paging.

---

# Steps

```text
1  Documents: AS2 (the catalog, the fixed list only without the Downloader), AS7 (the ticker is
   sent with the subscription); downloader-interface.md (catalog, lookup by ticker, subscription
   path and body, compared on 2026-11-17); architecture (ports, /api/symbols, the asset's
   ticker); backlog and roadmap                                                                  done
2  Downloader client: copy CatalogResponse.cs; SearchAsync(source, text, limit); LookupAsync by
   ticker (URL-encoded); SubscribeAsync sends SourceSymbol. Tests against recorded answers
   (binance, tradingview with NYSE:BRK.B, refreshedAt null, truncated, 404, 400, 503)            done
3  Domain and workspace: SymbolInfo gets SourceSymbol (default: the symbol); script 0006 adds
   assets.source_symbol (filled with the symbol for the assets there); repository, upgrade test  done
4  Application and Api: /api/symbols/{source}?q= searches the catalog when the Downloader is on,
   the fixed list when it is off, answering sourceSymbol, exchange, truncated, "not loaded yet";
   /api/symbols/{source}/{sourceSymbol} looks one up (tick size, session, earliest dates);
   POST /api/assets takes the ticker; handler and endpoint tests with a fake Downloader           done
5  Web: the picker on the assets page (debounce 250 ms, list, "type more", details of the entry
   picked, add); a check in the headless browser against a fake or the Pi's Downloader (see
   question 2)                                                                                   done
6  Docker, README, runbook; Pi on the owner's go-ahead                                            done (Pi open)
```

Each step has its tests; tests never contact the Downloader (they use recorded answers).

---

# Questions for the Owner

1. **Scope**: only binance in the picker until TradeViewer can chart stocks, ETFs and forex (my
   recommendation; otherwise you could add a stock that then shows no chart), or all three
   sources now, with yahoo and tradingview marked "cannot be charted yet"?
2. **Trying it against the real catalog**: may the development instance on WSL use the Pi's
   Downloader (192.168.178.47:8081)? Search and lookup only read. Adding an asset subscribes it
   under the client `tradeviewer-dev`; I would add one test asset and remove it again, which ends
   that subscription at the Downloader.
3. **Without the Downloader**: keep the fixed list `Symbols/binance.json` for development and tests
   (recommended), or drop it?

---

# Answers of the Owner (2026-11-17)

"Do what you recommend":

1. Only binance in the picker until TradeViewer can chart stocks, ETFs and forex.
2. The development instance may use the Pi's Downloader for search and lookup, and add and remove
   one test asset under `tradeviewer-dev`.
3. The fixed list `Symbols/binance.json` stays for working without the Downloader.

---

# Result (2026-11-17)

- Step 1: AS2 and AS7 changed; downloader-interface.md compared on 2026-11-17 (catalog; the
  subscription path takes the folder name, the ticker goes in the body); architecture chapters 5, 6
  and 9; roadmap: the item for stocks, ETFs and forex says what TradeViewer still needs.
- Step 2: `CatalogResponse.cs` copied unchanged; `IDownloaderClient.SearchAsync`, `LookupAsync` by
  ticker (URL-encoded), `SubscribeAsync` with the ticker in the body; `SymbolInfo.SourceSymbol` and
  `Ticker`. 5 client tests against recorded answers (catalog, not loaded, escaped text, NYSE:BRK.B,
  folder name in the path).
- Step 3: script 0006 adds `assets.source_symbol`; NULL means the same as the symbol, which every
  asset added before is (the plan said "filled with the symbol": NULL says the same without
  copying it). Upgrade tests insert their older rows with plain SQL (`InsertOlderAsset`).
- Step 4: `/api/symbols/{source}?q=` answers `SymbolSearchResponse` (symbols with ticker and
  exchange, total, truncated, from catalog, refreshed at), the fixed list without the Downloader;
  `/api/symbols/{source}/{sourceSymbol}` answers `SymbolDetailsResponse`; `POST /api/assets` takes
  the ticker, and with the Downloader the lookup, not the fixed list, is the check. Only
  `SymbolSources.Chartable` (binance). The test factory can stand the stub in for the Downloader
  (`TradeViewerFactory.WithDownloader`); 8 handler and 3 endpoint tests.
- Step 5: the picker on the assets page. Headless browser against the Pi's catalog
  (`~/tools/browser/catalog-check.mjs`, client `tradeviewer-dev`, empty workspace): four keys,
  one request; "50 of 303 matches shown. Type more"; BTCUSDT with tick size, session, timeframes
  and earliest dates; added and subscribed, removed and unsubscribed; nothing of
  `tradeviewer-dev` left at the Downloader.
- Step 6: README and runbooks need no change. Docker image built; the WSL container runs this
  branch with the Downloader off (fixed list), its workspace upgraded to version 6 with a backup.
- In all: 383 unit, 19 architecture, 198 integration tests pass; `dotnet format` clean.
