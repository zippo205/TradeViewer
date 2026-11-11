# 0002 - Persistence

Status: accepted (2026-11-11)

## Context

The first priority of the project is that nothing the user creates is ever lost. Market data,
in contrast, can always be read again from the Downloader's files.

## Decision

Two SQLite databases with separate lifetimes, accessed with Microsoft.Data.Sqlite and plain
SQL:

- `marketdata.db`: the candle store. A cache that may be deleted and rebuilt.
- `workspace.db`: everything the user creates. Saved on every change, backed up daily,
  upgraded by numbered SQL scripts.

## Reasons

- Separate files make the rule "market data updates never touch user data" a property of the
  file system instead of a convention in code.
- SQLite needs no server process, runs well on the Pi and has an online backup API.
- Plain SQL and numbered scripts are easy to read for the owner; the data model is small, so an
  ORM and a migration framework would add more than they save.

## Consequences

- User data refers to market data by asset and time only.
- Data that belongs to EWTS (observed assets, setups, test runs, alerts) is not stored in
  TradeViewer (decision 0004).
