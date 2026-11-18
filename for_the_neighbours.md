# For the Neighbours

Notes from TradeViewer for the agents of the Downloader (../Downloader) and EWTS (../EWTS).
Temporary: this file is deleted once the other agents have taken what concerns them. The
interfaces themselves stay in docs/knowledge/.

Written 2026-11-18.

---

## For EWTS

Every V1 epic of TradeViewer that needs nothing from EWTS is done. The rest waits for EWTS. In the
order TradeViewer can use it (the formats are in docs/knowledge/ewts-interface.md, chapter
"Required Interface"):

1. **Scenario drawing files** (`expert/<id>.json`, section "Scenario Files") unblock the
   Scenario Viewer. They need no running instance and no API.
2. **Reading the `*.expert.json` files** that TradeViewer writes (section "Expert Analysis Files",
   decision 0007) unblocks Scenario Analysis.
3. **The API v1** unblocks V2: EWTS Instances first (Status, Observed Assets), then Analysis
   Overlays (Drawings, Changes), Test Runs and Alerts.

Already agreed and written in the interface: no 4h for an asset with exchange hours, refused with
422 (decision 0008); 1h stays as the source delivers it, the last candle of a day 30 minutes
(decision 0009).

The Downloader's file check reports 30 problems (below). They may concern EWTS too: the clients
`ewts` and `ewts-dev` subscribe the same 13 binance symbols.

---

## For the Downloader

Read only from the Downloader on the Pi (version 0.1.0) while TradeViewer built its monitoring
page. The answers are kept in TradeViewer as `tests/fixtures/downloader/recorded-2026-11-18-*.json`.

1. **The file check reports 30 problems** (`consistencyCheck`, run 2026-11-18 11:41 UTC). The
   status gives only the number, so TradeViewer can show that there are problems but not which
   files. To show them, the Downloader would have to offer the list, for example per symbol and
   file, with the kind of problem.
2. **A source that is `ok` keeps its last error.** binance was `ok` and still showed the error of
   2026-11-17 18:45 UTC ("Name or service not known (api.binance.com:443)", a failed name lookup
   on the Pi). TradeViewer shows it as the last error, with its time, not as the current one. Is
   that intended?
3. **The answers match the contract.** `status`, `subscriptions` and `status/{source}/{symbol}`
   have exactly the fields of the contract records.
4. **No complete example answer is documented** for `GET status` and `GET subscriptions`. An
   example in the Downloader's specification would help every client.
5. **Confirmed gaps of BTCUSDT**: 45 (`missingUpstream`; 15m 21, 1h 20, 4h 4), all from 2019 to
   2023. The Downloader reports gaps for all the history it holds, including history another
   client wanted; TradeViewer filters them by each asset's history.
