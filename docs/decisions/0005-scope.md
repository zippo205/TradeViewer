# 0005 - Scope

Status: accepted (2026-11-11, owner)

## Context

The first documents described three different products: a workstation with replay training and
paper trading, a user interface for the Downloader and EWTS, and a mixture of both. An earlier
implementation of the first one exists in ../TradeViewerMVP.

## Decision

TradeViewer is the user interface for the Downloader and EWTS: charts, the user's drawings,
analysis overlays, monitoring, test runs and alerts. Replay training and paper trading are not
part of TradeViewer.

## Consequences

- No replay, paper trading, order or position models in the architecture.
- ../TradeViewerMVP may be consulted for how something was solved; no code is copied from it.
- If replay or paper trading are wanted later, that is a new decision and a new epic; nothing
  in the architecture prepares for them.
