// Plane geometry in pixels for drawing and hit testing. Pure functions, tested with Vitest.

export interface Point {
  readonly x: number;
  readonly y: number;
}

/** How close the pointer must come to a line to hit it, in pixels (as TradingView, roughly). */
export const HIT_TOLERANCE = 6;

/** The distance of a point from the segment between a and b. */
export function distanceToSegment(point: Point, a: Point, b: Point): number {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const lengthSquared = dx * dx + dy * dy;

  // Where the perpendicular from the point meets the line, clamped to the segment (0 = a, 1 = b).
  const along = lengthSquared === 0 ? 0 : Math.min(Math.max(((point.x - a.x) * dx + (point.y - a.y) * dy) / lengthSquared, 0), 1);

  return Math.hypot(point.x - (a.x + along * dx), point.y - (a.y + along * dy));
}

/**
 * Where the ray from a through b leaves the box from (0, 0) to (width, height): the far end to draw a ray to.
 * b itself when a and b are the same point or b lies outside the box already.
 */
export function rayEnd(a: Point, b: Point, width: number, height: number): Point {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  if (dx === 0 && dy === 0) {
    return b;
  }

  // The largest t >= 1 at which a + t (b - a) is still on the box, per axis.
  const limit = (start: number, delta: number, size: number) =>
    delta > 0 ? (size - start) / delta : delta < 0 ? -start / delta : Number.POSITIVE_INFINITY;
  const t = Math.min(limit(a.x, dx, width), limit(a.y, dy, height));

  return t > 1 ? { x: a.x + t * dx, y: a.y + t * dy } : b;
}

/** True when the point lies inside the rectangle with the opposite corners a and b, edges included. */
export function insideRectangle(point: Point, a: Point, b: Point): boolean {
  return (
    point.x >= Math.min(a.x, b.x) && point.x <= Math.max(a.x, b.x) && point.y >= Math.min(a.y, b.y) && point.y <= Math.max(a.y, b.y)
  );
}

/** True when the point lies inside the polygon (even-odd rule). */
export function insidePolygon(point: Point, corners: readonly Point[]): boolean {
  let inside = false;
  for (let index = 0, previous = corners.length - 1; index < corners.length; previous = index++) {
    const a = corners[index]!;
    const b = corners[previous]!;
    if (a.y > point.y !== b.y > point.y && point.x < ((b.x - a.x) * (point.y - a.y)) / (b.y - a.y) + a.x) {
      inside = !inside;
    }
  }

  return inside;
}

/** The height of the line through a and b at x; undefined for a vertical line. */
export function yOnLine(a: Point, b: Point, x: number): number | undefined {
  return a.x === b.x ? undefined : a.y + ((x - a.x) * (b.y - a.y)) / (b.x - a.x);
}

/** True when the point lies within the tolerance of the segment between a and b. */
export function hitsSegment(point: Point, a: Point, b: Point, tolerance: number = HIT_TOLERANCE): boolean {
  return distanceToSegment(point, a, b) <= tolerance;
}
