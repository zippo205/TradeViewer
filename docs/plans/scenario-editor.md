# Implementation Plan: Scenario Editor

## Status

```text
Epic:     Scenario Editor (P1 - Workstation)
Status:   In work; plan approved by the owner 2026-11-20; steps 1 to 4 done
Branch:   feature/scenario-editor
Next:     step 5 (chart module: the named points as handles, point tools)
Baseline: docs as of commit 692f02c (main)
```

Update this section whenever a step is completed.

---

# Why This Epic Comes Now

- EWTS delivered 13 converted expert scenarios on 2026-11-20 (shared folder on WSL,
  `/home/admin/data/ewts/scenarios/expert/`), made for the development of this editor. They are
  the fixtures the backlog asks for.
- Decision 0011: the editor is built first; the Test-Run Viewer waits for EWTS's test-run files.
- It needs Price Chart and Drawing Tools, both done.

---

# Knowledge Sources

```text
docs/specification.md
    -> 4.12 SC1-SC16, SC20, SC21 (both pages and the editor), OV6 (colours by role), CP2 (the log scale of
       a scenario is not saved per asset, SC3), 4.6 (points are moved and removed like drawings),
       AS3 and SC6 (history of a scenario's span), WS1 (nothing lost, last save wins)
docs/architecture.md
    -> 4 (feature Scenarios; ports IExpertScenarioStore in Abstractions/Ewts), 5 (scenario files
       are contract records, form checks in Domain/Scenarios), 6 (a scenario span moves the
       history start back), 8 "Scenario Files" (store, atomic writes, unknown fields kept, id
       under a lock, cache by size and modified time), 9 (/api/scenarios/expert), 10 (setOverlays,
       point tools), 12 (Scenarios:RootPath, a folder that is not writable switches off writing),
       14 (fixtures, temporary folder)
docs/decisions/0006, 0008, 0010, 0011, 0012 (levels, free drawings in the file)
docs/knowledge/ewts-interface.md
    -> "Drawings" (shape vocabulary), "Scenario Files", "Expert Scenario Files" (format, id rule,
       snap rules, agreed points)
docs/plans/drawing-tools.md
    -> interaction state machine, save queue, exact anchor times, browser checks
```

---

# Scope

In scope:

- The 13 converted files copied into `tests/fixtures/scenarios/expert/` (tests never read the
  shared folder).
- Store of `expert/<id>.json`: list (heads, cached by size and modified time), read, save
  (atomic, unknown fields kept, id taken on the first save under a lock), delete; a file it
  cannot read is listed with its error.
- Form checks (SC15) and the id rule (SC14) in Domain/Scenarios; the snap rules of the format.
- Shapes built from the named points (Application), in the vocabulary of "Drawings", coloured by
  role (OV6).
- Chart module: primitives for the six shape types (line, label, level, zone, marker, vertical
  line) with groups and the detail text on pointing (`setOverlays`); the editable named points
  and the point tools.
- The scenario chart shared with the later Test-Run Viewer (one Blazor component: chart, list at
  the right, panel below, previous and next, groups shown and hidden, log scale by default).
- The three timeframe levels (SC11) with their visibility (SC21); the free drawings of the chart
  page on every level, saved in the file's `drawings` section (SC20).
- The page `/scenarios/expert` with list, filters and sort (SC7), new scenario (SC9), point tools
  by level and verdict (SC10), panel (SC12), autosave (SC13), id at the first save (SC14), form problems at
  their field or point (SC15), delete (SC16), reload and the 30-second check (SC5), missing
  asset or history (SC6).

Out of scope:

- The Test-Run Viewer (SC17-SC19, `feature/test-run-viewer`); only the shared component is
  built so that it can take a test-run scenario later.
