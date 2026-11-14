# Implementation Plan: Solution Foundation

## Status

```text
Epic:     Solution Foundation (P0 - Foundation)
Status:   Implemented 2026-11-14 (all 9 steps); waiting for the owner's check of the arm64 image on the Pi and the approval
Branch:   feature/solution-foundation
Next:     Epic Workspace Persistence
Baseline: docs as of commit 81229eb
```

Update this section whenever a step is completed.

---

# Why This Epic Comes First

- Every other epic needs the solution, the layers and their dependency rules.
- The architecture tests protect the layer rules from the first line of code.
- The Docker image for arm64 and amd64 is cheapest to prove while the code is small.
- The chart module build is wired into `dotnet build` now, so no later epic has to touch the
  build.

---

# Knowledge Sources

```text
docs/architecture.md
    -> chapters 3 (solution structure, dependency rules), 4 (CQRS, results, errors), 10 (chart
       module build), 12 (configuration), 13 (deployment), 14 (testing), 15 (code style)
docs/specification.md
    -> NF6 (no outside requests), NF7 (arm64 and amd64), NF8 (no authentication), NF9 (512 MB),
       NF11 (browsers)
docs/decisions/0001-technology-stack.md
../Downloader (read-only template)
    -> Directory.Build.props, global.json, Application/Abstractions/Messaging, architecture
       tests, deploy/Dockerfile, deploy/docker-compose.yml, docs/plans/solution-foundation.md
```

---

# Scope

In scope:

- `global.json` (SDK 10.0.403), `Directory.Build.props` (net10.0, nullable, implicit usings,
  warnings as errors, LangVersion 12), `TradeViewer.sln`
- the seven `src/` projects and three test projects of architecture chapter 3, with the
  dependency rules as project references
- CQRS abstractions, `Result<T>`, `ErrorType`, `Error` (architecture chapter 4)
- configuration options and binding with start-up checks (architecture chapter 12): required
  `MarketData:RootPath` and `Storage:DataPath`, optional `Scenarios:RootPath`, `Enabled` flags
  of the Downloader and EWTS
- `GET /health`, problem details for errors, unknown `/api` routes answer 404
- `TradeViewer.Web`: an empty Blazor WebAssembly application served by the Api
- `TradeViewer.Chart`: TypeScript project with esbuild and Vitest, one placeholder module and
  test; the bundle goes to `TradeViewer.Web/wwwroot/chart/`; the MSBuild target runs `npm ci`
  and the bundling in `dotnet build` (`-p:SkipChartBuild=true` skips it)
- unit, integration and architecture tests; the Vitest test of the chart module
- `deploy/Dockerfile` (node, dotnet sdk, aspnet; arm64 and amd64), `deploy/docker-compose.yml`
  (memory limit, health check, configurable user, three volumes)
- README commands checked against the real project

Out of scope (later epics):

- SQLite databases and schema scripts (Workspace Persistence)
- the SignalR hub (first used by Assets and Market Data)
- Lightweight Charts and any chart code (Price Chart; the version is pinned there)
- Downloader and EWTS clients, feature folders

---

# Steps

```text
1  Solution and projects, global.json, Directory.Build.props, project references          done
2  CQRS abstractions, Result<T>, ErrorType in Application/Abstractions                     done
3  Architecture tests for the dependency rules (with a probe that they detect violations)  done
4  Configuration: options, binding, start-up checks, appsettings.Development.json         done
5  Api: /health, problem details, 404 for unknown /api routes, static files of Web          done
6  Web: empty Blazor WebAssembly application, Contracts project                             done
7  Chart: TypeScript project, esbuild, Vitest, MSBuild target in the Web project            done
8  Tests: unit (options and checks), integration (WebApplicationFactory), Vitest            done
9  Dockerfile and docker-compose; README commands verified                                  done
```

---

# Decisions Made During This Epic

- **Solution file:** the classic `TradeViewer.sln` (`dotnet new sln --format sln`), as in the
  Downloader, not the .NET 10 default `.slnx`. Projects are written by hand, not from
  `dotnet new` templates, which bring sample code and newer syntax.
