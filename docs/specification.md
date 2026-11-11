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
scenarios EWTS is developed and tested with. It contains no analysis of its own and places no
orders.

The Downloader and EWTS are specified in their own repositories. This document only states what
TradeViewer needs from them; the interfaces are in docs/knowledge/downloader-api.md and
docs/knowledge/ewts-api.md.

## 2. Terms

```text
Source          a data provider of the Downloader, e.g. binance
Asset           a symbol of a source, e.g. binance / BTCUSDT; the source is part of its identity
Timeframe       candle size: 15m, 1h, 4h, 1d, 1w (later 1m, 5m, 12h, 1M)
Candle store    TradeViewer's local copy of the candles of its assets
Workspace       everything the user creates in TradeViewer (watchlists, layouts, drawings, settings)
EWTS instance   one running EWTS; it handles the assets of exactly one source
Observed asset  an asset an EWTS instance analyses continuously
Overlay         analysis drawings of EWTS shown on a chart; read-only in TradeViewer
Test run        a backtest (later also a Walk Forward run) executed by an EWTS instance
Alert           a message of an EWTS instance about a setup or a trade plan event
Scenario        one market situation written down in EWTS's scenario format: the waves of a
                1-2, optionally the trade; either the owner's reading (expert scenario) or
                the engine's reading of one of its trades (analysis case)
```

## 3. Users and Environment

- One user, the owner. No accounts, no roles, no login.
- Runs in the owner's home network on a Raspberry Pi 5 or a Linux machine, without internet
  access. Used from a desktop browser; a tablet works for viewing.
- The Downloader and the EWTS instances run in the same network.

---

## 4. Functional Requirements

Every requirement has an id. The backlog refers to these ids.

### 4.1 Assets and Watchlists

- **AS1** The user adds an asset by source and symbol. TradeViewer looks it up at the Downloader
  first and shows its name, asset class and available timeframes; an unknown symbol is rejected
  with the Downloader's message.
- **AS2** Adding an asset subscribes it at the Downloader with the timeframes and the history
  start the user chooses (defaults: all timeframes of the source, the Downloader's default
  history). The user can change both later.
- **AS3** Removing an asset ends TradeViewer's subscription and removes its candles from the
  candle store. The user's drawings for the asset are kept and reappear when the asset is added
  again.
- **AS4** Watchlists: named, ordered lists of assets. The user creates, renames, deletes and
  reorders watchlists and their entries. An asset can be in several watchlists.
- **AS5** A watchlist row shows the last close, the change of the current day in percent, and
  whether an EWTS instance observes the asset.

### 4.2 Market Data

- **MD1** Charts show the candles the Downloader delivered, unchanged. TradeViewer never invents,
  fills or repairs candles. Only closed candles exist; there is no candle in progress.
- **MD2** New and changed candles appear on open charts without a reload, at the latest two
  minutes after the Downloader wrote them.
- **MD3** A timeframe the source does not provide is built from the largest fitting one, aligned
  to the session of the asset.
- **MD4** Gaps the Downloader reports are visible: as a marker on the chart and in the asset's
  status, with their reason.
- **MD5** The candle store can be deleted and rebuilt from the Downloader's files at any time
  without losing anything the user created.

### 4.3 Chart

- **CH1** Candlestick chart with a time scale, a price scale (linear or logarithmic), crosshair
  with values of the candle below it, zoom and pan.
- **CH2** Volume as a histogram below the candles.
- **CH3** Changing the asset or the timeframe of a chart keeps the visible time range where the
  new timeframe has data for it.
- **CH4** Scrolling back loads older candles without a visible interruption, down to the start of
  the stored history.
- **CH5** Indicators, calculated by TradeViewer: SMA, EMA, Bollinger Bands on the price; RSI,
  MACD and ATR in their own panes below. The user sets their parameters and colours per chart.
- **CH6** Indicator templates: a named set of indicators the user saves and applies to a chart.

### 4.4 Layouts

- **LY1** A layout shows 1, 2 or 4 charts side by side. Each chart has its own asset, timeframe
  and indicators.
- **LY2** Optional per layout: all charts follow the same asset; the crosshair is shown at the
  same time on all charts.
- **LY3** The user creates, renames, duplicates and deletes layouts. The last used layout opens
  at the next start.

### 4.5 Drawings

- **DR1** Tools: trend line, ray, horizontal line, vertical line, parallel channel, rectangle,
  arrow, text note, Fibonacci retracement, Fibonacci extension, wave label.
- **DR2** A drawing can be selected, moved, reshaped by its handles, restyled (colour, width,
  line style, text) and deleted. Undo and redo cover the actions of the current session.
- **DR3** A drawing belongs to an asset and a layout, not to a timeframe: it stays on the chart
  when the timeframe is switched.
- **DR4** Fibonacci tools take their levels from a preset the user can edit. Default
  retracement levels: 0, 0.236, 0.382, 0.5, 0.618, 0.786, 1. Default extension levels: 1,
  1.272, 1.618, 2, 2.618.
- **DR5** Wave labels are free text markers from a fixed list (1-5, A-C, W-X-Y, A-E, each in
  several degrees). TradeViewer does not check them against any Elliott Wave rule.
- **DR6** Drawings can be hidden and shown per chart, all at once or by tool type.

### 4.6 Downloader Monitoring

- **DL1** A status page shows the Downloader's state: reachable or not, version, last and next
  update cycle with result, queue sizes, and the state of every source.
