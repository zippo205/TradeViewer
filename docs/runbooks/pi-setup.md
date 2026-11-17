# Runbook: TradeViewer on the Raspberry Pi

What is specific to TradeViewer on the production Pi. The base of the Pi that all applications share
(OS, SSD at /data, Docker with its data on the SSD, clock, folders, the Git deploy-key method,
network, OS updates, backups) is **owned by the Downloader repository**:
`../Downloader/docs/runbooks/pi-setup.md`. It is not repeated here; this file links to it.

Production Pi: **home8** (192.168.178.47), user `admin`. WSL is for development and testing only.
Installing and updating TradeViewer itself: docs/runbooks/deploy.md, section C.

---

## 1. What TradeViewer needs from the Pi

```text
Base      pi-setup.md of the Downloader, sections 2-6: arm64 OS, SSD at /data, Docker 29 with data on
          the SSD and RequiresMountsFor=/data, clock synchronised, memory accounting on
          (cgroup_enable=memory: without it the 512 MB limit of the container is not enforced and
          docker stats shows 0B)
Layout    /home/admin/trading/TradeViewer    code: one git clone (compose files, Dockerfile)
          /data/marketdata                   the Downloader's output root, mounted READ-ONLY here
          /data/tradeviewer                  TradeViewer's own state (databases, backups)
Port      8080 (http only, no authentication: never forward it on the router)
Memory    512 MB limit of the container (NF9); about 26 MB measured idle on amd64
User      admin (uid/gid 1000) in the container, so it can write /data/tradeviewer
```

Rules, as for every application on the Pi:

- **Data never goes to the SD card.** /data/tradeviewer is on the SSD. If the SSD is missing at boot,
  Docker does not start (RequiresMountsFor) and nothing is written to the SD card.
- **Only the Downloader writes /data/marketdata.** TradeViewer mounts it read-only.
- The Pi **only pulls** the code; changes are made on WSL and pushed from there.

## 2. Check before installing

```bash
uname -m                                  # aarch64
findmnt /data                             # the SSD (ext4)
docker version --format '{{.Server.Version}}'
docker stats --no-stream                  # MEM USAGE shows real values (not 0B)
ls -ld /data/marketdata                   # exists, owned by admin: the Downloader's folder
```

## 3. Deploy key for this repository

GitHub allows a deploy key in one repository only, so TradeViewer has its own, read-only key and
SSH alias `github-tradeviewer`. The steps (key, GitHub setting, `~/.ssh/config`, clone) are in
docs/runbooks/deploy.md, section C1; the method is the Downloader's pi-setup.md section 7.

```text
Repository  github.com/zippo205/TradeViewer (private)
Key         ~/.ssh/id_ed25519_tradeviewer (on the Pi), read-only, "Allow write access" off
Alias       github-tradeviewer
```

## 4. Folders and ports of all applications

```text
Application   Code                              Data                       Port
Downloader    /home/admin/trading/Downloader    /data/marketdata (rw)      8081
                                                /data/downloader
TradeViewer   /home/admin/trading/TradeViewer   /data/marketdata (ro)      8080
                                                /data/tradeviewer
EWTS          /home/admin/trading/EWTS          /data/marketdata (ro)      8082-8089 (8081 + the
                                                /data/ewts/<n> (one        instance number)
                                                folder per instance,
                                                n = 1..8)
                                                /data/ewts/scenarios
                                                (shared by all instances
                                                and TradeViewer)
```

EWTS can run several times on the Pi, up to 8 instances numbered 1 to 8, each with its own data
folder `/data/ewts/<n>` and its own unique `instanceId`. Instance n listens on port 8081 + n: 8082
for instance 1, 8083 for instance 2, up to 8089. All instances write their scenario files into the
one folder `/data/ewts/scenarios`; TradeViewer mounts it as `SCENARIOS_PATH` and writes its
`*.expert.json` files there (docs/runbooks/deploy.md). TradeViewer reaches instance n at
`http://host.docker.internal:<8081 + n>` (deploy.md, "Reaching the Downloader and EWTS"). Do not
forward 8080-8089 on the router.

If this table and the Downloader's pi-setup.md differ, the Downloader's file is the original:
correct this copy.

## 5. Access for the agent

