# 0010 - Expert Scenarios Are Made in TradeViewer

Status: accepted (2026-11-20, owner, agreed with EWTS: ../EWTS/docs/decisions/
0006-expert-scenarios-from-tradeviewer.md). Replaces 0007; amends 0006, points 1 and 2.

## Context

Decision 0007 let the user draw an own analysis on any scenario (`<id>.expert.json` beside it)
and create an expert scenario as a file of drawings, which EWTS was to merge into its scenarios.
A file of shapes does not say which point is the end of wave 1 or the entry, so EWTS could not
test against it. Two questions stayed open: deleting a test run would delete the user's analysis
beside its scenarios, and it was unclear whether a new expert file replaces the hand-written YAML
of EWTS. EWTS has decided the scenarios anew with the owner (its decision 0006).

## Decision

1. **Two kinds of scenario, each with one writer.** Expert scenarios are made by the owner in
   TradeViewer's scenario editor and saved as `expert/<id>.json` in the scenario folder; they
   replace EWTS's YAML files. Test-run scenarios are written by EWTS into
   `test-runs/<run id>/` (drawing file `<id>.json`, Pine script `<id>.pine`).
2. **No analysis of the user on top of a scenario.** There is no `<id>.expert.json` and nothing
   EWTS merges. A test-run case worth keeping becomes a new expert scenario.
3. **EWTS defines the expert format**: named points (waves, internals of wave 2, trade), not
   shapes (../EWTS/docs/architecture.md, chapter 7, "Scenario Files"). TradeViewer builds the
   editor for it and draws expert scenarios itself from the points. EWTS writes no drawing file
   for an expert scenario.
4. **The editor reopens a scenario from its named points.** The file holds no drawing records of
   TradeViewer; the picture is rebuilt from the points every time it is opened. Fields
   TradeViewer does not know (`legacy` of the converted files, later additions) are kept
   unchanged when it saves.
5. **TradeViewer writes only in `expert/`**, atomically (`<name>.part`, then rename), and never
   writes into `test-runs/`. EWTS never writes or deletes anything in `expert/`; deleting a
   run removes its folder, which now holds only EWTS's files.
6. **No analysis in TradeViewer.** The editor snaps points by the rules of EWTS's format and
   makes form checks (order of the times, required fields by verdict, shares of the exits). Rule
   checks (is it a real extreme, does the entry follow the strategy) are EWTS's.

## Reasons

- Named points are what an acceptance test needs; drawing records would be a second copy of the
  same reading that can disagree with the points. One source of truth also means a file EWTS
  converted or changed opens in the editor exactly as written.
- The picture of an expert scenario is simple (lines through the wave points, labels, the levels
  of the trade) and needs no analysis knowledge, only the names of the points.
- One writer per folder removes the merge and the danger that deleting a run loses work of the
  user (priority 1).

## Rejected

- Storing TradeViewer's drawing records in the file (the optional field EWTS offered): two
  sources of truth, and a converted file would open without a picture.
- Keeping the user's analysis on test-run scenarios (0007): two files per scenario and a merge
  for comparisons the owner does not need.

## Consequences

- Specification 4.12 (SC1-SC17), architecture chapter 8 "Scenario Files", the API,
  docs/knowledge/ewts-interface.md ("Scenario Files", "Expert Scenario Files") and the backlog
  epics Scenario Viewer and Scenario Editor (formerly Scenario Analysis) are rewritten on this
  basis.
- The Scenario Viewer no longer waits for EWTS for expert scenarios: it can be built on the 22
  converted expert scenarios EWTS provides; test-run scenarios follow with EWTS's Scenarios epic.
- The editor needs point tools with snapping besides the free drawing tools of 4.6.
