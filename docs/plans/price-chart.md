# Implementation Plan: Price Chart

## Status

```text
Epic:     Price Chart (P0 - Foundation)
Status:   Completed and approved by the owner 2026-11-16 (he checked the chart in Docker on WSL
          with the frozen data); merged into main. Not on the Pi yet: the owner was not at home
Branch:   feature/price-chart
Next:     Epic Scenario Viewer
Baseline: docs as of commit dee1fab (main)
```

Update this section whenever a step is completed.

---

# Why This Epic Comes Now

- The chart is the core of the product. Everything after it (scenario viewer, watchlists, drawing
  tools, indicators, overlays) is drawn on it or opens it.
- The candles are there: the store, the endpoint with paging and the current candle, and the event
  for new candles exist since the last epic and are fast enough (a page in 2 to 17 ms on WSL).
- The chart module sets the base that the largest work package of the frontend, the drawing layer,
  is built on. The library version and its primitive API must be settled before that.

---

# Knowledge Sources

```text
docs/specification.md
    -> 4.3 (CH1-CH5), 4.5 (CP1-CP3), MD7, MD8, NF1, NF2, NF6, NF11
docs/architecture.md
    -> chapter 10 (chart module, interop contract), 6 (serving candles), 9 (routes, hub),
       11 (workspace tables), 14 (testing: Vitest, no browser tests in V1)
docs/decisions/0001-technology-stack.md
    -> Lightweight Charts (Apache-2.0, attribution notice), TypeScript only in TradeViewer.Chart
Lightweight Charts 5.3.0 (npm, the latest release; checked for this plan in a scratch folder)
    -> typings: addSeries(CandlestickSeries / HistogramSeries), panes (addPane), series and pane
       primitives (attachPrimitive, hit testing, autoscale info), visible logical range with its
       change event, barsInLogicalRange, price lines, price format with minMove, logarithmic price
       scale, time formatters, crosshair event; one bundle of 196 KB, no service or account
../relatedProjects/OpenCharts (look, never copy)
    -> how drawing tools sit on Lightweight Charts 4.2; consulted for the structure of the pane
       controller so that the drawing layer fits later
docs/plans/market-data.md
    -> the candle endpoint, the hub and the measurements this epic builds on
```

---

# Scope

In scope:

- Lightweight Charts pinned to an exact version and bundled into the application (NF6); a short
  proof that a series primitive and a pane primitive can be attached and drawn, because the later
  epics depend on it
- Chart module: candles, volume, time scale and price scale (linear and logarithmic, precision of
  the tick size), crosshair with the values of the candle below it, zoom and pan (CH1, CH2);
  all times UTC (MD7); the current candle shown as in progress (MD8)
- Data layer of the module: fetching pages from `/api/candles`, a page cache, loading older candles
  while scrolling back (CH4), the error of a timeframe without data
- Keeping the number of visible candles and the anchor when timeframe or asset changes (CH3);
  last-close line with label, jump to the latest candle (CH5)
- `ChartHost` in Web and the part of the interop contract this epic needs: createPane, setSeries,
  setOptions, refresh, dispose
- The chart page: asset and timeframe in the address, selectors for both, price scale mode,
  several windows side by side (CP1); opening TradeViewer without an address opens the last chart
  (CP3)
- Chart settings per asset in the workspace, saved automatically (CP2): of the settings the
  specification names, this epic has the price scale mode; the others come with their epics into
  the same record
- Live update: `CandlesUpdated` refreshes the open chart without a reload (MD2)
- Measurement of NF1 and NF2 on WSL and on the Pi

Out of scope (later epics):

- Indicators and their panes, drawings and their tools, overlays, gap markers (their epics); the
  interop calls for them (setIndicators, setDrawings, setOverlays, setTool and the events back)
- The watchlists: until they exist, the asset is chosen from a plain selector on the chart page
  and from a link on the assets page (Watchlists: AS1, AS6)
- Timeframes a source does not provide (Downloader Monitoring: MD3); the selector shows the
  timeframes of the asset
- The in-memory series cache of the Api, unless step 8 shows that NF1 is missed on the Pi

---

# Steps

```text
1  Lightweight Charts pinned (exact version), bundled; a first pane with candles from the Api;
   proof of a series primitive and a pane primitive; licence notice kept                        done
2  Data layer (TypeScript): candle client, columns -> bars, page cache, merging pages, the
   decision when to load older candles; Vitest                                                  done
3  ChartPaneController: candle and volume series, UTC time scale, price scale (linear, log,
   precision from the tick size, auto fit and reset), crosshair legend, current candle style    done
4  Behaviour: visible range kept on timeframe and asset change (CH3), paging without a visible
   interruption (CH4), last-close line, jump to latest (CH5); the range rules as pure functions
   with Vitest                                                                                  done
5  Workspace: chart settings per asset (schema script 0003, JSON with a payload version), last
   chart as a setting; feature ChartSettings; /api/chart-settings, /api/settings extended;
   removing an asset deletes its chart settings                                                 done
6  Web: ChartHost and the interop contract (createPane, setSeries, setOptions, refresh, dispose);
   chart page /chart/{source}/{symbol}/{timeframe} with asset and timeframe selectors, scale
   mode, legend; start page opens the last chart; link from the assets page                     done
7  Live update on CandlesUpdated (the last candles are read again, the view stays), reload after
   a reconnect of the hub                                                                       done
8  Measurement NF1 and NF2 (WSL, then the Pi with the owner's go-ahead), Docker build,
   architecture chapter 10 and README updated                                                   done, Pi open
```

