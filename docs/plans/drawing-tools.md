# Implementation Plan: Drawing Tools

## Status

```text
Epic:     Drawing Tools (P1 - Workstation)
Status:   Plan approved by the owner 2026-11-16, all proposals accepted. All 10 steps done (2026-11-17); waiting for the owner's look and approval
Branch:   feature/drawing-tools
Next:     owner's look (Docker on WSL, localhost:8080), approval, merge; Pi on go-ahead
Baseline: docs as of commit 50c0225 (main)
```

Update this section whenever a step is completed.

---

# Why This Epic Comes Now

- It is the largest package that needs nothing from EWTS, and two later epics build on it:
  Scenario Analysis (the user's own analysis on a scenario) and Analysis Overlays.
- It is the first epic in which the user creates analysis, so it is the first real test of
  priority 1 of AGENTS.md: what the user creates is never lost.
- The Scenario Viewer still waits for the drawing files of EWTS (its epics Scenarios and Drawings
  are open).

---

# Knowledge Sources

```text
docs/specification.md
    -> 4.6 (DR1-DR6), CP2 (drawings hidden is a setting per asset), AS5 (removing an asset deletes
       its drawings), WS1 (saved at once, last save wins), NF3, SC10 (the same tools on a scenario)
docs/architecture.md
    -> chapter 5 (Drawing, Anchor, FibonacciPreset), 9 (/api/drawings, /api/fibonacci-presets,
       idempotent PUT with a client id), 10 (interop contract, tools/ and primitives/), 11 (JSON
       payload with type and version), 14 (what is tested where)
docs/decisions/0007-scenario-analysis.md
    -> the drawing records are written into the expert file later: they must not depend on the
       workspace
../relatedProjects/OpenCharts/src/lib/chart-plugins/drawing-tools   (studied, nothing copied)
src/TradeViewer.Chart/node_modules/lightweight-charts (5.3.0)        (the time scale was read)
```

---

# Study: Time to X (asked for by the backlog)

What OpenCharts does (Lightweight Charts 4.2):

- A drawing keeps Unix seconds and a price per anchor. For x it first asks the library
  (`timeToCoordinate`), which answers only for a time that is exactly the time of a bar.
- Otherwise it computes a bar index itself: between two bars it **interpolates** (an anchor at
  10:45 on a 1h chart sits three quarters of the way from the 10:00 to the 11:00 bar); after the
  last bar and before the first it extrapolates with the length of the timeframe.
- The library draws a fractional index at x = 0, so OpenCharts asks for the two whole indices
  around it and interpolates the pixels.
- From x to time it rounds to whole bars, also in the empty space to the right.
- All drawings live in one series primitive; mouse events are taken on the container in the
  capture phase, so the chart does not pan while a drawing is dragged.

What I checked in Lightweight Charts 5.3.0 (the pinned version):

- `timeToCoordinate` still answers only for exact bar times; `logicalToCoordinate` still returns 0
  for a fractional index, and is linear for whole indices, also far outside the candles.
- So a whole index is always safe, on both sides of the data.

The mapping proposed for TradeViewer (a pure function, tested with Vitest):

```text
time inside the candles     the index of the candle that contains the time (the last candle whose
                            open time is not after it). Never between two candles: an anchor always
                            sits on a candle, as the architecture already says (chapter 10)
time after the last candle  last index + whole timeframes since the last open time (DR3)
time before the first       first index - whole timeframes before it, rounded outwards. It becomes
candle in memory            exact as soon as scrolling back has loaded the candles (CH4)
x to time                   the nearest whole index; its candle's open time, or the same
                            extrapolation outside the candles
```

Why "the candle that contains it" and not interpolation as in OpenCharts: an anchor on the high of
the 10:45 candle (15m) belongs, on the 1h chart, on the 10:00 candle, whose range holds that
price. Interpolated, it would hang in the air between two candles.

Known limits, accepted:

- The extrapolation counts timeframes of equal length. That is right for crypto (continuous
  session). Sessions with closed hours (stocks) need the session calendar there; that comes with
  those sources (roadmap, Later).
- A straight line between two anchors is straight in candles and pixels. Where a timeframe has a
  gap, the same line passes a slightly different price in between on another timeframe. TradingView
  behaves the same.

---

# Scope

In scope:

- The twelve tools of DR1 with select, move, reshape by handles, restyle, delete, lock (DR2)
- Magnet, on by default (DR2); anchors to the right of the last candle (DR3)
- Drawings per asset on every timeframe (DR3), saved in the workspace in the background (NF3, WS1)
- Undo and redo for the session (DR2)
- Fibonacci presets with the two defaults, edited on the settings page (DR4); levels in log space
  on a logarithmic scale
- Wave labels of the nine degrees, alone and on the points of a polyline (DR5)
- Hide and show all drawings of a chart (DR6), saved per asset (CP2): this brings the feature
  ChartSettings and its endpoint, which the architecture left for "the first epic that has a
  setting per asset"
- Removing an asset deletes its drawings (AS5): the confirmation text says so

Out of scope:

- The drawing layer of the scenario page and the expert file (Scenario Analysis); the module and
  the records are built so that it only needs another place to save
- The shapes of EWTS's overlays (Scenario Viewer, Analysis Overlays)
- Touch input (NF11: desktop browsers), copy and paste, multi-selection, templates per tool,
  alerts on lines, a measuring tool, a list of all drawings

---

# Steps

```text
1  Chart module, proof first: the time mapping as pure functions with tests; one drawing layer
   (series primitive) that draws a trend line from data and reports a hit; pointer events taken
   before the chart so that dragging does not pan. Checked in the headless browser               done
2  Domain and contracts: Drawing, Anchor, tool types with their rules (number of anchors, style,
   payload version), FibonacciPreset with the defaults, wave degrees and labels; unit tests      done
3  Workspace: script 0004 (drawings, fibonacci_presets), repositories; ChartSettings repository
   on the table of script 0003; tests with upgrade and restore                                   done
4  Features Drawings and ChartSettings: queries, commands, validators, endpoints
   (/api/drawings, /api/fibonacci-presets, /api/chart-settings), hub event DrawingsChanged;
   handler and endpoint tests                                                                    done
5  Chart module: tool state machine (place, select, move, reshape, cancel), hit testing, handles,
   magnet, lock; the simple tools: trend line, ray, horizontal line, vertical line, rectangle,
   arrow, text note, parallel channel; Vitest                                                    done
6  Web: ChartHost calls and events, the list of drawings with saving in the background (queue,
   retry, "not saved" mark), undo and redo, tool rail, bar for the selected drawing (colour,
   width, style, text, lock, delete), keys; hide all; unit tests for the client state            done
7  Chart module and Web: polyline, wave label, labels on the points of a polyline, the chooser of
   degree and label                                                                              done
8  Chart module and Web: Fibonacci retracement and extension (linear and log), presets on the
   settings page                                                                                 done
9  Exact anchor times across timeframes (see the proposal "Where a snapped anchor sits");
   accepted by the owner                                                                         done
10 Second window follows (DrawingsChanged); measurement of NF3 (frame times while dragging with
   200 drawings); checks in the headless browser; Docker; architecture, README and runbook
   updated                                                                                       done
```

Each step has its tests: Vitest for mapping, geometry, hit testing, the state machine, Fibonacci
levels and the magnet; C# unit tests for the rules of a drawing, the handlers and undo/redo;
integration tests for endpoints, repositories and the schema upgrade; scripted checks in the
headless browser for what only a browser shows (dragging, the bar, the keys).

---

# Result of Step 1 (2026-11-17)

- `tools/timeMapping.ts`: `timeframeSeconds` and `TimeMapping` (`indexOf`: the candle that contains
  the time, whole timeframes outside the candles, rounded outwards before the first; `timeOf`: the way
  back). Calendar months (1M, later) have no fixed length and are not handled yet.
- `tools/geometry.ts` (distance to a segment, hit tolerance), `tools/drag.ts` (anchors moved by whole
  bars and by pixels converted through the price scale, so a drag on the log scale keeps the ratio).
- `primitives/drawingLayer.ts`: one series primitive with trend lines from data; draws them, finds the
  topmost one under a point, reports it to the library (`drawing:<id>`, cursor "move").
- `tools/pointerInput.ts`: `mousedown` in the capture phase on the element around the chart; a press
  on a drawing never reaches the library, so it does not pan; the drawing follows the pointer and the
  result is reported once on release; Escape puts it back.
- `ChartPane.setDrawings`, `drawings`, `drawingPoints` and three figures in `stats()` for the checks;
  the calls of the interop contract and the events come with steps 5 and 6.
- 26 new Vitest tests (90 in all). Headless browser (`~/tools/browser/drawing-check.mjs`, BTCUSDT 1h
  of the development data): the "move" cursor on the line; a drag moves both anchors by the same
  18 whole hours, follows the pointer to 0.2 px and leaves the visible range unchanged; a drag beside
  the line still pans; Escape restores the line and reports nothing; the line is drawn on 4h, 15m and
  1d; an anchor 6.5 h after the last candle sits 6 bars after it and stays on whole hours when dragged;
  on the log scale a vertical drag keeps the ratio of the two prices. No console errors.

# Result of Step 2 (2026-11-17)

- Domain `Drawings/`: `Drawing`, `Anchor` (Unix seconds, price null only for a wave label),
  `DrawingStyle`, `WaveMark`, `PointLabel`, `FibonacciPreset`; `DrawingTools` (anchor counts,
  payload version 1, which tools have an area, default styles), `DrawingRules` (field -> problems,
  as the validators report them), `FibonacciPresets` (the two defaults with fixed ids, rules of
  levels), `WaveNotation` (the accepted table; the chooser's next label: 1-5, A-E, W-Y, each run
  starting again), `DrawingCodes` (camelCase codes as in the API).
- Limits chosen: a polyline has 2 to 200 points; a text at most 2000 characters; at most 30
  Fibonacci levels within ±100; anchor times 1970 to 2200; colours `#RRGGBB` or `#RRGGBBAA`.
- Contracts `Drawings/`: `DrawingRecord` (also the record of the expert file later: nothing of the
  workspace), `AnchorRecord` (ISO time), `DrawingStyleRecord`, `WaveMarkRecord`,
  `PointLabelRecord`, `FibonacciPresetRecord`. The mapping to the Domain comes with step 4.
- 83 new unit tests (328 in all); integration and architecture tests unchanged and passing.

# Result of Step 3 (2026-11-17)

- Script 0004: `drawings` (id from the client, asset, tool, payload version, JSON payload, `seq`
  for the order of making, cascade with the asset: AS5) and `fibonacci_presets` with the two
  defaults (fixed ids; a test keeps them equal to the Domain's).
- `DrawingRepository` (get by asset in order, get, idempotent save that answers `UnknownAsset` or
  `OtherAsset` instead of storing, delete), `FibonacciPresetRepository`, `ChartSettingsRepository`
  on table 0003 with a Domain `ChartSettings` (drawings hidden so far). Stored shapes are private
  records of Infrastructure, apart from the Domain and the API.
- A row this build cannot read (unknown tool, newer payload version, broken JSON) is skipped and
  left untouched, never deleted or overwritten by a guess.
- 13 new integration tests (177 in all): every tool read back exactly after a restart; last save
  wins and keeps the place; repeated save; delete and undo by the same id; removing an asset takes
  its drawings and chart settings only; unreadable rows; presets; Fibonacci levels kept when the
  preset changes; a version 3 workspace upgraded with a before-v4 backup; restore.

# Result of Step 4 (2026-11-17)

- Feature Drawings: GetDrawings, SaveDrawing, DeleteDrawing, GetFibonacciPresets,
  SaveFibonacciPreset, DeleteFibonacciPreset; `DrawingMapping` between the records (codes, ISO
  times) and the Domain; the validator applies `DrawingRules` and answers with the fields. Feature
  ChartSettings: get (defaults when never saved), save.
- Endpoints `/api/drawings/{source}/{symbol}`, `/api/drawings/{id}` (put, delete),
  `/api/fibonacci-presets` (list, put, delete), `/api/chart-settings/{source}/{symbol}`.
- Hub event `DrawingsChanged(source, symbol, window)`; the window comes from the header
  `X-TradeViewer-Window`.
- Decided while building (in the architecture): the lock is kept by the chart only, because the
  client coalesces changes ("unlock, then move" is one save); the last Fibonacci preset of a kind
  cannot be deleted or change its kind, so each Fibonacci tool always has levels.
- 18 handler tests and 8 endpoint tests (346 unit, 185 integration in all).

# Result of Step 5 (2026-11-17)

- `primitives/drawing.ts`: the drawing as the module holds it (the record with Unix seconds;
  unknown fields carried through) and the default styles of the Domain. `primitives/shapes/`: one
  class per tool (trend line, ray, arrow, horizontal and vertical line, rectangle with four corner
  handles, text note, parallel channel that keeps its distance when the line is reshaped).
- `tools/interaction.ts`: the state machine (place, select, move, reshape, Escape, lock, hidden);
  `tools/magnet.ts` (nearest open, high, low or close in pixels); the drawing layer is also the
  surface that converts pixels and anchors; `tools/pointerInput.ts` routes the mouse to it.
- `ChartPane`: `setDrawings`, `setTool`, `setDrawingOptions`, `selectDrawing`, `onDrawingEvents`.
- Changed against the proposal: the module reports no deletion (Web deletes and calls
  setDrawings) and reports `toolChanged`, so the tool rail of Web follows when the module returns
  to the pointer. In the architecture, chapter 10.
- 53 new Vitest tests (143 in all). Headless browser (`~/tools/browser/tools-check.mjs`): all
  eight tools placed by clicks without the chart panning, each reported once with the pointer back
  after it; select, move (whole hours, no pan), reshape by a handle (the other anchor unchanged),
  Escape while dragging and while placing, the free corner of a rectangle, a drag on empty chart
  pans and clears the selection, a locked line neither moves nor pans, hidden drawings are not hit,
  the magnet puts a line on an OHLC value of its candle. `drawing-check.mjs` of step 1 passes too.

# Result of Step 6 (2026-11-17)

- Web `Drawings/`: `DrawingSession` (list of the asset shown, selection, undo and redo per asset,
  lock rules), `DrawingSaveQueue` (background saves, newest change only, retry 1-2-5-10 s, "not
  saved", unconfirmed changes laid over a fresh read), `ChartDrawing` (the record with Unix
  seconds), `WindowIdentity` (header `X-TradeViewer-Window`); `Api/DrawingsApi`.
- `ChartHost`: setDrawings with the selection, setTool, setDrawingOptions, warnWhenLeaving and the
  events; the chart module got `blazorBridge.ts` (events to ChartHost, page keys, leave warning).
- Chart page: tool rail at the left, bar of the selected drawing, keys Delete, Ctrl+Z, Ctrl+Y.
- The magnet is a setting in `/api/settings` (`magnet`, on until switched off); DR2 says so now.
- Changed against the proposal "Leaving the page with a save waiting sends it with keep-alive":
  every save is sent with keep-alive (so one that started survives closing the window), and while
  a change is unconfirmed closing or reloading the window asks first. A request that waits for its
  retry cannot be sent after the page is gone, so asking is the safe way.
- Found in the browser and fixed: chart and page toggled the selection forever (a push resuming
  after an await sent an older selection back); ChartHost now sends a selection only when the
  newest wanted one differs from the chart's. Blazor writes a bool attribute as present or absent,
  so `aria-pressed` is written as text.
- UnitTests reference Web for its client state (architecture chapter 14): 20 new tests; one
  endpoint test for the magnet; 2 Vitest tests for the keys (366 unit, 186 integration, 145
  Vitest in all). Headless browser (`~/tools/browser/web-drawings-check.mjs`), all through Blazor:
  a line placed from the rail is saved; colour, width and style from the bar are saved; Ctrl+Z
  three times and Ctrl+Y on the server; a drag sends one save with the window id; with PUTs
  failing the rail says "not saved" and leaving asks, after the connection is back it is saved and
  both go away; a locked line resists Delete and the bar offers only unlock; Delete and Ctrl+Z
  bring it back with the same id; a text note gets its text; hidden and magnet survive a reload;
  another asset has its own drawings. No console errors.

# Result of Step 7 (2026-11-17)

- Chart module: `PolylineShape` (any number of points; double-click on the last point or Enter
  ends it, Escape drops it; line hidden; wave labels on its points) and `WaveLabelShape` (no price:
  drawn at the high or low of the candle that contains its time); `waveNotation.ts` and
  `waveLabelDrawing.ts` (double brackets drawn as a circle). `setTool` takes extras: the wave mark
  of the chooser. `DrawingSurface` got `pointOf` and `sideAt` (above or below the middle of the
  candle under the pointer).
- Web: `WaveLabels` (degrees, labels, next label, written form, labels for the points of a count),
  the chooser `WaveChooser`, the rail with polyline and wave label, the bar with degree, label and
  side of a wave label, and "line", "label points" and "no labels" for a polyline.
- Decided while building: the wave label tool stays in use after each label (the other tools
  return to the pointer), because the plan's chooser moves on to the next label after a click; a
  count is clicked label after label, Escape or the pointer ends it. "Label points" labels the
  points after the first (the start of the move) from the chosen label on: highs above, lows below.
- 28 new Vitest tests (173), 4 new unit tests (370; one keeps Web's table equal to the Domain's).
  Headless browser (`~/tools/browser/waves-check.mjs`): a polyline of five points ended by a
  double-click, its points labelled (1) to (4) above highs and below lows, its line hidden; a
  polyline ended by Enter; four primary labels placed one after another, numbered on, above or
  below by the click, without a price, the chooser at ((5)) afterwards; Escape puts the tool away;
  a label is selected by a click and put on the other side; on 4h all six drawings are there, the
  labels on their candles. No console errors.

# Result of Step 8 (2026-11-17)

- Chart module: `fibonacci.ts` (levels interpolated in pixels: linear in price on a linear scale,
  in log space on a logarithmic one) and `FibonacciShape` for both tools; `Look.priceText` gives
  the price of a level as the scale writes it.
- Web: the two tools on the rail; `FibonacciChooser` (the preset whose levels a new drawing
  copies, per kind, for the window) beside the rail while a Fibonacci tool is in use;
  `FibonacciPresetsEditor` on the settings page (edit name and levels, add, delete; the server
  keeps the last preset of a kind).
- 11 new Vitest tests (184 in all), among them one that checks the levels on a logarithmic scale
  against the price formula of log space. Headless browser (`~/tools/browser/fib-check.mjs`): a
  retracement takes the default levels; an extension takes three clicks, the extension levels and
  light blue; on the log scale the 0.5 level sits at the geometric mean (66946.50 between 72863.69
  and 61509.84, against 67186.76 on the linear scale); the settings page lists, edits (levels
  saved in order), refuses levels that are not numbers, keeps the last preset of a kind and adds
  one; drawings made before keep their levels; new drawings take the edited and the chosen
  preset. No console errors. The script puts the presets of the development workspace back.

# Result of Step 9 (2026-11-17)

- Chart module: `tools/exactTime.ts` (which extreme a price is, which candle made it, the smallest
  timeframe); `ChartPane` looks the time up in the smallest timeframe when a drawing is placed or an
  anchor reshaped on a high or low (wave labels always); the interaction reports a new or reshaped
  drawing after the lookup, once, and never while dragging. `setSeries` takes the timeframes with
  candles, which Web passes.
- Decided while building: not only anchors snapped by the magnet are looked up, but every placed or
  reshaped anchor whose price is the high or low of its candle (with the magnet off that is rare,
  and then right too); a drawing moved as a whole keeps the times it was moved to, since its
  prices are no longer the extremes they were.
- 8 new Vitest tests (192 in all). Headless browser (`~/tools/browser/exact-check.mjs`, BTCUSDT
  1d): a trend line from the high of 4 November to the low of 12 November got 15:15 and 23:15, the
  times of the 15m candles that made them, in two lookups; a wave label above the candle of
  8 November got 13:15, the 15m candle of that day's high; on 15m no lookup happens. No console
  errors.

# Result of Step 10 (2026-11-17)

- Other windows follow: Web's hub client hears `DrawingsChanged`; a chart page showing that asset
  reads its drawings again unless it made the change. Headless browser
  (`~/tools/browser/windows-check.mjs`, a 4h and a 1d window of BTCUSDT): a line drawn in the 4h
  window appears in the 1d window, its delete and the undo follow; the 4h window reads nothing
  again; a window showing ETHUSDT reads nothing.
- Found and fixed: since step 9 the chart selects a new drawing before it reports it (after the
  lookup of its exact times), so Web did not know the selected drawing and Delete did nothing;
  `DrawingSession` now selects a drawing when its creation arrives if the chart selected it before
  (two unit tests).
- NF3 (`~/tools/browser/nf3-measure.mjs`, 1h, a line dragged by 90 pointer moves 16 ms apart):
  with 200 drawings of all tools 187 frames, median and p95 16.7 ms, longest 16.8 ms, no long task;
  the same as with no other drawing. Measured in a headless Chromium on WSL.
- All checks of the epic run again and pass: tools-check, drawing-check, web-drawings-check,
  waves-check, fib-check, exact-check, windows-check. The older two were adjusted: tools-check
  starts from no saved drawings (since step 6 the page shows them), exact-check asks the pane for a
  point (`pointOf`) instead of setting a probe drawing, which Web, owning the list, replaced.
- Docker: the image builds (amd64, 23 s with cached layers); the container `tradeviewer` on WSL
  (localhost:8080, data /home/admin/tradeviewer, frozen market data) runs this branch, healthy; its
  workspace was upgraded to version 4 with the backup `workspace-20261117-153810-before-v4.db`.
- Architecture chapter 10: NF3 measured, other windows follow. README and runbooks need no change;
  the Pi's deployment log is written when the owner lets the epic go there.
- In all: 372 unit, 19 architecture, 186 integration and 192 Vitest tests pass; `dotnet format`
  clean.

# Changes after the Owner's Look (2026-11-17)

The owner asked, after looking at the epic in Docker:

1. Fibonacci as in TradingView: levels switched on and off, and the levels from the first to the
   last click instead of to the right edge.
2. Elliott wave patterns as in TradingView, under one group button: 1-2-3-4-5, A-B-C, W-X-Y,
   W-X-Y-X-Z, A-B-C-D-E; labels on by default, the line can be switched off; icons like
   TradingView's.

Done (DR4 and DR5 changed first, then architecture chapters 5 and 10):

- Domain `FibonacciLevel` (ratio, on or off) for presets and drawings; default presets with the
  levels of DR4 on and further ones off (retracement 1.272, 1.414, 1.618, 2, 2.618; extension 0,
  0.382, 0.5, 0.618, 0.786, 4.236, chosen after TradingView's list); Contracts
  `FibonacciLevelRecord`. Stored as `{ratio, visible}`; plain numbers, stored before, are read as
  levels that are on (`StoredLevels`); script 0005 gives the default presets their levels that are
  off, only where the user has not changed them. Payload version stays 1: the epic is not merged
  and the reader takes both shapes.
- Chart module: only levels that are on are drawn and hit, from the leftmost to the rightmost anchor.
  Web: a "levels" panel in the bar of a Fibonacci drawing; the presets on the settings page as
  levels with a check box each, a field to add one (Enter) and a button to remove one.
- Z added to the labels (Domain, module, Web; Next: Y to Z, Z to W). `primitives/wavePatterns.ts`:
  labels for the points of a pattern and their sides; the interaction places a pattern as a
  polyline with `pattern` and `degree` extras, ends it at its last point (Enter and double-click
  do not end it early) and gives reshaped polylines' labels their sides again. Web: `WavePatterns`
  (five patterns with icons), a group button with a flyout on the rail showing the pattern used
  last, the chooser in degree-only mode, and the degree of a whole count in the bar.
- Found while checking: Enter in the "add a level" field did nothing, because the field was bound
  on change (which comes after the key); it is bound on input now.
- Tests: 375 unit (Z, presets on and off), 188 integration (old plain-number levels read; the
  upgrade leaves an edited preset alone), 204 Vitest (levels on and off, the span, patterns, sides
  after a reshape, Z). Headless browser: `fib-check.mjs` rewritten for levels on and off,
  `elliott-check.mjs` new (the flyout, all five patterns, primary degree, line off, degree of a
  count, sides after a reshape, Enter does not end a pattern); all eight checks pass.

---

# Proposals

They become decisions when the owner accepts them, and go into the architecture with the step that
needs them.

**The record**

- A drawing: `id` (UUID from the client), asset, `tool`, `locked`, `anchors` (time UTC + price),
  `style` (colour, width 1 to 4, line style solid/dashed/dotted, fill for rectangle and channel),
  `text`, and what only one tool has (levels of a Fibonacci drawing, labels and "line hidden" of a
  polyline, degree, label and side of a wave label). Stored as JSON with tool and payload version
  (chapter 11); the server checks it (known tool, right number of anchors, finite numbers) and
  refuses what it cannot read rather than store it.
- **A Fibonacci drawing keeps its own levels**, copied from the preset chosen when it is drawn.
  Editing a preset changes the drawings made afterwards, never the existing ones: a change on the
  settings page must not silently alter analysis already done.
- **A wave label keeps time and side (above or below), no price**: it is drawn at the high or low
  of the candle that contains its time, in the timeframe shown. That is "follows the candle" (DR5).

**The tools**

```text
Trend line, arrow      2 anchors
Ray                    2 anchors, continues beyond the second
Horizontal line        1 anchor (price); across the whole chart, price shown at the scale
Vertical line          1 anchor (time)
Rectangle              2 anchors (opposite corners), pale fill
Parallel channel       3 anchors: the line, then the distance of the parallel
Text note              1 anchor
Polyline               any number of points; double-click or Enter ends it, Esc drops it
Fibonacci retracement  2 anchors; level 0 at the second, 1 at the first (as TradingView)
Fibonacci extension    3 anchors (start, end, retracement); the levels are the move start-to-end
                       laid from the third point (TradingView's trend-based extension)
Wave label             1 anchor; after a click the chooser moves to the next label (1, 2, 3 ...)
```

- Placing is click, move, click, with a preview that follows the pointer. After a drawing is
  finished the pointer is the selection tool again, as in TradingView's default.
- Fibonacci levels are drawn between the anchors and continue to the right edge; each shows ratio
  and price at its left end.
- Default colours from the palette already in the specification (OV6): lines blue `#2962FF`,
  Fibonacci extension light blue `#64B5F6` (DR4), retracement gray `#787B86`, wave labels blue.

**Wave labels (DR5), for the owner to check**

```text
Degree             motive            corrective      also
Grand Supercycle   ((I)) ... ((V))   ((a)) ((b)) ((c))   W X Y and A-E in the form of the degree
Supercycle         (I) ... (V)       (a) (b) (c)
Cycle              I ... V           a b c
Primary            ((1)) ... ((5))   ((A)) ((B)) ((C))
Intermediate       (1) ... (5)       (A) (B) (C)
Minor              1 ... 5           A B C
Minute             ((i)) ... ((v))   ((a)) ((b)) ((c))
Minuette           (i) ... (v)       (a) (b) (c)
Subminuette        i ... v           a b c
```

Double brackets stand for the circle of the textbook notation and are drawn as a circle around the
text. The record stores degree and label, not the drawn text.

**In the chart module**

- One series primitive, the drawing layer, holds all drawings of the chart; each tool is one class
  in `primitives/` that knows its geometry, its drawing, its hit test and its handles. One layer
  because the order of drawing and the question "what is under the pointer" need one place. The
  architecture says "one series primitive per drawing type": it will say "one class per drawing
  type on one layer".
- Pointer events are taken on the chart's element before the library gets them. With a tool chosen,
  or on a drawing, the chart does not pan; elsewhere everything stays as it is (NF2).
- While dragging, only the module works: no call to Blazor, no request. On release it reports the
  result once (NF3).
- The contract as built: `setDrawings`, `setTool` (with the tool's options), `setOptions` (magnet,
  drawings hidden), and the events `drawingCreated`, `drawingChanged`, `drawingDeleted`,
  `selectionChanged`. `contextMenuRequested` is not built: the bar of the selected drawing does
  what a context menu would.

**Saving (priority 1)**

- Web shows a change at once and sends a `PUT /api/drawings/{id}` in the background; several
  changes of one drawing in a row send only the last. A failed save is tried again and the page
  shows "not saved" as long as one is waiting, so a lost connection is visible and not silent.
- Leaving the page with a save waiting sends it with the browser's keep-alive request.
- `DELETE /api/drawings/{id}`; undo of a delete is a `PUT` of the same record with the same id.
- Undo and redo: Ctrl+Z, Ctrl+Y and two buttons; they cover create, change and delete of the asset
  shown, until the window is reloaded. Client state in Web, in C# with unit tests (chapter 14).
- **Other windows follow**: a new hub event `DrawingsChanged(source, symbol)`; a window showing
  that asset reads its drawings again. Reason: several windows are how TradeViewer shows several
  timeframes of one asset (CP1), so a line drawn on 4h should appear in the 1d window beside it.
  The window that made the change ignores its own event.

**Settings**

- Drawings hidden (DR6) is saved per asset in the chart settings (CP2).
- The magnet is one setting for all charts, in `/api/settings` next to the price scale mode: it is
  a way of working, not a property of an asset.

**Where a snapped anchor sits (step 9, the owner decides)**

The magnet puts an anchor on the high of a daily candle. Stored is the day, 00:00. On the 15m chart
that anchor is then drawn on the 00:00 candle, with the price of a high that was made at, say,
14:30: the point hangs beside the candle that made the high. TradingView does the same. For wave
counts that are refined on lower timeframes this is the main way drawings would *not* behave the
same on every timeframe.

Proposal: when the magnet snaps to a high or low, the module looks up, in the smallest timeframe
that has candles there, which candle made that price, and stores its time (14:30). On the daily
chart nothing changes, because 14:30 lies in that day's candle; on every lower timeframe the point
sits on the candle that made the extreme. Cost: one small request for candles when a drawing is
placed or an anchor is moved; never while dragging.

---

# Questions for the Owner

1. **Step 9**: should a snapped anchor get the exact time from the smallest timeframe (my
   recommendation), or stay at the open time of the candle it was set on, as in TradingView?
2. **Wave labels**: is the table of the nine degrees the notation you use, with double brackets
   drawn as circles?
3. **Fibonacci presets**: existing drawings keep their levels when a preset is edited (proposal).
   Agreed, or should they follow the preset?
4. Any objection to the other proposals: tool returns to the pointer after one drawing, the bar
   instead of a context menu, other windows following, the magnet as one setting for all charts?

---

# Answers of the Owner (2026-11-16)

The owner agreed to the plan as a whole:

1. Step 9 is built: a snapped anchor gets the exact time from the smallest timeframe.
2. The table of the wave labels is accepted.
3. A Fibonacci drawing keeps its levels when a preset is edited.
4. No objection to the other proposals.