- **Hosting the Web client:** the Api serves the published Blazor files with
  `UseBlazorFrameworkFiles` and `UseStaticFiles`, so there is one origin and one process
  (architecture chapter 2).
- **Docker:** it was not installed at first; the owner started Docker Desktop on 2026-11-14, so
  the image is built here (amd64). The arm64 image is proved by the owner on the Pi before the
  epic is approved.
- **Dependency rule added:** the Api needs a build reference to the Web project to serve its
  published files; the rule list in architecture chapter 3 missed it and now says so (no type of
  Web is used).
- **NF6 check:** an integration test scans the served Web files for addresses outside the
  application, so a CDN link cannot slip in.
- **Handlers return `Result<T>`:** `IQueryHandler<TQuery, TResult>` and
  `ICommandHandler<TCommand, TResult>` return `Task<Result<TResult>>`, as architecture chapter 4
  says. A command without a value uses `Unit` (`Result.Success()`). `Result<T>`, `Error`,
  `ErrorType` and `Unit` live in `Application/Abstractions/Messaging` next to the interfaces. An
  `Error` converts implicitly to a failed `Result<T>`.
- **Code style in practice:** explicit constructors and plain properties (no primary
  constructors, no collection expressions); handlers are registered explicitly, one line each,
  in `AddApplication`.
- **Dependency rule added:** Infrastructure may use Contracts. The ports in Application return
  the Contracts records (the EWTS records travel through unchanged, architecture chapter 8), and
  Infrastructure implements the ports, so the rule list in chapter 3 missed it.
- **Architecture tests** check three things: the project references and packages in the `.csproj`
  files (these work while an assembly is still empty), the namespace dependencies of the types
  (NetArchTest), and that no feature depends on another. `Probe/` holds deliberate violations
  and `RuleProbeTests` proves the rules fail on them; this found that NetArchTest matches a
  forbidden namespace by its start and does not accept a trailing dot, so a rule without the
  probe would have passed on everything.
- **Configuration lives in Infrastructure:** the options classes (defaults of architecture
  chapter 12 in the classes, not repeated in appsettings.json), `ConfigurationRules` (pure
  checks, unit tested), `StorageLocations` (the folders as absolute paths) and
  `StorageInitializer`, a hosted service that runs first. Options are validated with
  `ValidateOnStart`; the Application sees no configuration until a handler needs a value (then
  through a port).
- **Start-up checks:** a missing output root or scenario folder stops the start (a missing
  mount; the output root is never created), the data path is created and probed with a
  `.write-test-<guid>` file, a data path inside the output root or the scenario folder is
  refused, and a scenario folder that is not writable only sets `ScenariosWritable` to false.
- **`~` in paths** means the home folder, so `appsettings.Development.json` can point at the
  frozen copy (`~/marketdata`) without a user name in the repository.
- **Development:** `launchSettings.json` profile `http` on port 8080; the Downloader client name
  in Development is `tradeviewer-dev`.
