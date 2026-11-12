# TradeViewer - Backlog

The epics in the order they are built. An epic names the requirements of docs/specification.md
it delivers; it does not repeat them. Releases and status are in docs/roadmap.md.

How an epic is implemented (classes, steps) is decided in its plan, docs/plans/<epic-name>.md,
within docs/architecture.md and docs/decisions/.

---

# P0 - Foundation

## Epic: Solution Foundation

Branch: `feature/solution-foundation`

- Solution, projects and build settings as in architecture chapter 3
- Architecture tests for the dependency rules
- Chart module build wired into `dotnet build`
- Configuration binding with start-up checks (architecture chapter 12)
- Health endpoint, error handling as problem details
- Docker image for linux/arm64 and linux/amd64, compose file

Requirements: NF6, NF7, NF8
Outcome: the empty application builds, passes its tests and runs in Docker on a Raspberry Pi 5.

## Epic: Workspace Persistence

Branch: `feature/workspace-persistence`

- workspace.db with numbered schema scripts and upgrade at startup
- Daily backup, list, download and restore of backups

Requirements: WS1, WS2, WS3, WS4
Depends on: Solution Foundation
Outcome: a place for user data that survives restarts, updates and restores.

## Epic: Assets and Market Data

Branch: `feature/market-data`

- Downloader client with the copied contracts
- Symbol list per source; add an asset, loading progress, change its history, remove it
- CSV reader for the Binance spot format, file rules and validation
- Candle store, import, rescan, sync service on the Downloader's change numbers
- Candle endpoint in column format, with the current candle built from the smallest timeframe

Requirements: AS2, AS3, AS4, AS5, MD1, MD2, MD5, MD6, MD8, NF4, NF5
Depends on: Workspace Persistence
Knowledge: docs/knowledge/downloader-api.md
Outcome: the candles of every added asset are in the store and stay up to date.

## Epic: Price Chart

Branch: `feature/price-chart`

- Chart module with candles, volume, scales, crosshair, zoom, pan and paging
- `ChartHost` and the interop contract
- One chart page with asset and timeframe selection, address per chart, settings per asset
- Live update on `CandlesUpdated`

Requirements: CH1, CH2, CH3, CH4, CH5, CP1, CP2, CP3, MD7, NF1, NF2
Depends on: Assets and Market Data
Outcome: any stored asset can be charted in any timeframe within the performance targets.

---

# P1 - Workstation

## Epic: Scenario Viewer

Branch: `feature/scenario-viewer`

- Reader for the scenario drawing files, list and detail endpoints
- Primitives of the chart module for the overlay shapes (line, label, level, zone, marker,
  vertical line), with groups and the detail text on pointing
- Scenario list with filters, scenario page with chart, panel, previous and next

Requirements: SC1, SC2, SC3, SC4, SC5, SC6, SC7, SC8, SC9
Depends on: Price Chart; EWTS writes the drawing files (architecture, open point O1)
Knowledge: docs/knowledge/ewts-api.md (Drawings, Scenario Files)
Outcome: a scenario is checked in TradeViewer instead of pasting a Pine script into
TradingView.

## Epic: Watchlists

Branch: `feature/watchlists`

Requirements: AS1, AS6
Depends on: Price Chart
Outcome: the user finds the assets in their five lists and opens any of them in the chart.

## Epic: Drawing Tools

Branch: `feature/drawing-tools`

- Interaction layer of the chart module: tool state machine, hit testing, handles
- One primitive per tool, Fibonacci presets, wave label markers
- Saving in the background, undo and redo

Requirements: DR1, DR2, DR3, DR4, DR5, DR6, NF3
Depends on: Price Chart
Outcome: drawings behave the same on every timeframe and are never lost.

## Epic: Indicators

Branch: `feature/indicators`

- Indicator calculations in the Domain, verified against reference values
- Indicator endpoint, panes below the chart, settings per chart, templates

Requirements: IN1, IN2
Depends on: Price Chart

## Epic: Downloader Monitoring

Branch: `feature/downloader-monitoring`

- Status page of the Downloader, coverage and gaps per asset, all subscriptions
- Gap markers on the chart
- Aggregation of timeframes a source does not provide

Requirements: DL1, DL2, DL3, DL4, MD3, MD4
Depends on: Assets and Market Data
Knowledge: docs/knowledge/downloader-api.md

---

# P2 - EWTS

These epics need the API of docs/knowledge/ewts-api.md, chapter "Required Interface", which
EWTS has to build first (architecture, open point O1). TradeViewer's side can be
developed against recorded answers; an epic is finished only when it works against a real
instance.

## Epic: EWTS Instances

Branch: `feature/ewts-instances`

- EWTS client with the copied contracts, polling service
- Register instances, status, observed assets, overview, asset details

Requirements: EW1, EW2, EW3, EW4, EW5, EW6
Depends on: Assets and Market Data, Watchlists
Knowledge: docs/knowledge/ewts-api.md

## Epic: Analysis Overlays

Branch: `feature/analysis-overlays`

- Overlays of an instance on the chart, with the shape primitives of the Scenario
  Viewer; groups with show and hide, copy to an own drawing
- Update on `OverlaysChanged`

Requirements: OV1, OV2, OV3, OV4, OV5
Depends on: EWTS Instances, Scenario Viewer, Drawing Tools
Knowledge: docs/knowledge/ewts-api.md

## Epic: Test Runs

Branch: `feature/test-runs`

- Start, progress with running statistics, cancel, list, result view, comparison, export
- Scenarios of a run listed with their figures and opened in the Scenario Viewer

Requirements: BT1, BT2, BT3, BT4, BT5, BT6, BT7, BT8
Depends on: EWTS Instances, Scenario Viewer
Knowledge: docs/knowledge/ewts-api.md

## Epic: Alerts

Branch: `feature/alerts`

Requirements: AL1, AL2, AL3, AL4
Depends on: EWTS Instances
Knowledge: docs/knowledge/ewts-api.md
