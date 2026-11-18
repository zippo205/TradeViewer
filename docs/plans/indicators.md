# Implementation Plan: Indicators

## Status

```text
Epic:     Indicators (P1 - Workstation)
Status:   Steps 1 and 2 done 2026-11-18
Branch:   feature/indicators
Next:     step 3
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
   tests (t equals the candle page, current candle included, seams, 400/404/422)                open
4  Chart settings and templates: ChartSettings with indicator instances (Domain, contract,
   validator, stored payload), script 0007 and the template repository, template commands and
   /api/indicator-templates, hub event ChartSettingsChanged; tests                              open
5  Chart module indicators/: client, data with paging and refresh, set with panes and series,
   band fill, legends; setIndicators in the interop; Vitest                                      open
6  Web: ChartHost.SetIndicatorsAsync, the bar with Indicators and Templates, the settings dialog,
   legend actions (hide, settings, remove), saving with the chart settings, other windows follow
   on ChartSettingsChanged; browser checks                                                       open
7  NF1 and NF2 with six indicators, browser checks, Docker image, architecture (chapters 4, 6, 9,
   10, 11), plan result; then the owner's look                                                    open
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
