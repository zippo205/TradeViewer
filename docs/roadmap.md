# TradeViewer - Roadmap

Releases and the status of their epics. What an epic contains is in docs/backlog.md.

Status values: open, in progress, done (with date).

## V1 - Charts on the Downloader's Data

Usable without a running EWTS instance. The Scenario Viewer needs the scenario drawing files
of EWTS.

| Epic | Status |
|------|--------|
| Solution Foundation | done (2026-11-14) |
| Workspace Persistence | done (2026-11-15) |
| Assets and Market Data | done (2026-11-15) |
| Price Chart | done (2026-11-16) |
| Scenario Viewer | open |
| Watchlists | done (2026-11-16) |
| Drawing Tools | done (2026-11-17) |
| Scenario Analysis | open |
| Indicators | open |
| Downloader Monitoring | open |

## V2 - EWTS

Starts when EWTS provides its required interface.

| Epic | Status |
|------|--------|
| EWTS Instances | open |
| Analysis Overlays | open |
| Test Runs | open |
| Alerts | open |

## Later

Not planned; each needs a specification change first.

- Stocks, ETFs, Forex and commodities (when the Downloader provides the sources and the
  asset class for commodities)
- Symbol search from the Downloader's symbol catalog instead of the fixed list (AS2)
- Walk Forward runs (when EWTS provides them)
- Journal and trades of EWTS
