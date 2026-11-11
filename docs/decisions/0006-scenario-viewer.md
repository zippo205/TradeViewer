# 0006 - Scenario Viewer

Status: accepted (2026-11-11, owner)

## Context

EWTS is developed against scenarios: YAML files with the waves of a 1-2 and optionally a trade
(../EWTS/docs/knowledge/scenarios.md). To check one, the owner generates a Pine script, pastes
it into TradingView and compares it with the candles. TradeViewer shall replace that step.

EWTS's tool `scenario-to-pine.py` already builds the drawing of a scenario as plain shapes
before it writes Pine.

## Decision

1. TradeViewer lists and draws scenarios; it does not write them.
2. EWTS writes, next to each scenario, a drawing file (JSON) with the head of the scenario and
   its shapes in the vocabulary of the analysis drawings (docs/knowledge/ewts-api.md).
3. The files are exchanged through a shared folder that TradeViewer only reads
   (`Scenarios:RootPath`), as market data is exchanged with the Downloader.
4. TradeViewer reads the folder on request and stores nothing.

## Reasons

- Building the shapes needs the scenario format, log-scale levels and the entry model rules.
  That is analysis knowledge and stays in EWTS (decision 0004); TradeViewer would otherwise
  have to follow every change of the scenario format.
- Scenarios are files in the EWTS repository, not data of a running instance, so a folder fits
  better than an API and works when no EWTS instance runs.
- A few hundred small files need no index; reading on request means a changed file is visible
  at once.

## Consequences

- EWTS has to export the drawing files; until then the viewer has nothing to show.
- The scenario viewer and the analysis overlays share the same shape primitives in the chart
  module.