- **DL2** Per asset: coverage per timeframe (wanted from, covered from and to), pending jobs,
  gaps with reason, last change, last error.
- **DL3** The page lists all subscriptions of the Downloader and marks those of other clients
  as read-only.
- **DL4** When the Downloader is not reachable, charts keep working with the stored candles and
  the status page says since when it is unreachable.

### 4.7 EWTS Instances

- **EW1** The user registers an EWTS instance by name and address, edits and removes it.
  Several instances can be registered.
- **EW2** The instance list shows for each: reachable or not, version, source, number of
  observed assets, time of the last analysis.
- **EW3** Observed assets: the user adds an asset to an instance with setup timeframe, history
  start and parameter profile, and removes it. An asset added to an instance is also added to
  TradeViewer (AS1, AS2) if it is not there yet.
- **EW4** Overview per instance: one table of its observed assets with setup timeframe, time of
  the last analysis, state of a watched setup or open plan, and grade of the best setup.
- **EW5** Asset details: the current count in short form, the setups with grade, score, entry,
  add level, stop and targets, the time they were found, and the history of earlier setups.
- **EW6** When an instance is not reachable, everything else keeps working; its data is shown
  as last received, with the time, and marked as stale.

### 4.8 Analysis Overlays

- **OV1** A chart can show the overlays of an EWTS instance for its asset: wave labels and wave
  lines, price levels (invalidation, entry, add, stop, targets), zones, Fibonacci levels and
  markers.
- **OV2** Overlays are grouped (e.g. preferred count, alternate count, setup, Fibonacci,
  structure). The user shows and hides each group per chart.
- **OV3** Overlays are read-only. The user can copy an overlay into an own drawing and edit the
  copy.
- **OV4** Overlays update on open charts when the instance has analysed the asset again.
- **OV5** The chart states which instance the overlays come from and the time of the analysis.

### 4.9 Test Runs

- **BT1** The user starts a backtest at an instance: asset, timeframe, period, account size,
  risk per trade and the optional overrides the instance offers.
- **BT2** A running test shows its progress and can be cancelled. Finished runs stay listed.
- **BT3** Result view: funnel, statistics, equity curve, and the list of trades and watches.
- **BT4** Selecting a trade or a watch opens a chart at its time with its entry, stop, exits and
  events drawn.
- **BT5** Two or more runs can be compared side by side by their statistics and equity curves.
- **BT6** A run can be exported as JSON; its trade list as CSV.

### 4.10 Alerts

- **AL1** EWTS sends alerts itself (e.g. by Telegram). TradeViewer sends nothing.
- **AL2** TradeViewer shows the alert history of every instance: time, asset, kind, text. An
  alert links to the chart of its asset.
- **AL3** A new alert appears in the open TradeViewer as a notice without a reload.
- **AL4** The user edits the alert settings of an instance in TradeViewer: channels, and which
  kinds of alerts are sent.

### 4.11 Scenario Viewer

Replaces the present way of checking a scenario of EWTS: generating a Pine script and loading
it in TradingView.

- **SC1** TradeViewer lists the scenarios EWTS provides in a shared folder: id, asset, context
  timeframe, direction, verdict, draft mark and kind (expert scenario, analysis case). The list
  can be filtered by asset, kind, direction and verdict and sorted by any column.
- **SC2** Opening a scenario shows a chart of its asset on its context timeframe, positioned
  on the time span of the scenario, with the scenario drawn: waves and their labels, levels,
  the trade with entry, add, stop moves and exits. The user can switch the timeframe; the
  drawing stays.
- **SC3** The drawing is grouped as in the scenario (waves, lower-degree 1-2, levels, trade).
  The user shows and hides each group.
- **SC4** A panel next to the chart shows the verdict, the reason and the notes of the
  scenario. Pointing at a label or level shows its price, time and reason.
- **SC5** The chart of a scenario uses the logarithmic price scale by default, because the
  levels of a scenario are measured in log scale.
- **SC6** "Previous" and "next" step through the filtered list without returning to it.
- **SC7** A new or changed scenario file appears when the list or the open scenario is
  reloaded; no restart is needed.
- **SC8** When the asset of a scenario is not in TradeViewer, the scenario says so and offers
  to add the asset.
- **SC9** Scenarios are read-only. Writing and changing scenario files stays in EWTS.

### 4.12 Workspace

- **WS1** Every change the user makes is saved immediately. There is no save button.
- **WS2** The workspace survives restarts, updates, and deleting or rebuilding the candle store.
- **WS3** The workspace is backed up once a day; the last 14 backups are kept. The user can
  download a backup and restore one.
- **WS4** An update of TradeViewer upgrades the workspace without losing data.

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

---

## 6. Out of Scope

- Analysis of any kind: wave counting, rule validation, setup detection, scoring (EWTS)
- Downloading market data (Downloader)
- Sending alerts (EWTS)
- Writing and editing scenarios (EWTS)
- The journal and the trades of EWTS (possible later)
- Order execution and broker connections
- Replay training and paper trading (decision 0005)
- Several users, shared workspaces
- A mobile application

---

## 7. Dependencies on EWTS

Sections 4.7 to 4.10 need interfaces EWTS does not have yet. They are listed in
docs/knowledge/ewts-api.md, chapter "Required Interface". Section 4.11 needs only the scenario
drawing files described there, which EWTS's existing overlay tool can write. Sections 4.1 to
4.6 and 4.12 need only the Downloader, whose interface exists.