- Overlays of EWTS instances (OV1-OV5): the primitives are built here, the overlay epic uses them.
- Rule checks of the strategy (EWTS's).
- A hub event for scenario files: the list asks every 30 seconds (architecture, chapter 9).

---

# Design

## Files and the store (Infrastructure)

```text
Infrastructure/Scenarios/
├── ExpertScenarioStore      IExpertScenarioStore on <root>/expert/
├── ExpertScenarioJson       read: known fields into the contract record, the whole file kept as a
│                            JsonObject; write: known fields laid over that object
└── ScenarioFileNames        id and file-name rules, so no request leaves expert/
```

- Unknown fields: the store keeps the file's `JsonObject` and writes the known fields over it.
  Objects are merged field by field at every depth (`legacy` stays as it is; a future field
  inside `waves` too); the items of `exits` and `stopMoves` keep their unknown fields by
  position. In the 13 files every unknown field is inside `legacy` (checked 2026-11-20).
- A save writes `<id>.json.part`, then renames it over `<id>.json`. The first save of a new
  scenario takes the next free `X<n>` of its prefix under a lock (one `SemaphoreSlim` in the
  store) and creates the file with `FileMode.CreateNew`, so a file EWTS or a second window made
  meanwhile is never overwritten.
- `createdAt` and `updatedAt`: see proposal 5.
- Without `Scenarios:RootPath` the feature is off; with a folder that is not writable the editor
  is read-only (architecture, chapter 12). The stale comments about `*.expert.json` in
  `StorageLocations`, `Options` and `StorageInitializer` are corrected in step 1.

## Domain/Scenarios

```text
ExpertScenarioId        <symbol>-<context tf>-<yyyy-MM>-X<n>, next free number of a prefix
ScenarioLevels          context, setup, trigger from the context timeframe (1d/4h/1h, 4h/1h/15m)
ScenarioPointName       wave1Start, wave1End, wave2End; wave 1 iEnd-vEnd or aEnd-cEnd; wave 2
                        aEnd-cEnd; entry, add, stop, stopMove, exit; with its level and, for a wave point, whether it is a low
                        or a high (long only, SC9)
ScenarioSnap            the snap rules of the format on candles of the section's timeframe
ScenarioFormRules       SC15: order of the times, fields the verdict and the pattern require,
                        shares adding up to 100, a reason for a no-trade, points inside
                        from..to, drawings by DrawingRules; one problem per field
                        or point, never a reason to refuse a save
```

The Domain sees no contract record: the feature maps the record to small Domain values and the
problems back to field paths (`trade.exits[1].share`).

## Application, feature Scenarios

```text
ListExpertScenariosQuery     the heads (SC7) and the files that could not be read
GetExpertScenarioQuery       the scenario, its shapes, its problems, asset and history state (SC6)
PlaceScenarioPointCommand    a point placed or moved at a time and price on the chart's timeframe:
                             snapped by its section's timeframe, returns the changed scenario,
                             shapes and problems; writes nothing (proposal 1)
SaveExpertScenarioCommand    write the scenario (first save takes the id); returns id, shapes,
                             problems
DeleteExpertScenarioCommand  SC16
ExpertScenarioShapes         service: named points -> groups waves, wave2, trade (proposal 6)
```

- SC6: Get tells whether the asset is in TradeViewer and whether its stored history covers
  `from`. A missing asset is added only by the user (the page calls the existing add of the
  assets page). A history that starts after `from` is moved back through `IAssetRepository` (the
  rule of architecture chapter 6); the import then loads it.

## Api

```text
GET    /api/scenarios/expert                 list (SC7)
POST   /api/scenarios/expert                 first save, returns the id (SC14)
GET    /api/scenarios/expert/{id}            scenario, shapes, problems, asset state
PUT    /api/scenarios/expert/{id}            save (SC13)
DELETE /api/scenarios/expert/{id}            SC16
POST   /api/scenarios/expert/snap            proposal 1: snap a point, build shapes and problems
                                             of a scenario that is not saved yet or being edited
```

The `snap` route is new against architecture chapter 9; it is written there in step 3.

## Chart module

```text
primitives/overlays/     one series primitive for all overlay shapes: line, label, level, zone,
                         marker, verticalLine; colours from the role (OV6, theme.ts); groups hidden
                         by key; hit test for the detail text (price, time, reason)
tools/scenarioPoints.ts  the named points of an expert scenario as handles over the shapes:
                         place with a point tool ("1-2", "i-v", "a-c" place several in a row), move
                         by dragging, remove with Delete; reports pointPlaced(name, index, time,
                         price) and pointRemoved(name, index)
```

The free drawings (SC20) are the drawing layer and interaction of the chart page, unchanged; the
scenario page gives it the drawings of the levels shown (SC21) and saves what it reports into the
file instead of the workspace. A drawing created on a level gets that level.

```text
Blazor -> chart    setOverlays(groups, hiddenKeys), setScenarioPoints(points, tool)
chart  -> Blazor   pointPlaced, pointRemoved, overlayHovered (for the panel)
```

While a point is dragged, only the point follows the pointer (NF3). When it is let go, Web asks
the Api to snap it, and the lines through it are redrawn from the answer (one local request).

## Web

```text
Scenarios/
├── ScenarioChart.razor       chart, list at the right (collapsible), panel below, previous and next,
│                             groups, log scale by default (SC2-SC4); shared with the Test-Run Viewer
├── ExpertScenarioList.razor  columns id, asset, context tf, verdict, draft; filter and sort (SC7)
├── ScenarioLevelBar.razor    the three timeframes, a show/hide switch per level (SC11, SC21)
├── ExpertScenarioPanel.razor head fields, points with their reasons, exits with price, share and
│                             kind, problems at their field (SC12, SC15)
├── ScenarioPointTools.razor  the point tools of the level shown and the verdict (SC10), next to
│                             the drawing tool rail of the chart page (SC20)
├── ExpertScenarioSession.cs  the scenario in work, undo and redo, dirty state
└── ExpertScenarioSaveQueue   newest change only, retry, "not saved", as DrawingSaveQueue
Pages/ExpertScenarios.razor   /scenarios/expert and /scenarios/expert/{id}
```

---

# Steps

```text
1  Fixtures and store: copy the 13 files to tests/fixtures/scenarios/expert/; contract records
   (Contracts/Scenarios, with the `drawings` section); ExpertScenarioStore with list cache, read, atomic save keeping unknown
   fields, first save under a lock, delete, file-name rules; stale *.expert.json comments
   corrected. Integration tests on a temporary copy: all 13 read; read then save keeps every
   file equal as JSON (legacy included); a new id takes the next free number; test-runs/ is
   never touched; a broken file is listed with its error.
2  Domain/Scenarios: id rule, levels, point names, snap rules, form checks (SC14, SC15), with unit tests
   on small candle sets; the 13 files checked (expected: no problem besides those EWTS knows of).
3  Application and Api: feature Scenarios (queries, commands, shapes service), endpoints with
   the snap route, SC6 asset and history state.
   Unit tests of the handlers with in-memory ports; endpoint tests against the fixtures.
4  Chart module, overlay shapes: the six types, groups, roles to colours, detail on pointing,
   setOverlays; Vitest; browser check that draws all 13 fixtures' shapes.
5  Chart module, named points: handles, point tools ("1-2", "i-v", "a-c", trade tools), drag,
   Delete, events; Vitest; browser check.
6  Web, scenario chart and list: ScenarioChart, list with filter and sort, previous and next,
   the three levels with their visibility, log scale, reload and the 30-second check, menu entry
   (hidden when the feature is off), missing asset or history (SC6). Opening and stepping
   through scenarios writes nothing.
7  Web, the editor: new scenario (SC9), point tools by level and verdict, snapping through the
   Api, panel, autosave with the queue, leave warning before the first save, problems at their
   fields, undo and redo, delete with confirmation. Browser check: edit a fixture copy, the
   legacy section survives; create one, it gets X<n> when wave 2 end is placed.
8  Web, free drawings (SC20, SC21): the tool rail of the chart page on every level, drawings saved
   into the file with their level, shown on their level and the lower ones, undo and redo shared
   with the points. Browser check: draw on all three levels, reload, everything is back; a
   drawing of the trigger is not shown on the context.
9  Finish: NF1 for opening a scenario, Docker with SCENARIOS_PATH on WSL (the shared folder),
   README, runbook, architecture, roadmap; the owner's look. Pi only with the owner's go-ahead.
```

---

# Result of Step 1 (2026-11-20)

- The 13 converted files are in `tests/fixtures/scenarios/expert/` (copied from the shared folder
  on WSL); tests copy them into a temporary folder and never read the shared one.
- Contracts/Scenarios: `ExpertScenarioRecord` with the sections waves, wave1, wave2, trade and
  drawings (`ScenarioDrawingRecord`: the drawing record without the asset, plus its level), and
  `ExpertScenarioSummary` for the list. Port `IExpertScenarioStore` in Abstractions/Ewts with
  `ExpertScenarioRead` and `ExpertScenarioSave`.
- Domain/Scenarios: `ExpertScenarioId` (the id rule, the strict check, the next free number),
  brought forward from step 2 because the store needs it.
- Infrastructure/Scenarios: `ExpertScenarioJson` lays the known fields over the JSON of the file
  (proposal 4); a known field whose value is unchanged keeps its text, so saving an unchanged
  converted file changes no line: only `createdAt` and `updatedAt` are added at its end
  (proposal 5). Times are written in UTC with `Z`, text is not escaped. `ExpertScenarioStore`
  writes `<id>.json.part` and renames it, all writes under one lock; the first save renames
  without overwriting, so a file made meanwhile is never replaced and the next number is taken.
  A file it cannot read is listed with its error and never overwritten. Heads are cached by size
  and modified time.
- The comments about `*.expert.json` in Options, StorageLocations and StorageInitializer are
  corrected.
- Tests: 23 integration tests of the store (all 13 read and saved without a change of a line,
  unknown fields kept at every depth and by position in the exits, removed known fields, next
  free number with 8 concurrent first saves, drawings, broken files, ids that leave the folder,
  test-runs/ unchanged after every test) and 16 unit tests of the id rule. 543 unit,
  258 integration, 19 architecture tests pass.

# Result of Step 2 (2026-11-20)

- Domain/Scenarios: `ScenarioLevel` and `ScenarioLevels` (the chain 1d/4h/1h and 4h/1h/15m, the
  level of a timeframe, what a level shows, SC21), `ScenarioPointName` and `ScenarioPoints`
  (level, snap kind and field of each point; long only: wave 1 start, ii, iv, b of a three and a,
  c of wave 2 are lows), `ScenarioSnap` (the containing candle, found by binary search; the snap
  of each kind), `ScenarioCodes`, `ExpertScenarioReading`, `ScenarioFormProblem` and
  `ScenarioFormRules` (SC15).
- Form rules decided while writing them: the internals of a wave lie after the point the wave
  starts at and before the close of the context candle of the point it ends at (they sit on
  smaller candles inside it); the entry is after the open of the candle of the end of wave 2;
  add and stop moves after the entry, exits not before it, both in time order; a point is inside
  the span when its candle opens within from..to; prices are finite and above 0, shares above 0
  and at most 100. A pattern of wave 1 with points of the other pattern reports each such point.
- The mapping from the file's record to the Domain view (`ExpertScenarioReadings`, feature
  Scenarios) is brought forward from step 3 so the 13 files could be checked: none of them has
  a form problem.
