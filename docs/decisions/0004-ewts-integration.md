# 0004 - EWTS Integration

Status: accepted (2026-11-11, owner)

## Context

The specifications of EWTS and TradeViewer both claimed the screens for operating EWTS (the
"Workbench": observed assets, overview, test runs), both claimed to send Telegram alerts, and
TradeViewer's first architecture contained its own Elliott Wave rule validation.

## Decision

1. **TradeViewer is the only user interface.** EWTS provides the Workbench as an API and has no
   website of its own.
2. **EWTS sends alerts.** TradeViewer shows the alert history and edits the alert settings of
   an instance, but sends nothing.
3. **TradeViewer contains no analysis.** No wave counting, no rule validation, no setup logic.
   Wave labels the user draws are plain markers.
4. **EWTS owns its data.** Observed assets, setups, test runs, alerts and their settings are
   stored by EWTS. TradeViewer stores only which instances exist.
5. **The browser talks to TradeViewer.Api only**, which calls the instances.

## Reasons

- One website for the whole setup, and one implementation of every Elliott Wave rule.
- Alerts must arrive when TradeViewer is down, and the application that detects a setup is the
  one that knows when to alert.
- Analysis results as generic drawings (label, line, level, zone, marker) let EWTS change its
  analysis without a change in TradeViewer.

## Consequences

- EWTS's specification and backlog (Workbench) must be changed to "API only" and must include
  the interface in docs/knowledge/ewts-api.md, chapter "Required Interface".
- The epics that use EWTS depend on that work in the EWTS repository.
- The knowledge documents about Elliott Wave, Fibonacci, market structure and risk were
  removed from this repository; they live in EWTS.
