# TradeViewer - Specification

Owner: Vitali Willms

This document says **what** TradeViewer does. How it is built is in docs/architecture.md; why a
choice was made is in docs/decisions/.

---

## 1. Purpose

TradeViewer is the one website of a three-application setup for discretionary market analysis:

```text
Downloader    downloads and updates market data, writes it as CSV files     ../Downloader
EWTS          analyses the data (Elliott Wave, setups, backtests, alerts)   ../EWTS
TradeViewer   shows charts and operates the other two                       this repository
```

TradeViewer shows charts with the user's own drawings and with the analysis of EWTS, and it is
the user interface for operating the Downloader and the EWTS instances. It also shows the
scenarios EWTS is developed and tested with, and it is the editor in which the owner makes the
expert scenarios. It contains no analysis of its own and places no orders.

The Downloader and EWTS are specified in their own repositories. This document only states what
TradeViewer needs from them; the interfaces are in docs/knowledge/downloader-interface.md and
docs/knowledge/ewts-interface.md.

## 2. Terms

```text
Source          a data provider of the Downloader, e.g. binance
Asset           a symbol of a source, e.g. binance / BTCUSDT; the source is part of its identity
Timeframe       candle size: 15m, 1h, 4h, 1d, 1w (later 1m, 5m, 12h, 1M)
Candle store    TradeViewer's local copy of the candles of its assets
Workspace       everything the user creates in TradeViewer (watchlists, drawings, chart settings)
EWTS instance   one running EWTS; it handles the assets of the sources it reports (today one)
Observed asset  an asset an EWTS instance analyses continuously
Overlay         analysis drawings of EWTS shown on a chart; read-only in TradeViewer
Test run        a backtest (later also a Walk Forward run) an EWTS instance runs on its own;
                it reports statistics and writes a scenario for every setup it handled
Alert           a message of an EWTS instance about a setup or a trade plan event
Scenario        one market situation in EWTS's scenario format: the waves of a 1-2, optionally
                the trade; the owner's reading (expert scenario, made in TradeViewer's
                editor) or the engine's reading (test-run scenario, written by EWTS)
```

## 3. Users and Environment

- One user, the owner. No accounts, no roles, no login.
- Runs in the owner's home network on a Raspberry Pi 5 or a Linux machine, without internet
  access. Used from a desktop browser (NF11).
- The Downloader and the EWTS instances run in the same network.

---

## 4. Functional Requirements

Every requirement has an id. The backlog refers to these ids.

### 4.1 Watchlists and Assets

- **AS1** There are five fixed watchlists, one per asset class: Crypto, Stocks, ETFs, Forex and
  Commodities. An asset is placed in the watchlist of its asset class, whatever source it
  comes from; each row names the source. The user reorders the entries of a watchlist.
  Watchlists cannot be created, renamed or deleted. Assets exist in TradeViewer only through
  the watchlists. (The Downloader has no asset class for commodities yet; that watchlist stays
  empty until it has.)
