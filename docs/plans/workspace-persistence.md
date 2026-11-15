# Implementation Plan: Workspace Persistence

## Status

```text
Epic:     Workspace Persistence (P0 - Foundation)
Status:   All steps done and deployed on the Pi 2026-11-15; waiting for the owner's check of the
          settings page (step 7 *), then approval and merge
Branch:   feature/workspace-persistence
Next:     Epic Assets and Market Data
Baseline: docs as of commit 15d8356 (main)
```

Update this section whenever a step is completed.

---

# Why This Epic Comes Now

- Priority 1 of the project is that nothing the user creates is ever lost. Every later epic that
  stores user data (assets, drawings, chart settings, instances) needs workspace.db, its schema
  upgrades and its backups first.
- Assets and Market Data is the next epic and already needs the workspace: assets live there
  (WS2: they survive a rebuild of the candle store).
- The riskiest parts (restoring a backup while the application runs, upgrading a database without
  losing data) are cheapest to get right while the database is almost empty.

---

# Knowledge Sources

```text
docs/specification.md
    -> 4.13 Workspace (WS1-WS5); NF9 (512 MB)
docs/architecture.md
    -> chapter 11 (persistence), 4 (ports, results), 9 (API: backups routes, file results),
       12 (Storage:Backup:KeepCount and Time), 14 (testing)
docs/decisions/0002-persistence.md
    -> two SQLite files, Microsoft.Data.Sqlite with plain SQL, numbered scripts
docs/runbooks/deploy.md
    -> section D (backups) is updated by this epic
../Downloader (read-only reference for how the same pattern was built there)
    -> Infrastructure/Sqlite (database access, numbered embedded scripts, PRAGMA user_version)
```

---

# Scope

In scope:

- `workspace.db` in `Storage:DataPath`: opened in WAL mode, created at start, upgraded by numbered
  embedded SQL scripts (`PRAGMA user_version`, one transaction per script); a database with a newer
  version than this build knows is refused
- first script `0001_initial.sql`: the `settings` table (key and JSON value), with a small settings
  repository; later epics add their tables with their own scripts
- backups with the SQLite online backup API into `<DataPath>/backups/`: before every upgrade, once
  a day at `Storage:Backup:Time` (UTC), a catch-up at start when a daily backup was missed, retention
  of the `Storage:Backup:KeepCount` newest
- list, download, restore (by name), restore from an uploaded file and "back up now", while the
  application runs
- the Workspace feature in the Application layer (queries, commands, ports) and its endpoints
  (`/api/workspace/backups`), file results (`Result<FileContent>`)
- the settings page in Web with the backups section (WS5); the default history and the EWTS
  instances come with their epics
- tests for WS1 to WS4 and the update of docs/runbooks/deploy.md, section D

Out of scope (later epics):

- `marketdata.db` and the candle store (Assets and Market Data)
- tables of assets, drawings, chart settings, indicator templates, EWTS instances (their epics)
- the default history setting and the EWTS instance list on the settings page
- weekly backup of /data to a second Pi (pi-setup.md, outside the application)

---

# Steps

```text
1  Microsoft.Data.Sqlite, WorkspaceDatabase (connections, WAL, access gate), file locations     done
2  Schema: numbered scripts, upgrade rules, 0001_initial.sql (settings), SettingsRepository,
   WorkspaceInitializer (hosted service after the folder checks)                                 done
3  Backups: create (online backup API), list, retention, validation of a file, restore,
   backup before an upgrade                                                                      done
4  Daily backup service (due times with TimeProvider, catch-up at start)                         done
5  Application: Workspace feature, ports, FileContent; Api: /api/workspace/backups (list,
   download, restore, upload, back up now); Contracts records                                    done
6  Web: settings page with the backups section, link in the header                               done
7  Tests across the steps, survival test (restart, update, restore), Docker build,
   runbook section D, architecture and README updated                                            done *
```

Each step has its tests: unit tests for the pure rules (naming, retention, due times, upgrade
order), integration tests with temporary folders for everything that touches SQLite or the API.

---

# Decisions of This Epic

Accepted by the owner on 2026-11-15. They are written into the architecture when the step that
needs them is done.

- **Durability over speed:** `PRAGMA synchronous=FULL` with WAL. The workspace is small and written
  rarely; a power loss on the Pi must not lose the last change (priority 1).
- **One retention rule:** the `KeepCount` newest backups in `backups/` are kept, whatever their kind.
  The kind is part of the file name: `workspace-<yyyyMMdd-HHmmss>-daily.db`, `-manual.db` and
  `-before-v<n>.db` (made before the upgrade to schema version n).
- **Catch-up:** when the application starts and the newest backup is older than the last due time
  (the Pi was off at 03:00), a daily backup is made at once.
- **A failed restore changes nothing:** a backup (or an uploaded file) is checked in a temporary
  copy first (opens as SQLite, `PRAGMA integrity_check`, is a workspace file, schema version not
  newer than this build); only then the current file is replaced. There is no safety copy of the
  replaced database (owner's answer on WS3).
- **Restore while running:** every use of workspace.db holds a shared lease; a restore takes all of
  them, closes the pooled connections, replaces the file and opens it again. Requests arriving
  meanwhile wait for the restore (a few milliseconds) instead of failing.
- **Time zone:** backup times and file names are UTC (MD7); the clock is `TimeProvider`.
- **Upload limit:** an uploaded backup may be at most 256 MB.
- **"Back up now" button** on the settings page (owner's wish, 2026-11-15; WS3 says so now). Such a
  backup has the kind `manual` and counts for the retention like any other.
- **Pi:** the new native SQLite library for arm64 is proved by the Docker build on the Pi; that is
  a deployment, so it needs the owner's go-ahead (docs/runbooks/pi-setup.md, section 5).

---

# Result

```text
Tests      79 unit, 65 integration, 19 architecture (C#); chart tests unchanged
Proved     start, upgrade with backup, refusal of newer and damaged files, backups, retention, daily
           and catch-up backup, restore by name and by upload while writes run, refused restores
           change nothing, restart; by hand with curl against the running application and against
           the Docker image (amd64, 30 MB of 512 MB)
* Open     the settings page was not seen in a browser by the agent (no browser tests in V1): the
           owner checks it by hand (http://192.168.178.47:8080/settings).
Pi         deployed 2026-11-15 at the owner's request (pi-setup.md, section 6): the arm64 image
           builds, backup and both restores work there, 34 MB of 512 MB
```

Found on the way:

- The name pattern of the backups accepted a trailing line break (`$` in a .NET regular
  expression); it now ends with `\z`.
- Kestrel answered an upload over its own limit with 500. The limit of the server is switched off
  for the upload route; the restore stops reading at 256 MB and answers 400 with the reason.
- A daily backup is also made at the very first start (there is none yet), so a new installation
  has one backup of the empty workspace.
- After a restore, other open windows still show the old state until they are reloaded (the page
  says so). The hub of chapter 9 can tell them once it exists.
