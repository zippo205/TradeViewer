import type { Anchor } from "../primitives/drawing";
import type { Point } from "./geometry";

/** Where an anchor was when dragging began: its whole bar index and its height in pixels. */
export interface AnchorStart {
  readonly index: number;
  readonly y: number;
}

/** What the pane converts for a drag: index to time (time mapping) and pixel height to price (scale). */
export interface DragScale {
  timeOf(index: number): number | undefined;
  priceOf(y: number): number | undefined;
}

/**
 * The anchors of a drawing moved by the pointer: in whole bars along the time axis (an anchor always
 * sits on a candle) and in pixels up and down, converted back to prices so that the move is right on a
 * logarithmic scale too. Undefined when a point cannot be converted (no candles, no scale).
 *
 * @param bars how many bars the pointer has moved since dragging began, a whole number
 * @param dy how many pixels the pointer has moved down since dragging began
 */
export function movedAnchors(starts: readonly AnchorStart[], bars: number, dy: number, scale: DragScale): Anchor[] | undefined {
  const moved: Anchor[] = [];
  for (const start of starts) {
    const time = scale.timeOf(start.index + bars);
    const price = scale.priceOf(start.y + dy);
    if (time === undefined || price === undefined || !Number.isFinite(price)) {
      return undefined;
    }

    moved.push({ time, price });
  }

  return moved;
}

/** The whole number of bars between two logical positions of the pointer (the library's fractional index). */
export function wholeBarsBetween(fromLogical: number, toLogical: number): number {
  return Math.round(toLogical - fromLogical);
}

/** A pointer that moves less than this many pixels between press and release has not dragged. */
export const DRAG_THRESHOLD = 3;

export function hasDragged(from: Point, to: Point): boolean {
  return Math.abs(to.x - from.x) >= DRAG_THRESHOLD || Math.abs(to.y - from.y) >= DRAG_THRESHOLD;
}
