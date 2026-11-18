# Implementation Plan: Indicators

## Status

```text
Epic:     Indicators (P1 - Workstation)
Status:   Completed and approved by the owner 2026-11-18
Branch:   feature/indicators
Next:     merged into main; the Pi only with the owner's go-ahead
Baseline: docs as of commit 30f5e68 (main)
```

Update this section whenever a step is completed.

---

# Why This Epic Comes Now

- The owner chose it after Symbol Catalog (2026-11-17). It needs nothing from EWTS.
- The Scenario Viewer still waits for EWTS's drawing files; Scenario Analysis waits for the viewer.
- The table `chart_settings` and its feature already exist (Drawing Tools, "drawings hidden"); the
  indicators are the main part of what CP2 says a chart keeps per asset.

---

# Knowledge Sources

```text
docs/specification.md
    -> IN1, IN2 (the six indicators, their defaults, templates), CP2 (indicators saved per asset,
       changes saved automatically), NF1 and NF2 (drawn within 150 ms, 60 fps without requests),
       MD8 (the current candle; indicators use it too), WS1 (nothing lost)
docs/architecture.md
    -> chapter 4 (features ChartSettings and Indicators; Domain Indicator, IndicatorInstance,
       IndicatorTemplate, ChartSettings), 6 ("Serving Candles": indicators calculated on request
       with their warm-up, in the column format), 9 (/api/indicators, /api/indicator-templates,
       /api/chart-settings), 10 (chart module indicators/, setIndicators, values never through
       interop), 11 (indicator templates table, instances stored as JSON with a discriminator)
docs/plans/price-chart.md, docs/plans/drawing-tools.md
    -> paging, refresh and NF1 measurement of the chart; the chart settings feature and how Web
       saves it
```

No interface document is needed: indicators come from TradeViewer's own candle store.

---

# Scope

In scope:

- The six indicators of IN1, calculated in the Domain in C#, checked against reference values:
  SMA, EMA, Bollinger Bands on the price; RSI, MACD, ATR each in its own pane below
- Any number of indicators per asset, also several of one kind (SMA 50 and SMA 200)
- Per indicator: its parameters, the price source where it has one (open, high, low, close, hl2,
  hlc3, ohlc4), colour and width of each line, shown or hidden
- Calculated from the candles of the timeframe shown, including the current candle (MD8); they
  page back with the candles (CH4) and follow new candles (MD2)
- A legend line per indicator at the top left of its pane, with its values under the crosshair and
  the actions hide, settings, remove (as in TradingView)
- Saved per asset in the chart settings, restored in any window (CP2); saved at once (WS1)
- Templates (IN2): save the indicators of the chart under a name, apply one to a chart, delete one

Out of scope:

- Further TradingView options: offset, the type of moving average inside BB, MACD and RSI (always
  as in TradingView's default), RSI smoothing line, bands of RSI other than 70 / 50 / 30
- Indicators on indicators, own scripts, other indicators: the specification names six
- Drawings in an indicator pane: drawings stay in the price pane
- Saving the height of indicator panes (proposal 5)

---

# Design

## Calculation (Domain)

```text
Domain/Indicators/
├── IndicatorType        Sma, Ema, BollingerBands, Rsi, Macd, Atr (codes sma, ema, bb, rsi, macd, atr)
├── PriceSource          Open, High, Low, Close, Hl2, Hlc3, Ohlc4
├── IndicatorParameters  per type, with the defaults of IN1 and the rules (length 1 to 5,000, fast < slow, ...)
├── IndicatorInstance    id (UUID from the client), type, parameters, style (colour and width per line), hidden
├── IndicatorTemplate    id, name, instances
├── Indicator            candles + parameters -> named lines of double? (null where not defined yet)
└── WarmUp               how many candles before the first wanted one a calculation needs
```

The formulas follow TradingView's built-in definitions, so values equal TradingView's from the
start of the history on:

```text
SMA    mean of the last n sources
EMA    alpha = 2 / (n + 1), seeded with the SMA of the first n values (Pine's ta.ema)
RMA    alpha = 1 / n, seeded with the SMA of the first n values (Wilder; used by RSI and ATR)
BB     basis SMA(n), upper / lower basis +- k * population standard deviation of the last n
RSI    100 - 100 / (1 + RMA(gain, n) / RMA(loss, n))
MACD   EMA(fast) - EMA(slow); signal EMA(signal) of it; histogram = MACD - signal
ATR    RMA(true range, n); the first true range is high - low
```

Warm-up: SMA and BB need n - 1 candles before. EMA, RMA and the indicators built on them have no
end of memory; they get as many candles before as make the influence of the starting value smaller
than 1e-10 (EMA 9: about 100, RSI 14: about 320, MACD 26: about 300). Two pages calculated with
their own warm-up then meet without a visible step, and a value equals TradingView's to the
precision of the price.

## Endpoint

```text
GET /api/indicators/{source}/{symbol}/{timeframe}?type=bb&length=20&mult=2&price=close&to={time}&limit={n}
-> { "t": [...], "lines": { "basis": [...], "upper": [...], "lower": [...] }, "partial": true }
```

- One request per indicator and page, with the same `to` and `limit` as the candle page, so `t` is
  the `t` of that candle page, one value per candle; `null` where the indicator is not defined yet
  (the first n - 1 candles of the history; MACD n = slow, its signal and histogram slow + signal - 2;
  RSI n candles, it needs n changes).
- The candles are read exactly as /api/candles reads them (store, aggregation of MD3, current
  candle of MD8): that code moves out of `GetCandlesQueryHandler` into one reader both features use,
  plus the warm-up candles before the page.
- 404 unknown asset, 422 timeframe the asset does not have, 400 with the fields for parameters that
  break the rules.
- The parameters travel in the query, not as an instance id: the endpoint calculates, it does not
  read the chart settings. The module asks for what it shows.

## Chart Module

```text
TradeViewer.Chart/src/indicators/
├── indicatorClient.ts   fetches a page of one indicator (same to/limit as the candle page)
├── indicatorData.ts     the values the window holds per indicator, paged and refreshed with the candles
├── indicatorSet.ts      the series and panes of the indicators of the chart: one pane per RSI, MACD, ATR
├── bandFill.ts          a series primitive that fills between two lines (BB background, RSI 30-70)
└── legend.ts            one line per indicator at the top left of its pane, values under the crosshair
```

- `setIndicators(instances)` replaces what is shown; unchanged instances keep their values, a
  changed parameter fetches again, a changed colour does not.
- The candles are painted first; the indicators are fetched in parallel with them and painted when
  they arrive. NF1 is measured for both (step 7).
- An older candle page triggers the older page of every indicator; a refresh refreshes them.
- Panes: price pane first, then the oscillators in the order they were added. Lightweight Charts 5.3
  has panes with a shared time scale and crosshair, resizable separators and stretch factors.
- Drawing tools act in the price pane only; a pointer in an indicator pane does what it does on an
  empty chart (to be proved in step 1: `PointerInput` sits over the whole canvas today).

## Web

- A bar above the chart gets "Indicators" (a dialog with the six to add) and "Templates" (save the
  current set under a name, apply, delete).
- The settings dialog of an indicator: parameters, price source, colour and width per line.
- `ChartSettings` (Domain, contract, stored JSON) gets the list of instances. Every change replaces
  the settings as a whole, as now, through the existing save of "drawings hidden".
- A new hub event `ChartSettingsChanged(source, symbol, window)` (proposal 3).

## Workspace

- Chart settings: the stored payload gets `indicators`; an added field needs no new payload version
  (the repository's rule). A later kind of indicator that changes the shape raises the payload
  version, which an older build already ignores.
- Script 0007: table `indicator_templates` (id, name unique, payload_version, payload, updated_at).
  Templates belong to no asset and survive the removal of every asset.

---

# Steps

```text
1  Proof in the browser: indicator panes in Lightweight Charts 5.3 (add, remove, order, stretch,
   resize by the separator, legend per pane from paneSize), the band fill primitive, and that the
   drawing tools ignore pointers outside the price pane; nothing stays that is not used later      done
2  Domain: IndicatorType, PriceSource, parameters with defaults and rules, the six calculations
   and WarmUp, IndicatorInstance, IndicatorTemplate; unit tests against reference values
   (question 1), at the start of the history and across a page seam                             done
3  Feature Indicators, calculation: one candle reader for /api/candles and /api/indicators
   (moved, not copied), GetIndicatorQuery with its validator, contract, endpoint; integration
   tests (t equals the candle page, current candle included, seams, 400/404/422)                done
4  Chart settings and templates: ChartSettings with indicator instances (Domain, contract,
   validator, stored payload), script 0007 and the template repository, template commands and
   /api/indicator-templates, hub event ChartSettingsChanged; tests                              done
5  Chart module indicators/: client, data with paging and refresh, set with panes and series,
   band fill, legends; setIndicators in the interop; Vitest                                      done
6  Web: ChartHost.SetIndicatorsAsync, the bar with Indicators and Templates, the settings dialog,
   legend actions (hide, settings, remove), saving with the chart settings, other windows follow
   on ChartSettingsChanged; browser checks                                                       done
7  NF1 and NF2 with six indicators, browser checks, Docker image, architecture (chapters 4, 6, 9,
   10, 11), plan result; then the owner's look                                                    done
```

---

# Result of Step 1 (2026-11-18)

Proved in headless Chromium with the real `ChartPane` on the frozen BTCUSDT 1d candles (local
Docker, Downloader off): BB with its fill in the price pane, RSI with the 30-70 fill, MACD (lines and
histogram) and ATR in panes 1 to 3. The proof page and its script stayed in the scratchpad.

- Panes: `addSeries(definition, options, paneIndex)` makes the pane; `IPaneApi.moveTo` orders;
  removing the last series of a pane removes the pane, a pane with series left stays.
  `setStretchFactor` divides the height (relative values; proposal 5's quarter is price 4, oscillator
  1); dragging a separator (cursor `row-resize`) changes the stretch factors of the two panes.
- `paneSize(i)` equals the pane element's height, the separator is 1 px. It throws until the
  library's next frame after a pane was added: legends are placed from a `ResizeObserver` on the pane
  elements (`IPaneApi.getHTMLElement`), which fires on every layout change (separator, pane added,
  moved or removed, window resize). The library has no event of its own for that.
- The crosshair event carries `paneIndex`: the legends of step 5 know which pane the pointer is in.
- Band fill: a series primitive with `zOrder` "bottom" drawing in `drawBackground` between two
  lines, or between two constant prices (RSI), only over the visible range.
- Drawing tools: `PointerInput` took presses in every pane, so a tool clicked in the RSI pane drew
  in the price pane. Fixed: a press below `paneSize(0)` goes to the library (`tools/pointerInput.ts`,
  kept); with a tool active it creates nothing there, without one a drag pans as on an empty chart.
  The fix stays because every later step needs it; nothing else of the proof is in the code.

---

# Result of Step 2 (2026-11-18)

- Domain `Indicators/`: `IndicatorType`, `PriceSource`, `IndicatorParameters` (one record per type:
  `SmaParameters`, `EmaParameters`, `BollingerBandsParameters` with `Deviation`, `RsiParameters`,
  `MacdParameters`, `AtrParameters` without a source; `Default(type)` gives IN1), `Indicator`
  (`Calculate` -> `IndicatorLine`s with one `double?` per candle; `Sma`, `Ema`, `Rma`, `Prices`),
  `IndicatorLines` (line names: value; basis, upper, lower; macd, signal, histogram), `WarmUp`,
  `IndicatorLineStyle` and `IndicatorStyles` (TradingView's colours, width 1), `IndicatorInstance`
  (`New(id, type)`), `IndicatorTemplate`, `IndicatorRules` (problems by field, as `DrawingRules`),
  `IndicatorCodes` (sma, ema, bb, rsi, macd, atr; open ... ohlc4).
- Style entries: one per line, except MACD: macd, signal, `histogramAbove`, `histogramBelow`; the
  chart draws falling columns in the lighter shade of each (step 5, the four shades of TradingView).
- Rules chosen: lengths 1 to 5,000, MACD fast < slow, BB deviation 0.001 to 50 (TradingView's
  range), at most 50 indicators per chart or template, template names 1 to 100 characters, ids
  distinct, colours as drawings write them.
- RSI is 100 without losses and 0 without gains, as TradingView's `ta.rsi`.
- `WarmUp.Candles`: SMA 9 -> 8, BB 20 -> 19, EMA 9 -> 112, RSI 14 -> 325, MACD 12/26/9 -> 437. A
  length of 5,000 on a smoothed indicator asks for about 63,000 (EMA) or 120,000 (RMA) candles;
  the reader of step 3 gives what the history has.
- Tests (80): the 20 values of TradingView to the cent on the whole fixture; the first value on
  the candle TradingView shows it when starting from 2017-10-01; a page with its warm-up equals the
  whole history (1e-8 relative) for twelve parameter sets at three seams; small series by hand;
  rules and codes. Fixture `tests/fixtures/indicators/BTCUSDT-1d.csv` (2,695 candles, 112 KB, from
  the frozen copy).

---

# Result of Step 3 (2026-11-18)

- The candle reader is a port, `ICandleSeriesReader` (Abstractions/MarketData), implemented by
  `Infrastructure/MarketData/Store/CandleSeriesReader`: the architecture lets no feature use another
  feature's code, and a candle series several features need is a port. The code moved out of
  `GetCandlesQueryHandler` unchanged (limit, asset, timeframe, current candle) and gained the warm-up:
  up to n candles before the page, fewer at the start of the history. `CandleSeriesPage` holds warm-up
  and page; the page limits (2,000 / 5,000) moved from `GetCandlesQuery` to the port.
- Feature `Indicators`: `GetIndicatorQuery`, `GetIndicatorQueryValidator` (returns the parameters or
  the fields), `GetIndicatorQueryHandler`; contract `IndicatorResponse` (`t`, `lines`, `partial`);
  `GET /api/indicators/{source}/{symbol}/{timeframe}?type=&length=&deviation=&fast=&slow=&signal=&price=&to=&limit=`.
- Query names: `deviation` (not `mult`) as in the Domain, `price` for the source (`source` is the
  route's). A parameter left out takes the IN1 default; a parameter the type does not have (`fast` for
  sma, `price` for atr) is a 400, so a client mistake is not silently ignored.
- Tests: 21 unit tests of the handler (every type equals the whole history on the latest page, two
  pages meet, nulls at the start, defaults, the current week, the errors by field); 5 integration
  tests (t equals the candle page, the warm-up from before the page, nulls at the start of the
  files, the current 1h candle, an older page, 400/404/422 as problem details).
- Checked in the local container on the frozen data: all 22 values of TradingView come out of the
  endpoint; 2,000 RSI values of 15m in 14 ms.

---

# Result of Step 4 (2026-11-18)

- Templates moved to the feature ChartSettings (architecture chapter 4 corrected): the indicators of
  the chart settings and of the templates are converted between API and Domain by one mapping
  (`IndicatorMapping`), and no feature may use another feature's code. Indicators keeps only the
  calculation.
- Domain: `ChartSettings(DrawingsHidden, Indicators)`. Contracts `Indicators/`: `IndicatorRecord`
  (type code and the parameters by the names of /api/indicators, null where the type has none),
  `IndicatorStyleRecord`, `IndicatorTemplateRecord`; `ChartSettingsResponse` gained `Indicators`.
- A PUT without `indicators` (null) keeps the saved ones; only an empty list removes them. An open
  window of the build before this epic sends only `drawingsHidden` and must not wipe indicators
  (priority 1). Every answer names the indicators.
- A request must give exactly the parameters of its type (missing or foreign ones are 400); the
  problems name the fields as sent (`indicators[0].length`, `indicators[0].price`,
  `indicators[0].styles.value.color`).
- Storage: `IndicatorPayload` writes each indicator with its type code as discriminator and only its
  parameters; the chart settings payload stays version 1 (an added field). An entry this build
  cannot read is left out, the others stay. Script 0007: `indicator_templates` (name unique without
  regard to case), payload version 1; templates belong to no asset.
- `/api/indicator-templates`: GET (by name), PUT /{id} (create or replace; 409 for a name another
  template has, whatever the case; the name is trimmed), DELETE /{id} (204, 404). Applying a template
  is the client's work: copies with new ids into the chart settings (step 6).
- Hub event `ChartSettingsChanged(source, symbol, window)`; the PUT of the chart settings reads the
  window from `X-TradeViewer-Window`, as the drawings do. No event for templates: the list is read
  when the menu opens.
- The validator of step 3 now follows the pattern of the others (`Error? Validate(query, out ...)`).
- Tests: 13 unit tests of the handlers, 7 storage tests (all six kinds back exactly after a restart,
  a payload of the earlier build, an unreadable entry, the upgrade from version 6 with its backup,
  templates by name, replaced, deleted, kept when every asset goes, the unique name), 4 API tests
  (JSON shape, the hub event with the window, a client without indicators keeps them, problems by
  field, templates 200/400/409/204/404). A test that named `before-v6` now takes the latest version.

---

# Result of Step 5 (2026-11-18)

- `indicators/`: `indicator.ts` (the instance as the Api's `IndicatorRecord`, so Blazor passes the
  chart settings unchanged; lines, price or own pane, the key of the values, TradingView's titles,
  MACD shades), `indicatorClient.ts`, `indicatorData.ts` (`IndicatorValues`: newest page, older pages
  in front, refresh from its first time on, a gap reported), `bandFill.ts`, `legend.ts`,
  `indicatorSet.ts` (series, panes, fills, legends, fetching).
- `ChartPane`: `setIndicators`, `indicators`, `indicatorValues` (checks), `onIndicatorActions`; stats
  `indicators` and `indicatorRequests` (apart from the candle requests NF2 counts). Bridge
  `bindIndicatorActions` reports `IndicatorAction(id, action)` to ChartHost (step 6).
- Fetching: a new series fetches the newest page of every indicator in parallel with the candles; what
  arrives is drawn together with the candles, so the values of the new asset never stand beside the
  old candles. Scrolling back fetches the same older page of every indicator in parallel with the
  candles. A refresh asks for the newest 500 values (`REFRESH_PAGE_SIZE`); when they do not join
  what is held (a window asleep), it reads back to the oldest candle again. A new indicator, or a new
  parameter, on a chart scrolled back reads until it reaches the oldest candle.
- Panes: a new oscillator pane gets a quarter of the price pane's stretch (proposal 5); the
  histogram is added before the MACD lines so they are drawn over it. Oscillator panes stay linear
  when the log scale is on (found in the browser: MACD below zero cannot be shown on a log scale).
- Legends: price indicators in a column below the candle legend; each oscillator at the top of its
  pane, placed from `paneSize` on every resize of a pane element (ResizeObserver, step 1). Values in
  the line's colours, the MACD histogram in the colour of its column; actions shown on hover; a press
  on them never reaches the chart.
- MACD shades: TradingView's rule (a column grows only when the one before is smaller); the pale shade
  mixes with white by 0.68, within 10 of 255 per channel of #B2DFDB and #FFCDD2.
- RSI: lines at 70, 50, 30, fill between 70 and 30 in the line colour at 10 %; BB fill #2196F3 at 5 %.
- Styles for the legend are in `app.css` with the other chart overlays.
- Tests: 12 new Vitest (216 in all). Browser check on the local container with the bundle of the
  working tree: six indicators in six requests; the values of TradingView for 2025-01-01 and, after
  scrolling back to 2017, for 2020-03-12; every indicator reaches back to the oldest candle; a colour
  costs no request, a length does; hidden and removed without a request; the settings action
  reported without moving the chart; 4h has one value per candle up to the current one; pan and zoom
  without a request; log scale only on the price pane; light and dark.

---

# Result of Step 6 (2026-11-18)

- `GET /api/indicator-defaults` (feature ChartSettings): a new indicator of each kind built from the
  Domain, ids empty. Web references only Contracts; this way the defaults of IN1 keep one home (the
  Domain) instead of a copy in Web.
- Web: `IndicatorsApi` (defaults, templates); `DrawingsApi.SaveChartSettingsAsync` names the window
  and is sent with keep-alive; `WorkspaceEvents.ChartSettingsChanged`; `ChartHost.SetIndicatorsAsync`
  and `OnIndicatorAction`; components `IndicatorMenu` ("ƒx Indicators"), `TemplateMenu`
  ("Templates": apply, save under a name, delete after a second click), `IndicatorSettingsDialog`
  (parameters, source, colour and width per line, Defaults, Cancel, OK), `IndicatorNames`.
- Chart page: the indicators of the asset come with its chart settings; another asset drops them at
  once, before its candles are read. Adding, the legend actions and OK in the dialog show at once and
  save at once, one save at a time, each with what the window shows. Until the settings of the asset
  are read, the menus are disabled and a save of "drawings hidden" leaves the indicators out (null),
  which keeps the saved ones. A change the Api refuses keeps the dialog open with the reason and
  changes nothing. Another window's ChartSettingsChanged reads the settings again.
- Menus close on a click beside them, as in TradingView.
- Not done: undo and redo for indicator changes (DR2's undo covers drawings; IN1 asks for none).
- Tests: one API test (the defaults are IN1's and can be saved as they come). Browser check in two
  windows on the local container (it restores the chart settings of BTCUSDT and deletes its template
  afterwards): six added from the menu, saved, window B (4h) follows; the dialog changes RSI to 21
  and green, B follows; a refused MACD (fast 30 > slow 26) stays in the dialog with the reason; EMA
  hidden and ATR removed from the legend, B follows; a template saved, applied with new ids, the
  template unchanged; "drawings hidden" keeps the indicators; a reload and another asset and back;
  deleting a template asks once more.

---

# Result of Step 7 and of the Epic (2026-11-18)

```text
Delivered  IN1: the six indicators, calculated in C# as TradingView does (all 22 values of the
           owner's Data Window to the cent), any number per asset and of one kind, parameters,
           price source, colour and width per line, hidden; on every timeframe from its own
           candles, the current candle included; paged with the candles; a legend line per
           indicator with hide, settings, remove; "Indicators" in a bar above the chart
           IN2: templates: save, apply (copies, replacing), delete
           CP2: indicators saved per asset with the chart settings, at once, other windows follow
Tests      .NET 731 (unit 497, integration 215, architecture 19), Vitest 216, format clean
Browser    step 5 (module) and step 6 (the page in two windows) checks pass on the final image
NF1        click to painted candles and six indicators: worst 128 ms, median 95 ms (150 ms);
           the indicators arrive first and are painted with the candles. WSL, headless Chromium
NF2        zoom, pan, crosshair with six indicators: 60 frames per second (no frame over
           16.8 ms), 0 requests
Docker     amd64 image built and run on WSL: 56 MB of 512 MB; workspace upgraded 6 -> 7 with its
           backup before
Docs       architecture chapters 4, 5, 6, 9, 10, 11; this plan
Open       the owner's own look in a real browser; the Pi (needs the owner's go-ahead: it brings
           schema 7, with the backup before-v7); undo for indicator changes is not built (IN1
           asks for none)
```

- `ChartPane.stats()` gained `lastIndicatorsMs` for the measurement: from setSeries until every
  indicator of the new series was painted.
- Measurement script and the browser checks are throw-away scripts in the session's scratchpad, as
  in the earlier epics (architecture chapter 14: no browser tests in V1).

---

# Proposals (accepted unless the owner objects)

1. **Calculated on the server in C#**, as the architecture says, one request per indicator and page.
   If NF1 is missed with six indicators on the Pi, the requests of one page are joined into one;
   that is not built in advance.
2. **TradingView's built-in definitions**, seeds included (EMA from the SMA of its first n values, RSI and ATR
   with Wilder's RMA, population standard deviation in BB).
3. **Hub event `ChartSettingsChanged`**: today two windows on the same asset can overwrite each
   other's settings, because a save replaces them as a whole. With indicators that would lose a
   user's work (priority 1). The event lets the other window read the settings again, as
   `DrawingsChanged` does for drawings.
4. **Applying a template replaces** the indicators of the chart (TradingView does the same); the
   template stays unchanged when the chart's indicators change later.
5. **Pane heights are not saved**: a new oscillator pane gets a quarter of the height of the price
   pane, a reload lays them out again. Saving them can follow when the owner misses it.
6. **No limit on the number of indicators** beyond what the dialog makes sensible; each oscillator
   is its own pane.

---

# Questions for the Owner

Answers of 2026-11-17: (2) BB basis blue, upper red, lower green: IN1 corrected; (3) the bar above
the chart, as in TradingView; (4) the price source can be chosen, default close: IN1 updated. (1)
answered 2026-11-18 with the Data Window values of TradingView (below); no export needed.

Reference values (BINANCE:BTCUSDT 1D, defaults, UTC). They equal the frozen candles' values to the
cent, calculated as in Design; the candles of both dates are the same.

```text
                SMA 9     EMA 9     BB basis  BB upper   BB lower  RSI 14  MACD     signal   hist     ATR 14
2020-03-12      8046.26   7642.72   8600.39   10673.63   6527.15   15.04   -625.32  -318.32  -307.00  641.76
2025-01-01     95353.54  95027.70  98025.68  106081.01  89970.34   46.13   -714.81   113.28  -828.09  3795.47
```

- The owner first wrote the histogram of 2025-01-01 as -829.09; a screenshot of TradingView showed
  -828.09 (a typo). The screenshot also shows the legend line as TradingView writes it (name,
  parameters, then the values: "MACD close 12 26 9  -828,09 -714,81 113,28"), the histogram in four
  shades (rising and falling, above and below zero) and ATR in dark red: the reference for step 5.
- TradingView's history of the pair starts on 2017-10-01, the Downloader's on 2017-08-17. The first
  values TradingView showed (SMA and EMA 9 on the 9th candle, ATR 14th, RSI 15th, BB 20th, MACD 26th,
  its signal 34th) prove that EMA is seeded with an SMA, not with the first price; the plan said
  otherwise and is corrected. Values from 2020 on do not depend on where the history starts.
- No test-only library is needed: the tests check the two dates, the first defined candle of each
  indicator, and a page seam.

1. **Reference values.** Best would be an export from your TradingView: BINANCE:BTCUSDT, 1D, with
   the six indicators in their defaults ("Export chart data" writes the indicator values into the
   CSV). Without it the tests use small hand-calculated series and the published worked examples
   of the formulas, and the comparison with TradingView is your look at the chart.
2. **Colours.** The colours of IN1 were written from memory. TradingView's built-in scripts today
   use: SMA and EMA #2962FF; BB basis #2962FF, upper #F23645, lower #089981, fill blue at 95 %
   transparency; RSI #7E57C2 with bands 70 / 50 / 30 grey and a purple fill; MACD #2962FF, signal
   #FF6D00, histogram #26A69A / #B2DFDB / #FF5252 / #FFCDD2; ATR #B71C1C. That differs from IN1 for
   the BB basis (orange in IN1). Can you check two or three of them in your TradingView? I then
   correct IN1 before step 2.
3. **Where the controls sit.** Proposed: a bar above the chart with "Indicators" and "Templates", and
   the actions of an indicator in its legend line (hide, settings, remove), as in TradingView. Or do
   you want them somewhere else, e.g. in the tool rail at the left?
4. **Price source.** IN1 names "source close"; proposed is the choice TradingView offers (open,
   high, low, close, hl2, hlc3, ohlc4). Fine, or close only?
