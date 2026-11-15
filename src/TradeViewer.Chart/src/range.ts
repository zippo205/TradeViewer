import type { Bar } from "./data/bars";

/** A newly opened chart shows this many candles with this much empty space to the right (CH3). */
export const DEFAULT_VISIBLE_BARS = 150;
export const DEFAULT_RIGHT_GAP = 8;

/**
 * The visible part of a series in bar indices (0 is the oldest bar in memory). Both ends can lie
 * between two bars or beyond the last one: the space to the right of the latest candle.
 */
export interface LogicalRange {
  readonly from: number;
  readonly to: number;
}

/** What the user was looking at, in terms that survive a change of timeframe or asset (CH3). */
export interface ViewState {
  /** The number of candles that fit into the view. */
  readonly visibleBars: number;
  /** True: the latest candle was in view; the chart stays at the latest candles. */
  readonly atLatest: boolean;
  /** The empty space to the right of the latest candle, in bars (only when atLatest). */
  readonly rightGap: number;
  /** The time at the right edge of the view, in Unix seconds (only when scrolled back). */
  readonly anchorTime: number;
}

export function initialRange(barCount: number): LogicalRange {
  const to = barCount - 1 + DEFAULT_RIGHT_GAP;
  return { from: to - DEFAULT_VISIBLE_BARS, to };
}

/** The range that shows the latest candles: so many candles in view, with the usual space to the right (CH5). */
export function latestRange(visibleBars: number, barCount: number): LogicalRange {
  const to = barCount - 1 + DEFAULT_RIGHT_GAP;
  return { from: to - visibleBars, to };
}

/** Describes the view so that it can be set up again on other candles. */
export function captureView(range: LogicalRange, bars: readonly Bar[]): ViewState | undefined {
  if (bars.length === 0) {
    return undefined;
  }

  const lastIndex = bars.length - 1;
  const atLatest = range.to >= lastIndex;
  const edge = bars[Math.min(Math.max(Math.floor(range.to), 0), lastIndex)]!;

  return {
    visibleBars: range.to - range.from,
    atLatest,
    rightGap: atLatest ? range.to - lastIndex : 0,
    anchorTime: edge.time,
  };
}

/**
 * The range that shows the same on new candles: the same number of candles, at the latest ones when the
 * view was there or when the asset changed, otherwise with the same time at the right edge (CH3).
 */
export function restoreView(view: ViewState | undefined, bars: readonly Bar[], assetChanged: boolean): LogicalRange {
  if (view === undefined || bars.length === 0) {
    return initialRange(bars.length);
  }

  const lastIndex = bars.length - 1;
  if (assetChanged || view.atLatest) {
    const to = lastIndex + (view.atLatest ? view.rightGap : DEFAULT_RIGHT_GAP);
    return { from: to - view.visibleBars, to };
  }

  // Scrolled back: the candle that contains the time at the right edge is the right edge again.
  const to = Math.max(indexAtOrBefore(bars, view.anchorTime), 0);
  return { from: to - view.visibleBars, to };
}

/** Index of the last bar that opens at or before the time: the candle that contains it. -1 before the first bar. */
export function indexAtOrBefore(bars: readonly Bar[], time: number): number {
  let low = 0;
  let high = bars.length;
  while (low < high) {
    const middle = (low + high) >> 1;
    if (bars[middle]!.time <= time) {
      low = middle + 1;
    } else {
      high = middle;
    }
  }

  return low - 1;
}