The owner has created an SSH key so that the coding agent can log in to the Pi as user `admin` and
deploy on its own. On the WSL machine `~/.ssh/config` has the entry `Host home8` (192.168.178.47,
user `admin`, key `~/.ssh/id_ed25519_home8`); the Pi accepts that key
(`~/.ssh/authorized_keys`; the Downloader's pi-setup.md, section 8, describes how to remove it).

```text
ssh home8 'hostname'        # home8  (a test that changes nothing)
```

Rules for the agent:

- **Always ask the owner first.** The key allows it to log in; it does not give permission to
  deploy. Each deployment, update, restart or other change on the Pi needs the owner's go-ahead in
  that conversation. The owner decides when something goes to the Pi.
- Read-only checks (`findmnt /data`, `docker ps`, `docker logs`, `ls`) are fine to look at the
  state once the owner has asked for a deployment or a check.
- Only the steps of this application: its folder `/home/admin/trading/TradeViewer`, its data
  `/data/tradeviewer` and its container. Never touch the Downloader, its folders or containers,
  `/data/marketdata`, or the OS and Docker settings; anything with `sudo` needs a separate question.
- Never print files that may hold secrets (`~/.bashrc`, `~/.ssh/`) into the conversation.
- Write files and long texts with the file tools, not with a shell heredoc: the text may contain its
  own `EOF` and the rest would run as commands.

## 6. State

```text
2026-11-14   TradeViewer deployed on home8 by the agent at the owner's request (deploy.md C2, C4,
             C5), commit a9b47ea of the branch feature/solution-foundation. The arm64 image built
             in about 100 seconds, the container is healthy, uses about 29 MB of its 512 MB, serves
             port 8080 (reachable from the PC), mounts /data/marketdata read-only, and reaches the
             Downloader through host.docker.internal:8081. A missing host folder is refused by the
             Pi's Docker engine (nothing is created). Data folder /data/tradeviewer (empty until
             the Workspace Persistence epic). The agent passed the settings inline (its SSH sessions do
             not read ~/.bashrc); the owner added the two export lines to ~/.bashrc the same day
             (deploy.md C3). After the epic is merged: git checkout main on the Pi.

2026-11-15   Epic Workspace Persistence deployed by the agent at the owner's request: commit a330cb0
             of the branch feature/workspace-persistence. The arm64 image with the native SQLite
             library built in 85 seconds; the container is healthy and uses about 34 MB.
             /data/tradeviewer now holds workspace.db (schema version 1) and backups/; the daily
             backup of the first start, "back up now", download, restore by name and restore of an
             uploaded file were proved with curl on the Pi. After the owner's approval the same day
             the Pi was switched to main (merge commit 872fe98); the image was unchanged, so the
             container kept running.

2026-11-15   Epic Assets and Market Data deployed by the agent at the owner's request: commit 055ed07
             of the branch feature/market-data, started with DOWNLOADER_ENABLED=true (passed
             inline; for a start by hand add `export DOWNLOADER_ENABLED=true` to ~/.bashrc, see
             deploy.md). The arm64 image built in 91 seconds. workspace.db was upgraded from schema
             version 1 to 2 with the backup workspace-20261115-192642-before-v2.db. Before the
             start the Downloader held 13 symbols for the clients ewts and ewts-dev and none for
             tradeviewer. BTCUSDT was added through the API: the Downloader accepted the lookup
             and the subscription (1d and 1w clipped to 2017), tradeviewer is now a subscriber of
             BTCUSDT, and the default history (4,677 files) was imported within seconds. The
             update cycle of 19:30:30 UTC was watched: the 15m candle of 19:15 was in TradeViewer
             at 19:30:33 without any request. Memory 81 MB of 512 MB. After the owner's approval
             the same day the Pi was switched to main (merge commit 6eba5cc) and rebuilt, again
             with DOWNLOADER_ENABLED=true; the assets the owner had added on the page meanwhile
             (ADAUSDT, VIRTUALUSDT) and their subscriptions were all there after the restart.

2026-11-16   Epics Price Chart and Watchlists deployed by the agent at the owner's request: main at
             commit 5ea6929, with DOWNLOADER_ENABLED=true (passed inline). The first build failed:
             the chart module's folder src/data had never been committed, because the ignore rule
             `data/` matched it (the builds on WSL had the files on disk). The rule now names only
             `/data` at the root and the files are in the repository; the old container kept
             running during that time. The arm64 image then built in 71 seconds. workspace.db was
             upgraded from schema version 2 to 3 with the backup
             workspace-20261116-191055-before-v3.db. The container is healthy and uses 65 MB of
             512 MB; the three assets (ADAUSDT, BTCUSDT, VIRTUALUSDT) are in the Crypto watchlist
             with their last prices. The Downloader reports no gaps for them, so the gap icons of
             the chart are still not seen with real data.

2026-11-17   Epics Drawing Tools and Symbol Catalog deployed by the agent at the owner's request:
             main at merge commit afc92d0, with DOWNLOADER_ENABLED=true (passed inline). The arm64
             image built in 74 seconds. workspace.db was upgraded from schema version 3 to 6 with
             the backup workspace-20261117-212235-before-v6.db. The container is healthy and uses
             69 MB of 512 MB; the five assets (ADAUSDT, AVAXUSDT, BTCUSDT, DOGEUSDT, VIRTUALUSDT)
             are there, the symbol picker searches the Downloader's catalog, the default Fibonacci
             presets hold their levels that are switched off, and the log has no errors.
```