- Checking the drawings with `DrawingRules` stays in step 3, where they are mapped to Domain
  drawings.
- 62 new unit tests (605 unit, 258 integration, 19 architecture pass).

# Result of Step 3 (2026-11-20)

- Feature Scenarios: queries GetScenariosStatus, ListExpertScenarios, GetExpertScenario,
  SnapScenarioPoint, PreviewExpertScenario; commands SaveExpertScenario (first save without id,
  later saves with the id of the address) and DeleteExpertScenario. Services:
  `ExpertScenarioShapes` (proposal 6), `ScenarioPointEditor` (puts a snapped point into the
  scenario: a missing section is made with its level's timeframe, wave 1 gets its pattern from
  the first point, a moved point keeps its reason, a new exit gets the share left to 100 % and
  the kind of its tool), `ExpertScenarioResponses` (shapes, form problems, levels, asset state),
  `ExpertScenarioValidator` (refuses only what could not be written or read again: asset, context
  timeframe, drawings by the rules of their tool and their level; everything else is a form
  problem).
- Snapping reads the candle that contains the time through `ICandleSeriesReader` on the
  timeframe of the point's section, or of its level when the section has none (a file written
  by hand with other timeframes snaps on its own). No candle there: 422 `scenario.noCandle`.
- SC6: the answer says whether the asset is in the workspace and which history starts of the
  three levels lie after the start of the span. The page sends them, after the user agreed,
  through the existing history change of the asset; the feature never changes an asset itself.