Each step has its tests: Vitest for everything in the module that is a rule (pages, ranges, time to
candle, price format), C# unit tests for the handlers, integration tests for the new endpoints.
What only a browser shows (the drawing itself, the feel of zoom and pan) is checked by hand, as
architecture chapter 14 says; see the first question below.

---

# Decisions of This Epic

Accepted by the owner on 2026-11-15. They are written into the architecture when the step that
needs them is done.

- **Version:** Lightweight Charts 5.3.0, pinned exactly (no `^`), as a dependency of
  TradeViewer.Chart. Version 5 has panes and pane primitives, which the indicator and overlay epics
  need; the reference project's 4.2 does not. An update of the library is a deliberate step with
  the chart checked by hand.
- **Licence notice:** the attribution logo of the library stays switched on (decision 0001).
- **Volume** like in TradingView (owner's decision): a histogram in the lower part of the candle
  pane, on its own hidden scale, coloured by the direction of its candle and drawn pale, so the
  candles above it stay readable.
- **Colours and theme:** rising candles green (#089981), falling red (#f23645), as the application
  already uses; the chart follows the light or dark setting of the system like the rest of the
  pages. The current candle is drawn paler than closed ones.
- **Address:** `/chart/{source}/{symbol}/{timeframe}`, e.g. `/chart/binance/BTCUSDT/1h`. Changing
  asset or timeframe changes the address without a page load, so back and forward work.
- **Start page:** `/` opens the last chart (CP3); without one, or when its asset is gone, the
  assets page.
- **Chart settings** are one JSON record per asset with a payload version, replaced as a whole
  (chapter 9). In this epic it holds the price scale mode. The last chart (asset and timeframe) is
  a setting of the workspace, written when a chart is opened; with several windows the last one
  opened wins (WS1).
- **Visible range:** the module keeps the number of visible candles. At the latest candle it stays
  there with the same empty space to the right; scrolled back it keeps the time at the right edge
  in view (CH3). The rules are pure functions and tested.
- **Paging:** the first page has 2,000 candles; older pages of 5,000 are requested when fewer than
  about 500 candles are left to the left of the view, so the user does not reach the edge (CH4).
  Pages stay in memory per asset and timeframe while the window shows that asset.
- **Live update:** on `CandlesUpdated` for the shown asset the module reads the newest page again
  and replaces the candles from the first one that differs; zoom and position stay. The same
  happens for the current candle of larger timeframes when the smallest one changes.
- **Hub client** stays in Web (C#), as built; the chart module itself has no connection.
- **Errors:** an asset without candles in a timeframe, an unknown asset and an unreachable server
  are shown as a message in place of the chart, never as an empty chart.

---

# Answers of the Owner (2026-11-15)

1. A headless Chromium for the agent on WSL is fine. The owner installs it himself with the
   agent's instructions (Playwright in `~/tools/browser`, outside this repository). It is a tool of
   the development machine, not a part of the solution; architecture chapter 14 stays as it is.
2. Volume like in TradingView: in the lower part of the candle pane.
3. No other change to the proposals.

---

# Result

```text
Tests      225 unit, 163 integration, 19 architecture (C#); 55 Vitest tests in the chart module
Browser    headless Chromium (Playwright in ~/tools/browser on the development machine, installed by
           the owner; not a part of the solution). Scripts there drive the chart page like a user:
           chart-check, paging-check, settings-check, live-check, measure, shot (screenshots)
Proved     in the browser, on the frozen copy (BTCUSDT and ETHUSDT, complete history):
           - candles, volume, scales, legend, last-close line, current candle; light and dark; log
           - timeframe change keeps 150 candles in view; at the latest candles it stays there, scrolled
             back it keeps the time at the right edge; asset change shows the latest candles (CH3)
           - scrolling back 60 drags: 5,000 older candles loaded, the view never jumped (CH4)
           - jump to latest; address, back button; unknown asset and timeframe show a message
           - scale mode saved per asset, seen by a second window; start page opens the last chart
           - live: candles appended to files of a temporary output root reach two open charts
             without a page load; the current 1h candle follows the 15m candles and is replaced by
             the closed one; a scrolled-back view does not move
NF1        click to painted chart: worst 112 to 123 ms (the first change), median 76 ms; of that the
           module (request and drawing) 13 to 30 ms. Target 150 ms. WSL, headless Chromium
NF2        zoom, pan, crosshair: 60 frames per second (95 % of frames within 16.8 ms), 0 requests
Docker     amd64 image: chart and start page work from the container, 57 MB of 512 MB
Open       the Pi (deployment needs the owner's go-ahead); the owner's own look at the chart in a
           real browser: the feel of zoom and pan cannot be judged from a script
```

Found on the way:

- A styling rule for buttons overrode the `hidden` attribute, so the jump-to-latest button always
  showed. Elements marked as hidden now stay hidden.
- Reading the settings of an asset first tore the chart down on every asset change, which lost the
  kept view. The chart now stays and switches once the settings are read.
- PUT /api/settings replaced the settings as a whole; with the last chart in the same record the
  settings page and a chart would have overwritten each other. A part that is null now stays.
- The frame rate is capped at 60 by the headless browser and measured on a desktop machine; it
  says that the chart is not the bottleneck, not what an old laptop does.
