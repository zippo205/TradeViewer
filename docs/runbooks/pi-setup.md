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
EWTS          /home/admin/trading/EWTS          /data/marketdata (ro)      8082-8089 (one port per
                                                /data/ewts (one folder     instance, up to 8 instances)
                                                per instance)
                                                /data/ewts/scenarios
                                                (shared with TradeViewer)
```

EWTS can run several times on the Pi, each instance with its own data and its own unique
`instanceId`. The first instance uses port 8082, every further one the next number, up to 8089.
All instances write their scenario files into the one folder `/data/ewts/scenarios`; TradeViewer
mounts it as `SCENARIOS_PATH` and writes its `*.expert.json` files there (docs/runbooks/deploy.md).
TradeViewer reaches an instance at `http://host.docker.internal:<port>` (deploy.md, "Reaching the
Downloader and EWTS").

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
2026-11-14   TradeViewer is not deployed on home8 yet. The image was built and checked on
             Docker Desktop (amd64) only. To do: deploy.md C1-C5, in particular that the arm64
             image builds, that it is healthy, and that a missing host folder is refused.
```
