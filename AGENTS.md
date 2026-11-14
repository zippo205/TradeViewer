# AGENTS.md

## Mission

Build and maintain TradeViewer: the website that shows charts with the user's drawings and
EWTS's analysis, and operates the Downloader and EWTS.

TradeViewer is a viewer and an operating console. It never analyses and never places orders.

## Priorities

1. User-created analysis is never lost.
2. Chart interactions feel immediate.
3. The code stays readable for a .NET developer: C# everywhere, TypeScript only in
   src/TradeViewer.Chart.

## Required Reading Order

1. docs/specification.md
2. docs/architecture.md
3. docs/backlog.md (the epic of the task)
4. docs/decisions/ (the decisions relevant to the task)
5. docs/knowledge/ (only the interface the task uses)

```text
Assets, market data, Downloader monitoring     -> docs/knowledge/downloader-interface.md
EWTS instances, overlays, test runs, alerts,
scenario viewer                                -> docs/knowledge/ewts-interface.md
```

Commands and setup are in README.md.

## Where Information Lives

Every piece of information has exactly one home. Link to it; never repeat it.

```text
What the product does, requirement ids        docs/specification.md
How it is built, configuration keys           docs/architecture.md
Why a choice was made, rejected options       docs/decisions/
Epics, their order and dependencies           docs/backlog.md
Releases and status                           docs/roadmap.md
Steps of the epic in work                     docs/plans/<epic-name>.md
Interfaces of the Downloader and EWTS         docs/knowledge/
Setup and commands                            README.md
Rules for agents                              AGENTS.md
```

A change that affects behaviour updates the specification first, then the architecture, then
the code. A new decision gets a new record in docs/decisions/; accepted records are not
rewritten, they are replaced by a later one.

## Git Workflow

- Every epic starts on its own branch, created from an up-to-date `main` before any code is
  written. The branch name is given in the backlog (`feature/<epic-name>`).
- The epic's implementation plan lives in docs/plans/<epic-name>.md and is updated as steps
  are completed.
- An epic is finished only when build, tests and chart tests all pass.
- The branch is merged into `main` when the epic is completed and approved.
- Documentation changes outside an epic may go directly to `main`.

## Working Rules

- The solution does not exist until the Solution Foundation epic is done; until then the
  documents describe the target, not existing code.
- ../TradeViewerMVP is an earlier implementation with a different scope. Consult it to see how
  something was solved, but write new code from the current documents. Never copy files from
  it.
- ../relatedProjects holds foreign projects for learning only. OpenCharts is the reference for
  drawing tools on Lightweight Charts. Consult them; never copy code (licences differ, PineTS is
  AGPL).
- ../Downloader and ../EWTS own their interfaces. Never change those repositories from here;
  a needed change is written into docs/knowledge/ and raised with the owner.
- The Downloader's output root (`MarketData:RootPath`) is read-only. Never write there.
- In the scenario folder (`Scenarios:RootPath`) TradeViewer writes only `*.expert.json` files
  (decision 0007), and only for a scenario the user has changed. Never change a file of EWTS.
- Tests never contact the Downloader or an EWTS instance.

## Authority Order

When documents conflict:

1. AGENTS.md
2. docs/specification.md
3. docs/architecture.md
4. docs/decisions/
5. docs/backlog.md
6. docs/roadmap.md
7. docs/knowledge/

Architecture has priority over personal coding preferences. A conflict between documents is a
defect: report it and fix the document that is wrong.
