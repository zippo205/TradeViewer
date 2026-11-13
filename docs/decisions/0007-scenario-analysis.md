# 0007 - Analysis of the User on Scenarios

Status: accepted (2026-11-13, owner). Amends 0006: points 1, 3 and 4 no longer hold as written.

## Context

Decision 0006 made the scenario viewer read-only. Three kinds of scenario exist: expert
scenarios the owner writes by hand to teach EWTS, analysis cases EWTS generates and the owner
annotates in the YAML, and scenarios of test runs. The owner wants to compare the analysis of
EWTS with an own analysis in the same view, hand the own analysis back to EWTS, and create
expert scenarios the same way. The drawing tools of TradeViewer already produce such analysis.

## Decision

1. Analysis cases and scenarios of test runs are one kind: **generated** scenarios. The other
   kind is **expert**.
2. On any scenario the user draws an own analysis, sets a verdict and writes remarks. They are
   saved as a file `<id>.expert.json` next to the scenario `<id>.json`: the id in the file
   name is the reference. The file has the format of a scenario file (docs/knowledge/ewts-api.md,
   Scenario Files).
3. A new expert scenario is created in the same view and saved as `expert/<id>.expert.json`,
   a file without a `<id>.json` beside it.
4. TradeViewer is the only writer of `*.expert.json` files and never changes a file written
   by EWTS. EWTS reads the expert files and merges them into the expert analysis section of its
   scenario.
5. The scenario folder (`Scenarios:RootPath`) is mounted writable for TradeViewer.

## Rejected

- Writing the analysis into EWTS's scenario file: two writers on one file, and a regeneration
  by EWTS would overwrite the work of the user.
- Sending the analysis through an EWTS API: every save would need a running instance and a
  new API call before anything works.

## Consequences

- The viewer (SC1-SC9) comes first and stays read-only; the analysis (SC10-SC13) is a later
  epic that reuses the drawing tools.
- The scenario folder is not part of the workspace backup (WS3); it lives with the EWTS
  repository or its mounted folder, where git or the host keeps it.
- EWTS has to read `*.expert.json` and ignore none of them (architecture, open point O1).