- **AS2** The user adds an asset to a watchlist by choosing the source and picking the symbol
  from the symbols of that source, found by typing: TradeViewer searches the Downloader's symbol
  catalog while the user types and says when more symbols match than are shown. The symbol
  picked is looked up at the Downloader first, which shows its tick size, session and earliest
  dates and confirms it; if the Downloader does not know it, the asset is rejected with the
  Downloader's message. Only sources TradeViewer can chart are offered: binance, until stocks,
  ETFs and forex can be charted. Without the Downloader, the symbols come from a fixed list per
  source that is part of TradeViewer (binance, with the symbols of the Downloader's frozen copy).
  An asset keeps its ticker at the source, which can differ from its symbol.
- **AS3** Adding an asset subscribes it at the Downloader with all timeframes of the source
  and the default history, which the user sets once in the settings (initially: 1d and 1w
  complete, 4h two years, 1h one year, 15m 90 days). The source may provide less. The user can
  change the history of a single asset later.
- **AS4** An asset whose history is still being downloaded is marked as loading, with the
  progress per timeframe. A timeframe can be charted as soon as its newest candles are there.
- **AS5** Removing an asset from its watchlist removes it from TradeViewer: its analysis is
  switched off at EWTS if it was on (EW3), its subscription at the Downloader ends, and its
  candles, drawings and chart settings are deleted. TradeViewer asks for confirmation first and says
  what will be deleted.
- **AS6** A watchlist row shows the last close with its time, the change against the previous
  daily close in percent (the day in UTC for crypto), a warning mark when a file of the asset
  was not imported (MD6), and a mark when EWTS analyses it. Gaps are not shown in the row but in
  the chart (MD4).
- **AS7** The assets of the workspace are the truth. At every start and after a restore of a
  backup (WS3) TradeViewer brings the rest in line with them: at the Downloader it subscribes
  the assets that have no subscription of TradeViewer and ends its subscriptions of symbols that
  are no asset any more; in the candle store it deletes the candles of such symbols and imports
  what is missing. After a restore every subscription is sent again, because the history of an
  asset may have changed. Subscriptions of other clients are never touched. A subscription
  names the asset by its symbol and carries its ticker at the source.

### 4.2 Market Data

- **MD1** Charts show the candles the Downloader delivered, unchanged. TradeViewer never invents,
  fills or repairs candles. The Downloader delivers closed candles only; the one exception is
  the current candle (MD8).
- **MD2** New and changed candles appear on open charts without a reload. TradeViewer checks
  for new data when the Downloader is expected to have written it (after each closed candle of
  the asset's smallest timeframe) and repeats the check in short intervals until the data is
  there. There is no live price: a closed candle shows after the Downloader has written it, up
  to about one update cycle later (15 minutes).
- **MD3** A chart offers the timeframes the Downloader holds for the asset. For an asset that
  trades around the clock or through the week (crypto, Forex), a timeframe the source does not
  provide is built from the largest fitting one, aligned to its trading week. An asset with
  exchange hours (stocks, ETFs) has no 4h: it is not built and its button is disabled (decision
  0008). When a timeframe can be neither read nor built, the chart shows an error instead of
  candles.
- **MD4** Gaps the Downloader reports as `missingUpstream` are visible as a marker on the
  chart and in the asset's status. The marker is a small icon at the bottom of the chart, where
  the gap is; pointing at it shows from when to when candles are missing and why. Candles still
  being downloaded show as loading (AS4), not as a gap. Periods before the source's history
  limit and closed market hours are no gaps.
- **MD5** The candle store can be deleted and rebuilt from the Downloader's files at any time
  without losing anything the user created. Rebuilding an asset is the same procedure as
  adding it: the asset shows as loading until its candles are back.
- **MD6** A file that breaks the format rules is not imported. The chart works without its
  candles and the asset shows a warning with the file name and the line.
- **MD7** All times are UTC: axes, crosshair, lists and drawings.
- **MD8** On timeframes above the smallest one, the chart also shows the current candle, built
  from the closed candles of the smallest timeframe since the last closed candle (open of the
  first, high and low over all, close of the last, volume summed). It is marked as in progress,
  is replaced by the Downloader's closed candle as soon as that exists, and is never stored or
  used by EWTS. It can be incomplete when candles of the period are missing.

### 4.3 Chart

- **CH1** Candlestick chart with a time scale, a price scale (linear or logarithmic), crosshair
  with values of the candle below it, zoom and pan. Candles are the only chart type. The price
  scale sits on the right, fits the visible candles automatically (reset after a manual
  change) and has the precision of the asset's tick size.
- **CH2** Volume as a histogram below the candles.
- **CH3** Changing the timeframe of a chart keeps the number of visible candles and the
  anchor: at the latest candles the chart stays there, scrolled back it keeps the same time in
  view. Changing the asset keeps the number of visible candles and shows the latest ones. A
  newly opened chart shows the latest candles with some empty space to the right.
- **CH4** Scrolling back loads older candles without a visible interruption, down to the start of
  the stored history.
- **CH5** A selector shows the timeframes of the asset (MD3). A line and label at the price
  scale show the last close, coloured by the direction of the last candle. When the chart is
  scrolled away from the latest candle, a button jumps back to it.

### 4.4 Indicators

- **IN1** Indicators, calculated by TradeViewer: SMA, EMA and Bollinger Bands on the price;
  RSI, MACD and ATR in their own panes below. The user sets their parameters, the price they
  are calculated from (open, high, low, close, hl2, hlc3, ohlc4; ATR has none) and their colours
  per asset; an asset can have several indicators of one kind. An indicator is added once and shows on every timeframe of the asset, always
  calculated from the candles of the timeframe shown (an SMA on 1d is never the SMA of 15m).
  A new indicator starts with the defaults of TradingView:

  | Indicator       | Parameters                                  |
  |-----------------|---------------------------------------------|
  | SMA, EMA        | length 9, source close                      |
  | Bollinger Bands | length 20, standard deviation 2, source close |
  | RSI             | length 14, source close                     |
  | MACD            | fast 12, slow 26, signal 9, source close    |
  | ATR             | length 14                                   |

  Colours follow TradingView's defaults too: blue lines (SMA, EMA, MACD line, Bollinger basis),
  red upper and green lower Bollinger band, orange MACD signal, purple RSI, green and red
  histogram for MACD, red ATR. Each indicator shows a legend line in its pane with its values
  under the crosshair and the actions hide, settings and remove; adding indicators and the
  templates are in a bar above the chart, as in TradingView.
- **IN2** Indicator templates: a named set of indicators the user saves and applies to a chart.

### 4.5 Chart Page

- **CP1** The chart page shows one chart. Its asset and timeframe are part of the address, so
  a window can be reloaded, bookmarked and duplicated. Several browser windows show several
  charts independently. Selecting an asset in a watchlist shows it in the current window, in the
  same timeframe when the asset has it, otherwise in the next larger one it has, and without one
  in the largest smaller one (decision 0008).
- **CP2** The settings of a chart (indicators, drawings hidden, selected analysis instance, hidden
  analysis groups) are saved per asset and restored when the asset is opened, in any window. The
  price scale mode (linear or logarithmic) is one setting for all charts: it stays when the asset
  or the timeframe changes, as in TradingView. Changes are saved automatically.
- **CP3** Opening TradeViewer without an address opens the chart last used.

### 4.6 Drawings

- **DR1** Tools: trend line, ray, horizontal line, vertical line, parallel channel, rectangle,
  arrow, text note, polyline (connected points, for wave counts), Fibonacci retracement,
  Fibonacci extension, wave label.
- **DR2** A drawing can be selected, moved, reshaped by its handles, restyled (colour, width,
  line style, text) and deleted. Undo and redo cover the actions of the current session. A
  drawing can be locked; a locked drawing cannot be moved, changed or deleted until it is
  unlocked. A magnet option (on by default) snaps anchors to the open, high, low or close of
  the nearest candle; it is one setting for all charts and is kept, like the price scale mode.
- **DR3** A drawing belongs to an asset, not to a timeframe: it stays on the chart
  when the timeframe is switched. Anchors may lie to the right of the last candle.
- **DR4** Fibonacci tools take their levels from a preset the user can edit and add to. Each level
  of a preset and of a drawing can be switched on and off, as in TradingView. Levels switched on
  by default: retracement 0, 0.236, 0.382, 0.5, 0.618, 0.786, 1; extension 1, 1.272, 1.618, 2,
  2.618, 3.618. The default presets also hold levels that are switched off (retracement 1.272,
  1.414, 1.618, 2, 2.618; extension 0, 0.382, 0.5, 0.618, 0.786, 4.236). A drawing has one colour
  for all its levels (default for extensions: light blue); each level shows its ratio and price
  and spans from the first to the last anchor of the drawing. On a logarithmic price scale the
  levels are calculated in log space.
- **DR5** Wave labels are text markers from a fixed list: the nine Elliott degrees (Grand
  Supercycle to Subminuette) with their numbers and letters (1-5, A-E, W, X, Y, Z). A label
  sits above a high or below a low and follows the candle. Labels can sit on the points of a
  polyline; the polyline can hide its line and show only its labels. Wave patterns, grouped
  under one button of the tool rail as in TradingView, place such a polyline with its labels in
  one go: impulse 1-2-3-4-5, correction A-B-C, triangle A-B-C-D-E, double combination W-X-Y and
  triple combination W-X-Y-X-Z, after a first click on the start of the move. Their labels are
  on, their line can be switched off. The single wave label and the polyline sit at the bottom of
  the same group, for counts that fit no pattern, such as one still in progress. TradeViewer does
  not check labels against any Elliott Wave rule.
- **DR6** All drawings of a chart can be hidden and shown at once.

### 4.7 Downloader Monitoring

- **DL1** A status page shows the Downloader's state: reachable or not, version, last and next
  update cycle with result, queue sizes, and the state of every source. A status dot in the
  header shows it at a glance: green, yellow (a source is rate limited or the last cycle was
  partial), red (not reachable). The page only shows data; it changes nothing at the Downloader.
- **DL2** Per asset: coverage per timeframe (wanted from, covered from and to), pending jobs,
  gaps with reason, last change, last error.
- **DL3** The page lists all subscriptions of the Downloader and marks those of other clients
  as read-only.
- **DL4** When the Downloader is not reachable, charts keep working with the stored candles and
  the status page says since when it is unreachable.

### 4.8 EWTS Instances

- **EW1** The user registers an EWTS instance by name and address, edits and removes it.
  Several instances can be registered.
- **EW2** The instance list shows for each: reachable or not, version, sources, number of
  observed assets, time of the last analysis.
- **EW3** Assets are independent of EWTS: every asset of a watchlist can be charted and drawn
  on without it. An instance handles the sources it reports (today one), so only the instances that handle
  the asset's source are offered, and EWTS refuses a request for another source. The user
  switches the analysis of an asset on by choosing the instance, the context timeframe (1d or 4h,
  which fixes the timeframes of the analysis; 1d only for an asset with exchange hours, decision
  0008), the history start (pre-filled with the default
  of AS3, can be changed) and the parameter profile. The instance then analyses the asset on
  every new candle until the user switches the analysis off. Several instances can analyse the
  same asset; the watchlist row shows a mark for each. Switching analysis on or off is refused
  with a message while the instance is not reachable.
- **EW4** Overview per instance: one table of its observed assets with context timeframe, time of
  the last analysis, data quality, the state of a watched setup or open plan, and the direction
  and state of the best setup (its grade too, when EWTS provides one).
- **EW5** Asset details, as the EWTS record offers them (docs/knowledge/ewts-interface.md): the current count in short form, the setups with state, entry,
  add level, stop and target, the time they were found, and the history of earlier setups
  (grade and score too, when EWTS provides them).
- **EW6** When an instance is not reachable, everything else keeps working; its data is shown
  as last received, with the time, and marked as stale.

### 4.9 Analysis Overlays

- **OV1** A chart shows the overlays of one EWTS instance at a time for its asset. When several
  instances analyse the asset, a selector on the chart switches between them or turns the
  overlays off; the choice is saved per asset (CP2). The overlays are: wave labels and wave
  lines, price levels (invalidation, entry, add, stop, targets), zones, Fibonacci levels and
  markers.
- **OV2** Overlays are grouped (e.g. preferred count, alternate count, setup, Fibonacci,
  structure). The user shows and hides each group per chart.
- **OV3** Overlays are read-only and cannot be edited or copied into drawings.
- **OV4** Overlays update on open charts when the instance has analysed the asset again.
- **OV5** A corner of the chart shows the selected instance (the selector), the time of its
  last calculation and whether the analysis is running. The time is refreshed with every update
  (OV4); when the instance is not reachable it is marked stale (EW6).

- **OV6** TradeViewer colours every shape by its role, in the overlays and in scenarios (SC2),
  with the palette of TradingView; EWTS sends no colours. Support is yellow, resistance red
  and the targets light blue, as the owner draws them.

  | Role                          | Colour               |
  |-------------------------------|----------------------|
  | neutral                       | gray `#787B86`       |
  | context                       | light gray `#B2B5BE` |
  | bullish                       | green `#089981`      |
  | bearish                       | red `#F23645`        |
  | wave                          | blue `#2962FF`       |
  | lowerDegree                   | purple `#9C27B0`     |
  | invalidation                  | pink `#E91E63`       |
  | entry                         | cyan `#00BCD4`       |
  | add                           | lime `#8BC34A`       |
  | stop                          | red `#F23645`        |
  | target                        | light blue `#64B5F6` |
  | exit                          | orange `#FF9800`     |
  | support                       | yellow `#FDD835`     |
  | resistance                    | red `#F23645`        |

  A role not in the table is drawn gray.

### 4.10 Test Runs

- **BT1** The user starts a backtest at an instance: an asset of a source the instance handles,
  timeframe (1d only for an asset with exchange hours, decision 0008), period, account size, risk per trade and the overrides the instance offers (shown
  as generic fields). The asset need not be in a watchlist: EWTS subscribes to the Downloader
  itself and waits until the data is there.
- **BT2** An instance runs one test at a time; a second start is refused with EWTS's message.
  A running test shows the state EWTS reports (e.g. downloading, checking, running, done, data
  error), the elapsed time, its progress and its statistics so far, updated as it runs, and
  can be cancelled. When the connection to the instance is lost, a connection error is shown
  and the data is marked stale (EW6).
- **BT3** A table lists all runs with state, asset, timeframe, period and their main figures.
  Selecting a row shows the details: funnel, statistics, equity curve, the list of trades and
  watches, and the table of the run's scenarios.
- **BT4** For every setup a run watched or traded, EWTS writes a scenario. The scenario table
  of a run shows its figures (e.g. result in R, exit reason, duration) and
  can be sorted and filtered by them. Scenarios appear while the run is still going.
- **BT5** Selecting a scenario of a run opens the test-run viewer (4.12) on the run: the chart
  with the scenario drawn, and at its right the list of the run's scenarios to select from.
- **BT6** A button on a run creates a ZIP file with all scenario files of the run.
- **BT7** Deleting a run, after confirmation, deletes its scenarios.

### 4.11 Alerts

- **AL1** EWTS sends alerts itself (e.g. by Telegram). TradeViewer sends nothing.
- **AL2** TradeViewer shows one alert history across all instances: time, instance, asset,
  kind, text; it can be filtered by instance, asset and kind. An alert links to the chart of
  its asset with the overlays of the instance that sent it.
- **AL3** A new alert appears in the open TradeViewer as a notice that stays until it is
  closed, and a counter in the header shows the unread alerts. Alerts are fetched by polling,
  so a notice can come up to 30 seconds late. There is no browser notification.
- **AL4** The user edits the alert settings of an instance in TradeViewer: channels, and which
  kinds of alerts are sent. Secrets such as a bot token can be entered but are never shown;
  the form only says whether one is set.

### 4.12 Scenarios

Replaces writing an expert scenario by hand (a YAML file) and checking a scenario of a test run (a
Pine script loaded in TradingView). Decisions 0010 and 0011.

**Both pages**

- **SC1** There are two pages: the scenario editor for the expert scenarios (`expert/` of the
  scenario folder, written by TradeViewer) and the test-run viewer for the scenarios of a test run
  (`test-runs/<run id>/`, written by EWTS).
- **SC2** Opening a scenario shows a chart of its asset on its context timeframe, positioned on
  the time span of the scenario, with the scenario drawn in groups. The user can switch the
  timeframe; the drawing stays. The user shows and hides each group; the choice stays while
  stepping through scenarios.
- **SC3** The chart of a scenario uses the logarithmic price scale by default, because the
  levels of a scenario are measured in log scale. The choice is not saved in the chart
  settings of the asset (CP2).
- **SC4** The page shows the chart in the middle, the list of scenarios at its right
  (collapsible) and a panel below the chart. Selecting an entry of the list loads and draws that
  scenario; "previous" and "next" step through the filtered list. Pointing at a point, label or
  level shows its price, time and reason.
- **SC5** New or changed scenario files appear without a restart: the list checks the folder
  every 30 seconds and has a reload button.
- **SC6** When the asset of a scenario is not in TradeViewer, the page says so and offers to add
  the asset (only after the user agrees). When the candles of the scenario's time span are not
  stored, TradeViewer loads the history it needs, whatever the default history of AS3 is.

**Scenario editor**

Used while EWTS is developed: the owner makes and adjusts the expert scenarios here; they are
copied by hand to the development machine, where EWTS's code is adjusted against them.

- **SC7** The list holds every expert scenario: id, asset, context timeframe, verdict and draft
  mark. It can be filtered by asset, verdict and draft mark and sorted by any column.
- **SC8** An expert scenario is drawn by TradeViewer from its named points, in the groups waves
  (with their labels), wave 2 (its internals) and trade (entry, stop, add, stop moves, exits).
  Every scenario opens ready to edit; there is no separate view mode.
- **SC9** The user creates an expert scenario: asset, context timeframe (1d, or 4h for an asset
  without exchange hours, decision 0008) and time span. Every expert scenario is long.
- **SC10** The editor has a tool per named point, each snapping by the rules of EWTS's format:
  a wave point (wave 1 start, wave 1 end, wave 2 end; A, B and C end of wave 2) to the open time
  of its candle and the candle's wick extreme (the low for a low, the high for a high); entry and
  add to the open time and the close of the candle; an exit to the open time of the candle at the
  price the user gives, with its share and kind (target or stop); the stop to a price, a stop move
  to a price and the candle that caused it. Points are moved and removed like drawings (4.6).
- **SC11** Each section (waves, wave 2, trade) has its own timeframe, chosen in the editor; a point
  snaps to the candles of its section's timeframe whatever timeframe the chart shows.
- **SC12** In the panel the user sets the head of the scenario: verdict (trade-win, trade-loss,
  no-trade), draft mark, reason, notes, the time span (from, to), the pattern of wave 2, and a
  reason for every point (optional).
- **SC13** The scenario is saved automatically, and only once the user has drawn, changed or
  written something: viewing writes nothing. It is the file `expert/<id>.json` in EWTS's format.
  Reopening it rebuilds the drawing from the named points. Fields TradeViewer does not know
  (e.g. `legacy` of converted scenarios) are kept unchanged.
- **SC14** The id is built by EWTS's rule (`<symbol>-<context tf>-<yyyy-MM>-X<n>`, the month of
  wave 2 end, the next free number) when the scenario is first saved, so a new scenario is saved
  from the moment its wave 2 end is placed; until then the page warns before it is left. The id
  does not change afterwards.
- **SC15** The editor checks the form and shows each problem at its field or point: the order of
  the times, the fields the verdict and the pattern require, exit shares that do not add up to
  100 %, a point outside the time span. A problem never prevents saving. Whether the reading
  follows the rules of the strategy is checked by EWTS, not by TradeViewer.
- **SC16** The user deletes an expert scenario after confirmation.

**Test-run viewer**

Replaces the `.pine` file of a test-run scenario; used mostly while EWTS runs on the Pi.

- **SC17** The viewer lists the run folders of the scenario folder (run id, asset, number of
  scenarios, newest file). Choosing a run lists its scenarios: id, asset, context timeframe,
  direction, verdict and the figures EWTS writes as columns; the list can be filtered by verdict
  and sorted by any column. Opened from a test run (BT5), it shows that run.
- **SC18** A test-run scenario is drawn from the shapes of its drawing file, in the groups of the
  file. The panel shows its verdict, reason and figures (what the engine measured and how the
  trade ended).
- **SC19** The viewer is read-only: TradeViewer never writes, changes or deletes anything in the
  test-run folders.

### 4.13 Workspace

- **WS1** Every change the user makes is saved immediately. There is no save button. With several
  windows open, the last save of a record wins.
- **WS2** The workspace survives restarts, updates, and deleting or rebuilding the candle store.
- **WS3** The workspace is backed up once a day; the last 14 backups are kept. The user can make
  a backup at any time, download a backup and restore one.
- **WS4** An update of TradeViewer upgrades the workspace without losing data.
- **WS5** A settings page holds what is not part of a chart: the default history (AS3), the
  EWTS instances (EW1) and the backups (WS3).

---

## 5. Non-Functional Requirements

Measured on a Raspberry Pi 5 with an SSD, a desktop browser in the same network, an asset with
10 years of 15m candles.

- **NF1** Changing asset or timeframe: the chart is drawn within 150 ms.
- **NF2** Zoom, pan and crosshair run at 60 frames per second and need no request to the server.
- **NF3** Moving a drawing follows the pointer without visible delay; saving it never blocks
  the chart.
- **NF4** 100 assets with 10 years of history in five timeframes can be stored and charted.
- **NF5** TradeViewer starts and shows charts when the Downloader and all EWTS instances are
  down.
- **NF6** No request leaves the home network: all scripts, fonts and libraries are part of the
  application.
- **NF7** One container image for linux/arm64 and linux/amd64.
- **NF8** No authentication. TradeViewer is intended for a trusted home network only.
- **NF9** TradeViewer runs within 512 MB of memory.
- **NF10** After a start, the first chart can be opened within 15 seconds, even with 100
  assets; the rescan at startup runs in the background.
- **NF11** Supported are the current versions of Chrome, Edge and Firefox on a desktop.

---

## 6. Out of Scope

- Analysis of any kind: wave counting, rule validation, setup detection, scoring (EWTS)
- Downloading market data (Downloader)
- Sending alerts (EWTS)
- Writing test-run scenarios, checking a scenario against the rules of the strategy and comparing
  an expert scenario with a test-run scenario (EWTS)
- The journal and the trades of EWTS (possible later)
- Order execution and broker connections
- Replay and paper trading (decision 0005)
- Several charts in one window (grid layouts, linked crosshairs); use several browser windows
- Several users, shared workspaces
- Authentication (NF8)
- A mobile application

---

## 7. Dependencies on EWTS

Sections 4.8 to 4.11 need interfaces EWTS does not have yet. They are listed in
docs/knowledge/ewts-interface.md, chapter "Required Interface". In section 4.12 the scenario
editor needs only EWTS's file format; the test-run viewer needs the drawing files EWTS writes for
its test runs. Sections 4.1 to
4.7 and 4.13 need only the Downloader, whose interface exists.
