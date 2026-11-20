import { distanceToSegment, insideRectangle, type Point } from "../../tools/geometry";
import type { OverlayShape } from "./overlay";

/** Where shapes go on the pane: x of a time (on the candle that contains it) and y of a price, in pixels. */
export interface Projector {
  x(time: number): number | undefined;
  y(price: number): number | undefined;
  readonly width: number;
  readonly height: number;
}

/** A shape in pixels, ready to be drawn and hit. */
export type Placed =
  | { readonly kind: "polyline"; readonly points: readonly Point[] }
  | { readonly kind: "label"; readonly at: Point; readonly text: Point; readonly side: "above" | "below" }
  | { readonly kind: "level"; readonly fromX: number; readonly toX: number; readonly y: number }
  | { readonly kind: "zone"; readonly fromX: number; readonly toX: number; readonly top: number; readonly bottom: number }
  | { readonly kind: "marker"; readonly at: Point }
  | { readonly kind: "vertical"; readonly x: number };

/** How far a label's text sits from its point, and how near the pointer must come to hit a shape, in pixels. */
export const LABEL_OFFSET = 12;
export const MARKER_RADIUS = 5;
export const HIT_TOLERANCE = 5;

/** The shape in pixels; undefined when one of its points cannot be placed (no candles, a price off the scale). */
export function place(shape: OverlayShape, projector: Projector): Placed | undefined {
  switch (shape.type) {
    case "line": {
      const points: Point[] = [];
      for (const point of shape.points ?? []) {
        const at = pointOf(point.time, point.price, projector);
        if (at === undefined) {
          return undefined;
        }

        points.push(at);
      }

      return points.length >= 2 ? { kind: "polyline", points } : undefined;
    }

    case "label": {
      const at = pointOf(shape.time, shape.price, projector);
      const side = shape.side ?? "above";
      return at === undefined ? undefined : { kind: "label", at, side, text: { x: at.x, y: at.y + (side === "above" ? -LABEL_OFFSET : LABEL_OFFSET) } };
    }

    case "marker": {
      const at = pointOf(shape.time, shape.price, projector);
      return at === undefined ? undefined : { kind: "marker", at };
    }

    case "level": {
      const y = shape.price === undefined ? undefined : projector.y(shape.price);
      const span = spanOf(shape, projector);
      return y === undefined || span === undefined ? undefined : { kind: "level", fromX: span.fromX, toX: span.toX, y };
    }

    case "zone": {
      const upper = shape.upper === undefined ? undefined : projector.y(shape.upper);
      const lower = shape.lower === undefined ? undefined : projector.y(shape.lower);
      const span = spanOf(shape, projector);
      if (upper === undefined || lower === undefined || span === undefined) {
        return undefined;
      }

      return { kind: "zone", fromX: span.fromX, toX: span.toX, top: Math.min(upper, lower), bottom: Math.max(upper, lower) };
    }

    case "verticalLine": {
      const x = shape.time === undefined ? undefined : projector.x(shape.time);
      return x === undefined ? undefined : { kind: "vertical", x };
    }

    default:
      return undefined;
  }
}

/** Whether the pointer is on the placed shape: near a line, on a label's text or point, inside a zone. */
export function hits(placed: Placed, pointer: Point): boolean {
  switch (placed.kind) {
    case "polyline":
      for (let i = 1; i < placed.points.length; i++) {
        if (distanceToSegment(pointer, placed.points[i - 1]!, placed.points[i]!) <= HIT_TOLERANCE) {
          return true;
        }
      }

      return false;
    case "label":
      return near(pointer, placed.text, LABEL_OFFSET) || near(pointer, placed.at, HIT_TOLERANCE);
    case "marker":
      return near(pointer, placed.at, MARKER_RADIUS + HIT_TOLERANCE);
    case "level":
      return Math.abs(pointer.y - placed.y) <= HIT_TOLERANCE && pointer.x >= placed.fromX - HIT_TOLERANCE && pointer.x <= placed.toX + HIT_TOLERANCE;
    case "zone":
      return insideRectangle(pointer, { x: placed.fromX, y: placed.top }, { x: placed.toX, y: placed.bottom });
    case "vertical":
      return Math.abs(pointer.x - placed.x) <= HIT_TOLERANCE;
  }
}

function pointOf(time: number | undefined, price: number | undefined, projector: Projector): Point | undefined {
  if (time === undefined || price === undefined) {
    return undefined;
  }

  const x = projector.x(time);
  const y = projector.y(price);
  return x === undefined || y === undefined ? undefined : { x, y };
}

/** From the candle of `from` to the candle of `to`, or to the right edge when it is open. */
function spanOf(shape: OverlayShape, projector: Projector): { fromX: number; toX: number } | undefined {
  const fromX = shape.from === undefined ? undefined : projector.x(shape.from);
  const toX = shape.to === undefined ? projector.width : projector.x(shape.to);
  return fromX === undefined || toX === undefined ? undefined : { fromX: Math.min(fromX, toX), toX: Math.max(fromX, toX) };
}

function near(pointer: Point, at: Point, reach: number): boolean {
  return Math.abs(pointer.x - at.x) <= reach && Math.abs(pointer.y - at.y) <= reach;
}