- **Errors:** `AddErrorHandling` and `UseErrorHandling` (`AddProblemDetails`, `UseExceptionHandler`,
  `UseStatusCodePages`) make exceptions and empty error answers `application/problem+json`; an
  unexpected exception is a 500 without details. `ResultHttpExtensions` maps a handler's
  `Result<T>` to HTTP (200 with the value, 204 for `Unit`, and per `ErrorType` 400 with the fields,
  404, 409, 422, 503; every problem carries the error's `code`).
- **Routing:** `/health` returns `{"status":"ok"}`. `/api/{**rest}` is mapped to a 404 so an
  unknown API route never returns the page of the client; the Blazor fallback comes after it.
- **Tests written with the step:** `TradeViewerFactory` (temporary folders, environment
  `Testing`), health and routing tests, and error handling tests on a small test host. Step 8
  adds the rest (NF6 scan, Vitest, start-up failure tests).
- **Web:** one page (`Home`) and a layout with a header. The page reads `/health` through
  `HealthResponse`, a record in `TradeViewer.Contracts`, so the whole chain Web, Contracts, Api is
  exercised. No script, font or style comes from outside (NF6); `app.css` defines colours as
  variables and follows the dark or light setting of the browser. The Api serves every page of
  the client (`index.html` as fallback) and the framework files.
- **Not verified in a browser:** this environment has none. The served files and the routes are
  tested over HTTP; that the page renders and shows "Server: ok" has to be checked by the owner
  at http://localhost:8080 (`dotnet run --project src/TradeViewer.Api --launch-profile http`).
- **Chart module:** `src/TradeViewer.Chart` with `package.json` (exact versions: esbuild 0.28.2,
  TypeScript 7.0.2, Vitest 5.1.0; Node 22.12 or later because of Vitest 5), `tsconfig.json`
  (strict), `build.mjs` (esbuild, ESM, minified and without source map when the configuration is
  Release) and one placeholder module, `moduleInfo`, with its Vitest test. The calls of the interop
  contract come with the Price Chart epic. `package-lock.json` is committed; `node_modules` and
  `wwwroot/chart/` are ignored.
- **Build wiring:** two targets in `TradeViewer.Web.csproj`: `npm ci` when `package-lock.json`
  is newer than `node_modules/.package-lock.json`, then `npm run build` when a source file or the
  lock changed. Verified: an up-to-date build runs neither, a touched source runs the bundling, a
  touched lock runs both, a build from a deleted `node_modules` and `wwwroot/chart` works and the
  Api serves `/chart/index.js` (integration test). `-p:SkipChartBuild=true` skips both. The
  bundle is up to date by its timestamp, so switching from Debug to Release does not rebuild it;
  delete `wwwroot/chart/index.js` (or change a source file) to get the minified Release bundle.
- **npm 11 warning:** `npm ci` prints that esbuild's postinstall script is not covered by
  `allowScripts`. It is harmless (esbuild uses its platform package) and not configured away.
- **Tests of the step:** `StartupTests` start the real application with a wrong configuration
  (missing root or data path, output root not mounted, data path inside the output root, enabled
  Downloader with a wrong address) and expect the clear message; a writable scenario folder
  switches the own analysis on and a read-only one only off (skipped silently where the file mode
  is ignored, e.g. as root). `OutsideRequestsTests` (NF6) check that `index.html` and its style
  sheets reference only files of the application; a probe with an external font link and an
  `@import` made both tests fail, then the files were restored. JavaScript is not scanned: a
  library such as Lightweight Charts may contain a link (its attribution) that is no request.
- **Count at this point:** 39 unit, 19 architecture and 25 integration tests, 1 Vitest test.
- **Docker:** `deploy/Dockerfile` has three stages (node 24 builds the chart, the .NET SDK
  publishes with `-p:SkipChartBuild=true`, aspnet 10.0 runs it as user `app`; the runtime stage
  installs curl for the `HEALTHCHECK`). `deploy/docker-compose.yml` has the one service with
  `mem_limit: 512m`, the configurable `user:`, log rotation and two bind mounts (`MARKETDATA_PATH`
  read-only, `APPDATA_PATH`); `docker-compose.scenarios.yml` adds `SCENARIOS_PATH` and
  `Scenarios__RootPath` because a volume cannot be optional in one compose file. The names
  follow the Downloader's compose file (`name:`, `APP_UID`, `OUTPUT_PATH` is `MARKETDATA_PATH`).
- **Verified here on Docker Desktop (amd64):** the image builds (394 MB), the container becomes
  healthy, serves `/health`, `/`, the framework files, the minified chart bundle and a 404 with
  problem details for `/api/x`, runs as 1000:1000, uses about 26 MB of the 512 MB, cannot write to
  the read-only market data mount and can write to the scenario folder.
- **Not verified:** (1) the arm64 image and the Pi (owner, before approval); (2) that a missing
  host folder is refused. On Docker Desktop `create_host_path: false` was ignored and the engine
  created an empty root-owned folder, so the container started on nothing. The Downloader's compose
  file uses the same option; check on the Pi that the native engine refuses a missing
  `MARKETDATA_PATH` as intended.
- **README commands** were run: build, test, the filtered test, `npm test`, `dotnet format
  --verify-no-changes` (clean) and `dotnet run` with the `http` profile.
