# 0013 - Nested Waves on Their Own Timeframe

Status: accepted by the owner (2026-11-21); the fields `wave1.start` and `wave2.start` agreed with
EWTS on the same day (docs/knowledge/ewts-interface.md, "Expert Scenario Files"). Amends 0012,
points 2 and 3.

## Context

The owner draws the 1-2 on the context (1d) and then goes down to the setup (4h). The points of
the context are open times of 1d candles at the day's low or high, so on 4h they do not sit on the
4h candle that made the extreme. The internals of wave 1 and wave 2 had no start of their own in
the format: their line started at a context point, hung in the air on 4h, and without a 1-2 on
the context there was no first leg at all. Points could only be removed one by one with the
Delete key or from the panel.

## Decision

1. **A point of a higher level is shown on the candle of the timeframe shown** that made it:
   the candle whose open time lies inside the point's candle and whose low (or high) equals the
   point's price, the earliest on ties (the rule of EWTS). Going back up shows it on its own
   candle again. Only the view changes; the file keeps the point on its own timeframe. Where no
   candle meets the rule (a converted point that is no extreme), the candle with the extreme of
   that span is used for the view.
2. **The nested waves stand on their own.** Their tools place every point of the form on the
   setup, the start included (`wave1.start`, `wave2.start`), also without a 1-2 on the context.
   When the context point exists and the start is missing, TradeViewer derives the start by the
   rule when the first point of the form is placed, so it is not clicked twice.
3. **The levels must fit before a save.** `wave1.start`, `wave1.vEnd` / `wave1.cEnd` and
   `wave2.start` / `wave2.cEnd`, where present, are checked against their context point by the
   rule. A deviation, a missing context point or no candle that meets the rule (a gap in the data)
   is shown at the form and disables Save until the user corrects it. Every other form problem is
   shown but does not disable Save (SC15).
4. **A form is removed by clicking it.** Clicking a line or label of a form of the level shown
   selects the form, clicking a handle selects the point, clicking a marker of the trade selects
   that point; a bar above the chart offers Delete, the Delete key does the same.

## Reasons

- The point on the lower timeframe follows from the context point, so showing it there needs no
  new data, and checking it is a lookup of the same rule EWTS applies.
- Drawing a form independently, then checking that the levels fit, keeps the drawing free and the
  file consistent; EWTS reports the same deviation, so a file TradeViewer saves passes it.
- Blocking only the fit between the levels keeps unfinished scenarios saveable, as agreed with
  EWTS (incomplete files are reported as incomplete).

## Rejected

- Moving the context point when a nested start is drawn elsewhere: a drawing on one level would
  silently change another.
- Writing the refined positions of the higher levels into the file: the context points are the
  reading EWTS tests and stay on their own timeframe.
- Blocking Save on every form problem: an unfinished scenario could not be kept.

## Consequences

- Specification 4.12: SC8, SC10, SC13, SC15 changed.
- docs/knowledge/ewts-interface.md: `start` of `wave1` and `wave2` (done).
- The shapes are built for the timeframe shown (Api: the timeframe of the chart with get,
  preview and snap); a form problem says whether it blocks a save.
- Assets with exchange hours: the rule is applied as for crypto until EWTS decides their chain.
