# 0012 - Timeframe Levels and Drawings in Expert Scenarios

Status: accepted by the owner (2026-11-20), confirmed by EWTS (2026-11-21). Amends 0010, points 3
and 4.

## Context

Decision 0010 made the expert file hold only the named points (waves, internals of wave 2, trade),
with each section naming its own timeframe, and the editor offered one tool per named point. While
reviewing the plan of the Scenario Editor the owner described how he works: he starts on the
context timeframe and draws Elliott waves and Fibonacci there, goes down to the setup timeframe
and draws there with the context still visible, and places the nested waves, the entry, the add
and the stop on the trigger timeframe, with notes on the chart. These drawings are part of his
reading of the scenario, so they must be kept (priority 1), but they are no named points.

## Decision

1. **Three timeframe levels.** A new expert scenario gets three timeframes from the chain of its
   context timeframe, as EWTS uses it: context `1d` -> setup `4h` -> trigger `1h`, context `4h`
   -> setup `1h` -> trigger `15m`. The editor offers only these three timeframes.
2. **The named points belong to a level:** the waves (wave 1 start, wave 1 end, wave 2 end) to
   the context, the internals of wave 2 to the setup, the trade to the trigger. Their tools are
   offered only on the timeframe of their level, so every point is placed on a candle of its own
   timeframe. The sections keep naming their timeframe in the file, written from the level. A file
   whose sections name other timeframes (written by hand) opens unchanged; its points are drawn
   on the level whose timeframe matches, otherwise on the context.
3. **Tools by verdict.** The wave tools are always offered: a "1-2" tool that places wave 1 start,
   wave 1 end and wave 2 end with three clicks, and an "A-B-C" tool for the internals of wave 2.
   The trade tools (entry, stop, add, stop move, exit at target, exit at stop) are offered for
   `trade-win` and `trade-loss` only. A `no-trade` is the waves, the reason and the time span.
4. **Free drawings in the file.** On every level the user has the drawing tools of the chart
   page (4.6: lines, Fibonacci, Elliott patterns such as 1-2-3-4-5 for a nested count, wave labels,
   text notes). They are saved in the expert file, in a section `drawings` that belongs to
   TradeViewer, each drawing with its level, in the record the workspace uses for drawings
   without the asset. EWTS ignores the section; the file stays the only record of the scenario.
5. **Visibility follows the levels.** What belongs to a level (its points and drawings) is shown
   on its own timeframe and on the lower levels, never on a higher one. Each level can be hidden;
   the choice stays while stepping through scenarios.

## Reasons

- The free drawings are the owner's reasoning around the points; losing them on reload would
  break priority 1, and keeping them beside the points in one file means a copy of the file
  carries everything.
- This is not what 0010 rejected: there the drawing records would have repeated the named points
  and could disagree with them. Here the points stay the only source of the reading EWTS tests;
  the drawings add what the points do not say.
- Placing a point only on its own timeframe removes the question which smaller candle is meant
  when the chart shows a larger one.
- The three levels match EWTS's chain and every converted file (1d / 4h / 1h, 4h / 1h).

## Rejected

- Free drawings in TradeViewer's workspace under the scenario id: they would not travel with a
  copy of the file, and deleting or renaming the file would orphan them.
- A timeframe chosen per section in the editor (SC11 as written): the chain fixes them, and a
  free choice adds the ambiguity of point 2.
- Showing the drawings of every level on every timeframe: the drawings of a lower level crowd the
  higher one.

## Consequences

- Specification 4.12: SC8, SC10, SC11, SC12 and SC15 changed; SC20 (free drawings) and SC21
  (levels shown and hidden) added.
- docs/knowledge/ewts-interface.md, "Expert Scenario Files": the section `drawings` and the level
  timeframes, agreed with EWTS.
- Architecture chapters 8 and 10: the store keeps the `drawings` section; the scenario page uses
  the drawing interaction of the chart page and saves into the file.
- The plan of the Scenario Editor gets a step for the drawings.
