# Implementation Plan: Watchlists

## Status

```text
Epic:     Watchlists (P1 - Workstation)
Status:   Plan approved by the owner 2026-11-16 (answers at the end); in work
Branch:   feature/watchlists
Next:     decided after this epic (Scenario Viewer needs the drawing files of EWTS)
Baseline: docs as of commit af424e3 (main)
```

Update this section whenever a step is completed.

---

# Why This Epic Comes Now

- The backlog puts the Scenario Viewer first, but it reads drawing files that EWTS writes in its
  epic 10 (Scenarios), which is still open behind its epics 7 to 9. No real drawing file exists.
  The owner decided on 2026-11-16 to take Watchlists first; it needs nothing from EWTS.
- The chart exists, but an asset is chosen from a plain selector and the assets page is one flat
  list. The watchlists are how the specification means assets to be found and opened (AS1, CP1).
- Drawing Tools, the next large epic without a dependency on EWTS, lists Watchlists as its
  dependency in the backlog.

---

# Knowledge Sources

```text
docs/specification.md
    -> 4.1 (AS1, AS6; AS2 and AS5 for the actions that stay), CP1 (selecting an asset in a
       watchlist shows it in the current window), MD4 and MD6 (what the warning mark means),
       EW3 (the mark for EWTS, later)
docs/architecture.md
    -> chapter 4 (feature Watchlists), 9 (/api/watchlists; the order is replaced as a whole),
       11 (assets with their position), 6 (sync state with the Downloader's gaps, import errors)
docs/plans/market-data.md, docs/plans/price-chart.md
    -> the assets page and the chart page this epic changes; the hub events it listens to
```

---

# Scope

In scope:

- The five fixed watchlists, one per asset class, each with its assets in the user's order (AS1)
- The figures of a row (AS6): last close with its time, change against the previous daily close in
  percent, a warning mark for gaps and for files that were not imported; the loading state (AS4)
- Reordering the entries of a watchlist, saved at once (WS1)
- A watchlist panel on the chart page: selecting a row shows the asset in the current window
  (CP1); it replaces the plain asset selector
- The assets page grouped by the five watchlists; add, history, reload, rebuild and remove stay
  as they are, and a new asset appears at the end of the list of its class
- Rows that update without a reload when candles or the state of an asset change

Out of scope (later epics):

- The mark for an asset that EWTS analyses and the switch for it (EWTS Instances: EW3). The row
  contract already has the field; it stays empty
- The Downloader status page with coverage and gaps per asset (Downloader Monitoring: DL1-DL4);
  the warning mark of a row only says that there is something to look at, and why
- Search across lists, own lists, renaming: the specification rules them out (AS1)

---

# Steps

```text
1  Domain: the figures of a row as a pure calculation (last close, previous daily close in UTC,
   change in percent), the rule for a valid new order; unit tests                               open
2  Workspace: saving a new order of one watchlist in one transaction; reading the newest candles
   of many assets from the candle store in one go                                               open
3  Feature Watchlists: GetWatchlistsQuery, ReorderWatchlistCommand with its validator; contracts;
   /api/watchlists (get) and /api/watchlists/{assetClass}/order (put); tests                    open
4  Web: watchlist panel on the chart page (five lists, rows with figures and marks, the selected
   asset highlighted, a click opens it in the same timeframe); the asset selector goes;
   panel state remembered in the browser                                                        open
5  Web: assets page grouped by watchlist, reordering by drag and drop with buttons as the
   keyboard way; link from the panel to add an asset                                            open
6  Live update of the rows on CandlesUpdated and AssetStatusChanged (one read for many events);
   measurement with all 13 symbols; checks in the headless browser; Docker; architecture and
   the wrong example in chapter 4 corrected                                                     open
```

Each step has its tests: unit tests for the figures and the order rule, handler tests with
in-memory ports, integration tests for the endpoints, and scripted checks in the headless browser
for the panel and the reordering.

---

# Decisions of This Epic

Accepted by the owner on 2026-11-16. They are written into the architecture when the step that
needs them is done.

- **Where the watchlists are.** A panel at the right side of the chart page, as in TradingView:
  the five lists as tabs with the number of their assets, the rows below. It can be folded away to
  give the chart the whole width. The assets page shows the same five lists with the management
  actions. There is no separate watchlist page.
- **A row** shows the display name, the source in small print, the last close with the precision
  of the tick size, the change in percent in green or red, and marks for "loading" and "warning".
  Pointing at the last close shows its time (UTC); pointing at the warning shows the reason.
- **Last close** is the close of the newest closed candle of the asset's smallest timeframe; the
  current candle that TradeViewer builds (MD8) is not used, so the figure is always a delivered one.
- **Change** compares the last close with the close of the last daily candle that ended before the
  day of the last close, the day cut at 00:00 UTC (AS6: the day in UTC for crypto). An asset
  without a daily timeframe or without an earlier daily candle shows no change. Other sessions
  (exchange, New York close) get their rule when their sources come.
- **Warning mark:** the asset has at least one file that was not imported (MD6), or the Downloader
  reports a gap with the reason `missingUpstream` (MD4). Gaps that are `pending` count as loading,
  `unavailable` (before the history limit of the source) as nothing.
- **Reordering** sends the whole new order of one watchlist (chapter 9). It is refused when it
  does not name exactly the assets of that list, e.g. because another window added one meanwhile;
  the page then reads the lists again. With two windows the last save wins (WS1).
- **Drag and drop** for reordering, on the assets page only; the panel on the chart page is for
  choosing, so a slip of the mouse there cannot change the order. Buttons "up" and "down" do the
  same for the keyboard.
- **Panel state** (open or folded, the chosen list) is kept in the browser (`localStorage`), per
  browser: it is how a window looks, not something the user created, so it is no part of the
  workspace and of its backups.
- **Choosing an asset** keeps the timeframe when the asset has it with candles, otherwise its
  largest timeframe with candles; the chart keeps the number of visible candles (CH3).
- **Rows update** when the hub reports new candles or a changed state; many events in a row lead
  to one read of the lists.
- **The example in architecture chapter 4** names a `RenameWatchlistCommand`, which contradicts
  AS1 (watchlists cannot be renamed). It is replaced by the real messages of this feature.

---

# Answers of the Owner (2026-11-16)

1. The panel at the right side of the chart page, as in TradingView.
2. Reordering on the assets page only, as proposed.
3. No other change to the proposals.