- Contracts: `Contracts/Overlays` (the shape vocabulary, also for the later overlays), scenario
  responses and requests. `DrawingMapping` moved from the Drawings feature to
  `Abstractions/Drawings`, so both features use it (architecture chapter 4).
- Api: `/api/scenarios` (status), `/api/scenarios/expert` (list, first save), `/snap`,
  `/preview`, `/{id}` (get, put, delete); chapter 9 updated (`/api/scenarios` and `/preview` are
  new against the plan).
- A folder that is not writable answers saves and deletes with 409 `scenarios.readOnly`; without
  a folder every route but the status answers 404 `scenarios.off`.
- Tests: 12 endpoint tests (with candle files of known values on 4h, 1h and 15m: every snap kind
  on its level; opening writes nothing; legacy kept through the Api; a broken drawing refused,
  a form problem never), 5 unit tests of the shapes. 610 unit, 270 integration, 19 architecture
  tests pass.

# Result of Step 4 (2026-11-20)

- `primitives/overlays/`: `overlay.ts` (types; the group records of the Api parsed, ISO times to
  Unix seconds; a shape of an unknown type or with fields it cannot read is dropped, as the
  interface asks of a client), `roleColours.ts` (OV6, unknown role gray), `placement.ts` (pure:
  a shape in pixels and its hit test), `overlayLayer.ts` (one series primitive, attached before
  the drawing layer so the user's drawings stay on top; texts with a border in the chart's
  background).
