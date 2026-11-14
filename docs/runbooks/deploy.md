# Runbook: Run and Deploy

How to run TradeViewer: in Docker on WSL (A), directly with `dotnet run` on WSL for development (B),
in Docker on the Raspberry Pi (C, production), backups (D) and problems (E). Commands for building
and testing are in README.md; configuration keys in docs/architecture.md (chapter 12); the compose
file is deploy/docker-compose.yml.

TradeViewer only **reads** the Downloader's output root. It never writes there, so running it
cannot damage the market data.

**Never run the container and `dotnet run` at the same time on the same port.** Both listen on 8080
by default, and Docker Desktop publishes the container's port outside WSL, so `localhost:8080` may
reach either one. Check `docker ps` first.

---

## Folders and settings

```text
MARKETDATA_PATH   the Downloader's output root, mounted read-only
                  WSL: /home/admin/marketdata (frozen copy)   Pi: /data/marketdata
APPDATA_PATH      TradeViewer's own data: marketdata.db, workspace.db, backups
                  WSL: /home/admin/tradeviewer                Pi: /data/tradeviewer
SCENARIOS_PATH    EWTS's scenario folder, writable (only with docker-compose.scenarios.yml)
TRADEVIEWER_PORT  host port of the website (default 8080; the Downloader uses 8081)
APP_UID/APP_GID   user the container runs as (default 1000:1000 = the first WSL/Pi user)
```

Both folders must exist; APPDATA_PATH must be writable for that user, MARKETDATA_PATH readable. The
service does not create the output root: it stops at startup if it is missing. The names differ from
the Downloader's compose file on purpose (`OUTPUT_PATH`, `DOWNLOADER_APPDATA_PATH`), so both can be
exported in one `~/.bashrc` without clashing.

Without these variables compose falls back to the repository's `data/` folders (git-ignored): the
service then starts on empty folders instead of the real data.

## One-time setup in ~/.bashrc (WSL)

```bash
# .NET 10 SDK (installed in ~/.dotnet)
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"

# TradeViewer folders, read by deploy/docker-compose.yml
export MARKETDATA_PATH=/home/admin/marketdata
export APPDATA_PATH=/home/admin/tradeviewer
```

Then `mkdir -p /home/admin/tradeviewer`, `source ~/.bashrc` and check
`echo "$MARKETDATA_PATH $APPDATA_PATH"`.

---

## A. Docker on WSL

Requirements: Docker Desktop running on Windows with WSL integration enabled.

```bash
cd /home/admin/workspace/TradeViewer
git pull
docker compose -f deploy/docker-compose.yml up -d --build
```

Check:

```bash
docker ps --filter name=tradeviewer                   # Up ... (healthy)
curl -s localhost:8080/health                         # {"status":"ok"}
docker logs tradeviewer 2>&1 | grep Folders           # market data ... (read only), data /data/app, scenarios off
docker exec tradeviewer touch /data/marketdata/x      # must fail: Read-only file system
docker stats --no-stream tradeviewer                  # about 26 MB of the 512 MB limit
```

Open http://localhost:8080 in the browser: the page shows "Server: ok".

Everyday commands (from the repository root):

```text
Stop                         docker compose -f deploy/docker-compose.yml down
Start / update after a pull  docker compose -f deploy/docker-compose.yml up -d --build
Restart without rebuild      docker restart tradeviewer
Logs                         docker logs -f --since 30m tradeviewer
```

With the scenario folder (EWTS): export `SCENARIOS_PATH` and add
`-f deploy/docker-compose.scenarios.yml` to every compose command above (also `down`).

**Docker Desktop differs from the Pi:** a missing host folder is *created* (empty, owned by root)
although the compose file says `create_host_path: false`. On WSL check the paths yourself.

## B. WSL without Docker (development)

```bash
docker compose -f deploy/docker-compose.yml down       # if the container runs
dotnet run --project src/TradeViewer.Api --launch-profile http     # http://localhost:8080
```

Development uses the frozen copy `~/marketdata` and the repository's `data/app`
(appsettings.Development.json). Other values as arguments after `--`, named like the setting with
`:` (`--MarketData:RootPath=/x`), or as environment variables with `__` (`MarketData__RootPath`).
Order of precedence: arguments, environment variables, appsettings. Stop with Ctrl+C.

---

## C. Raspberry Pi (Docker, production)

Target: **home8** (192.168.178.47), user `admin`. The image is built on the Pi itself (arm64). The
base of the Pi (OS, SSD at /data, Docker, clock) is in docs/runbooks/pi-setup.md.

```text
Code     /home/admin/trading/TradeViewer   git clone, compose file
Data     /data/marketdata                  the Downloader's output root (read only here)
         /data/tradeviewer                 marketdata.db, workspace.db, backups (on the SSD)
Port     8080
```

### C1. Get the code (read-only deploy key)

