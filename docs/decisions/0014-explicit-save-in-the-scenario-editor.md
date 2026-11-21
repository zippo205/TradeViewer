# 0014 - Explicit Save in the Scenario Editor

Status: accepted by the owner (2026-11-21). Replaces the automatic saving of the scenario editor
(plan of the Scenario Editor, step 7; SC13 as first written).

## Context

The editor first saved every change at once, like the drawings of the chart page. Since decision
0013 an expert scenario is checked before it is saved: the nested waves must fit their context
points, and a scenario whose levels do not fit must not be written. A change in the middle of the
work (a start dragged, a context point moved) often leaves the levels unfit for a moment.

## Decision

The user saves with a Save button beside Delete (SC13). It is always visible and enabled only
while there is an unsaved change that can be written: the first save needs wave 2 end (SC14), and
the levels must fit (SC15). The panel shows that there are unsaved changes; leaving the page or
opening another scenario with unsaved changes asks first. The server refuses a save whose levels
do not fit as well.

## Reasons

- A scenario needs validation before it is written, so it cannot be saved automatically: an
  automatic save would either write a file that does not pass the check, or silently not save.
- The user decides when a reading is complete enough to keep, and sees why Save is disabled.

## Rejected

- Saving automatically and only skipping the saves that fail the check: the user would not know
  which state is in the file.
- Saving automatically and showing the misfit afterwards: a file that does not fit would reach
  EWTS's acceptance tests.

## Consequences

- Specification SC13 and SC14 (done when the button was built).
- Unsaved changes live only in the browser until Save; closing the window asks first.
