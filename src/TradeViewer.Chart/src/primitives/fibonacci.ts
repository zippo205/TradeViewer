import type { Point } from "../tools/geometry";

/** One level of a Fibonacci drawing: its ratio and its height in pixels. */
export interface FibonacciLevel {
  readonly ratio: number;
  readonly y: number;
}

/**
 * The levels of a Fibonacci drawing in pixels (DR4), interpolated between the heights of its anchors:
 *
 * - retracement (2 anchors): level 0 at the second anchor, level 1 at the first, as in TradingView;
 * - extension (3 anchors: start, end, retracement): the move from start to end, times the ratio, laid from the
 *   third anchor (TradingView's trend-based extension).
 *
 * The interpolation is linear in pixels. On a linear scale pixels are linear in price, so the levels are linear in
 * price; on a logarithmic scale pixels are linear in the logarithm of the price, so the levels are calculated in
 * log space, as DR4 asks. Undefined while the drawing lacks anchors (the preview of placing).
 */
export function fibonacciLevels(points: readonly Point[], extension: boolean, ratios: readonly number[]): FibonacciLevel[] | undefined {
  const [a, b, c] = points;
  if (a === undefined || b === undefined || (extension && c === undefined)) {
    return undefined;
  }

  return ratios.map((ratio) => ({
    ratio,
    y: extension ? c!.y + (b.y - a.y) * ratio : b.y + (a.y - b.y) * ratio,
  }));
}

/** The ratio as the label shows it: 0.618, 1, 1.272. */
export function ratioText(ratio: number): string {
  return String(Number(ratio.toFixed(4)));
}

/** The levels of a drawing record: the finite numbers of its `levels`, or none. */
export function levelsOf(value: unknown): number[] {
  return Array.isArray(value) ? value.filter((level): level is number => typeof level === "number" && Number.isFinite(level)) : [];
}