The GitHub repository (account zippo205) is private. The Pi gets its own key that can only **read**
this repository (the method is the Downloader's pi-setup.md, section 7):

```bash
ssh-keygen -t ed25519 -C "home8 tradeviewer deploy key" -f ~/.ssh/id_ed25519_tradeviewer -N ""
cat ~/.ssh/id_ed25519_tradeviewer.pub
```

On GitHub: repository TradeViewer -> Settings -> Deploy keys -> Add deploy key -> paste, **"Allow
write access" off**. Then on the Pi, add this block to `~/.ssh/config`:

```text
Host github-tradeviewer
    HostName github.com
    User git
    IdentityFile ~/.ssh/id_ed25519_tradeviewer
    IdentitiesOnly yes
```

```bash
chmod 600 ~/.ssh/config
ssh -T git@github-tradeviewer      # first time answer "yes" if the fingerprint is
                                   # SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU
git clone git@github-tradeviewer:zippo205/TradeViewer.git /home/admin/trading/TradeViewer
cd /home/admin/trading/TradeViewer
git checkout feature/solution-foundation      # until the epic is merged into main
```

The Pi only pulls. Changes are made on WSL and pushed from there (remote alias `github-private`
and the private identity of the repository; never the global work identity).

### C2. Folders

```bash
findmnt /data                                  # the SSD must be mounted
mkdir -p /data/tradeviewer                     # /data/marketdata belongs to the Downloader
ls -ld /data/marketdata /data/tradeviewer      # both owned by admin
```

Never create these folders on the SD card (pi-setup.md): if the SSD is missing, fix it first.

### C3. Settings in ~/.bashrc (on the Pi)

Append these lines to `~/.bashrc` on the Pi, then run `source ~/.bashrc`:

```text
# TradeViewer (read by /home/admin/trading/TradeViewer/deploy/docker-compose.yml)
export MARKETDATA_PATH=/data/marketdata
export APPDATA_PATH=/data/tradeviewer
```

Check with `echo "$MARKETDATA_PATH $APPDATA_PATH"`. Add `APP_UID`/`APP_GID` only if `id -u` /
`id -g` are not 1000. Do not export `OUTPUT_PATH` or `DOWNLOADER_APPDATA_PATH` for TradeViewer: they
belong to the Downloader's compose file.

### C4. Build and start

```bash
cd /home/admin/trading/TradeViewer
docker compose -f deploy/docker-compose.yml up -d --build
```

The first build takes a few minutes (node, .NET SDK, restore); later builds use the cache.

### C5. Check

```bash
docker ps --filter name=tradeviewer                 # Up ... (healthy)
curl -s localhost:8080/health                       # {"status":"ok"}
docker logs tradeviewer 2>&1 | grep Folders         # market data /data/marketdata (read only), data /data/app
docker stats --no-stream tradeviewer                # real numbers, below 512MiB
docker exec tradeviewer touch /data/marketdata/x    # must fail: Read-only file system
```

From the PC open http://192.168.178.47:8080: the page shows "Server: ok".

A missing host folder must be **refused** on the Pi (the native Docker engine honours
`create_host_path: false`). Check once:

```bash
docker compose -f deploy/docker-compose.yml down
MARKETDATA_PATH=/data/does-not-exist docker compose -f deploy/docker-compose.yml up -d
# expected: an error that the bind source path does not exist
ls -d /data/does-not-exist                          # must NOT exist
docker compose -f deploy/docker-compose.yml up -d   # start normally again
```

If the folder was created, tell the owner: the protection against a missing mount is then not in
place (the Docker service itself still waits for /data: `RequiresMountsFor`, pi-setup.md).

### C6. Update and everyday commands

```bash
cd /home/admin/trading/TradeViewer
git pull
docker compose -f deploy/docker-compose.yml up -d --build
docker image prune -f                               # old image layers
```

```text
Stop       docker compose -f deploy/docker-compose.yml down
Restart    docker restart tradeviewer
Logs       docker logs -f --since 30m tradeviewer
Back to an earlier version   git checkout <commit-or-tag> and run the build command above again
```

The container restarts by itself (`restart: unless-stopped`), also after a reboot of the Pi.
Stopping is always safe. The user data of TradeViewer (later: workspace.db) lives in
`/data/tradeviewer`; the market data is never touched.

## D. Backups

Not needed yet: until the Workspace Persistence epic TradeViewer stores nothing of its own. After
it, the application backs up workspace.db once a day into `backups/` below `APPDATA_PATH`
(requirement WS3, 14 copies); the weekly backup of /data to a second Pi is planned in the
Downloader's pi-setup.md.

## E. Problems

```text
Container exits at once    docker logs tradeviewer: "MarketData:RootPath does not exist" = the volume
                           is not mounted; "must not be the Downloader's output root" = wrong paths
Permission denied          APPDATA_PATH is not writable for APP_UID:APP_GID (ls -ld; id -u; id -g)
Port already in use        another container or dotnet run on 8080: docker ps; TRADEVIEWER_PORT=8090
Page shows "not reachable" does the server answer? curl -s localhost:8080/health
Unhealthy                  docker inspect tradeviewer --format '{{json .State.Health}}'
```
