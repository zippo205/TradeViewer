# 0011 - Two Scenario Pages: Editor and Test-Run Viewer

Status: accepted (2026-11-20, owner, agreed with EWTS). Refines 0010.

## Context

Decision 0010 left the expert scenarios (written by TradeViewer) and the test-run scenarios
(written by EWTS) in one scenario page with one list. The owner and EWTS then agreed how each is
used:

- Expert scenarios are made in the editor during the development of EWTS, copied by hand to the
  development machine and used to adjust EWTS's code, as the YAML files were. Existing ones are
  loaded and adjusted.
- Test-run scenarios are shown as the `.pine` files were, mostly while EWTS runs on the Pi; on the
  development machine the agent of EWTS compares them with the expert scenarios.
- A test-run case worth keeping is written as a new expert scenario in the normal editor; there is
  no link from a test-run scenario to an expert scenario.

## Decision

1. Two pages: the **Scenario Editor** for the expert scenarios (`expert/`) and the **Test-Run
   Viewer** for the scenarios of a test run (`test-runs/<run id>/`).
2. The editor opens every expert scenario ready to edit; it has no read-only mode. The test-run
   viewer is read-only.
3. Both use one scenario chart (asset, time span, groups, log scale, panel, list at the right,
   previous and next) and the same shape primitives; an expert scenario is turned into shapes and
   drawn like a test-run scenario.
4. No "new expert scenario from a test-run scenario". Comparing an expert scenario with a test-run
   scenario stays with EWTS.
5. The editor is built first; the test-run viewer follows when EWTS writes test-run files.

## Reasons

- Different writers, actions and lists: the expert scenarios are edited across all assets, the
  scenarios of a run are only read, one run at a time. One mixed list would place side by side
  what nobody compares there.
- On one page the user would have to check the kind of a scenario before knowing whether it can be
  edited.
- The two are needed at different times: the editor now, the viewer with EWTS's Scenarios epic.

## Rejected

- One scenario page for both kinds (0010 as written).
- Taking asset and time span of a test-run scenario over into a new expert scenario: not needed;
  the editor's "new" is as quick.
