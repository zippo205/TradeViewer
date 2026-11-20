# Implementation Plan: Scenario Editor

## Status

```text
Epic:     Scenario Editor (P1 - Workstation)
Status:   Plan revised 2026-11-20 after the owner's answers (decision 0012); no step done
Branch:   feature/scenario-editor
Next:     the owner's review of the revised plan, then step 1
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
ScenarioPointName       wave1Start, wave1End, wave2End, aEnd, bEnd, cEnd, entry, add, stop,
                        stopMove, exit; with its level and, for a wave point, whether it is a low
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
                         place with a point tool ("1-2" and "A-B-C" place several in a row), move
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
5  Chart module, named points: handles, point tools ("1-2", "A-B-C", trade tools), drag,
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
   - wave 2: the line wave 1 end -> A -> B -> C (or wave 2 end) with the labels A, B, C (role
     `lowerDegree`);
   - trade: markers entry and add (`entry`, `add`); the stop as a level from the entry to the
     first stop move, each stop move as the next step of that level (`stop`); a marker per exit
     with its share (`target` or `stop` by its kind).
   Every shape carries the point's reason as its detail text.
7. **A new scenario is a draft** (`draft: true`) until the owner removes the mark.
8. **The list shows unreadable files** with their error and no actions besides delete.
9. **Undo and redo** per scenario as on the chart page (Ctrl+Z, Ctrl+Y), for points and fields.

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
