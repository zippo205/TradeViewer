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

/**
 * The ratios of the levels of a drawing record that are switched on (DR4). A level is `{ ratio, visible }`; a plain
 * number, as levels were stored before they could be switched off, is a level that is shown.
 */
export function levelsOf(value: unknown): number[] {
  if (!Array.isArray(value)) {
    return [];
  }

  const ratios: number[] = [];
  for (const level of value as unknown[]) {
    if (typeof level === "number" && Number.isFinite(level)) {
      ratios.push(level);
    } else if (typeof level === "object" && level !== null) {
      const { ratio, visible } = level as { ratio?: unknown; visible?: unknown };
      if (typeof ratio === "number" && Number.isFinite(ratio) && visible !== false) {
        ratios.push(ratio);
      }
    }
  }

  return ratios;
}

/** Where the levels of a drawing begin and end: from its first to its last anchor, left to right (DR4). */
export function levelSpan(points: readonly { x: number }[]): { readonly left: number; readonly right: number } {
  const xs = points.map((point) => point.x);
  return { left: Math.min(...xs), right: Math.max(...xs) };
}