- `ChartPane.setOverlays(groups, hiddenKeys)`; pointing at a shape shows the title of its group
  and its detail in the tooltip of the gap icons (now multi-line, 420 px wide); `stats()` counts
  the shapes drawn.
- Checked in the headless browser (`~/tools/browser/overlay-check.mjs`) on a dev instance with
  the frozen market data and a copy of the 13 files, over localhost and over the WSL address:
  every shape of all 13 scenarios drawn on its asset and context timeframe, a hidden group taken
  away, the detail shown when pointing at a label, an empty list removes everything. The history
  the scenarios needed was moved back through the SC6 answer of the Api and the asset's history
  change, as the page will do it.
- Seen in the screenshots: on the whole span of a long scenario (a year of daily candles) the
  labels of wave 2 lie on top of the label 1; the topmost shape wins the pointer. Zoomed in they
  are apart. Entry and add one hour apart sit on one daily candle; on the trigger timeframe they
  are apart.
- 10 new Vitest tests (229 in all); C# unchanged (610 unit, 270 integration, 19 architecture).

# Proposals

1. **The Api snaps, not the browser** (accepted 2026-11-20; architecture chapters 8 and 10
   already say so). The module reports where the point was let go, the Api snaps it on its
   level's timeframe from the candle store (`ScenarioSnap`, Domain).
2. **Opening never moves a point.** Snapping applies only to a point the user places or moves.
   A converted point that is not on its candle's extreme stays as written; the form checks do not
   report it (rule checks are EWTS's).
3. (Dropped: a point tool is offered only on its own level's timeframe, decision 0012.)
4. **Unknown fields** are kept at every depth; the items of `exits` and `stopMoves` keep theirs by
   position (a removed exit loses its own unknown fields).
5. **createdAt and updatedAt** (asked of EWTS in for_the_neighbours.md, not answered yet): optional
   in a file TradeViewer has not saved; the first save sets `createdAt` (when missing) and every
   save sets `updatedAt`. A converted file therefore gets `createdAt` at its first change.
6. **What is drawn from the points** (roles of OV6):
   - waves: a strong line wave 1 start -> wave 1 end -> wave 2 end (role `wave`) with the labels
     0, 1 and 2;
   - wave 1 (EWTS added the section on 2026-11-20): the line wave 1 start -> i -> ii -> iii -> iv
     -> v (or wave 1 end), or -> a -> b -> c, with the labels i-v or a-c (role `lowerDegree`);
   - wave 2: the line wave 1 end -> a -> b -> c (or wave 2 end) with the labels a, b, c (role
     `lowerDegree`; lower case like wave 1, one degree below the waves);
   - trade: markers entry and add (`entry`, `add`); the stop as a level from the entry to the
     first stop move, each stop move as the next step of that level (`stop`); a marker per exit
     with its share (`target` or `stop` by its kind).
   Every shape carries the point's reason as its detail text.
7. **A new scenario is a draft** (`draft: true`) until the owner removes the mark.
8. **The list shows unreadable files** with their error and no actions besides delete.
9. **Changing the pattern of wave 1** between a five and a three removes the points of the
   other pattern after a confirmation (EWTS treats a file with both as invalid); undo brings
   them back.
10. **Undo and redo** per scenario as on the chart page (Ctrl+Z, Ctrl+Y), for points and fields.

---

# Answers of the Owner (2026-11-20)

- The Api snaps the points (proposal 1).
- Timeframes: a scenario has the levels context, setup and trigger. The owner starts on the
  context with Elliott waves and Fibonacci, goes to the setup and then to the trigger, where the
  nested waves, entry, add and stop are placed and notes are written on the chart. What is drawn
  on a level is shown on the lower levels and can be hidden there, never on the higher ones.
  The three timeframes are set when the scenario is created (decision 0012, SC11, SC21).
- The drawing tools of the chart page (Elliott patterns, also 1-2-3-4-5 for a nested count on the
  setup, Fibonacci and the others) are offered on every level and saved in the file's `drawings`
  section; the editor adds the icons for the named points of EWTS's expert scenario (SC10, SC20).
- Icons by verdict: wave tools always, trade tools for trade-win and trade-loss; a no-trade is
  mainly the reason why there is no trade (SC10, SC12).
- Exit price: taken where the exit is placed, corrected in the panel (SC10, SC12).

Open: EWTS's agreement to the `drawings` section and to `createdAt`/`updatedAt` optional
(for_the_neighbours.md). Step 1 can start without it; the section is written from step 8.
