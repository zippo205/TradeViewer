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

/** True when the point lies within the tolerance of the segment between a and b. */
export function hitsSegment(point: Point, a: Point, b: Point, tolerance: number = HIT_TOLERANCE): boolean {
  return distanceToSegment(point, a, b) <= tolerance;
}
