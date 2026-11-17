import { describe, expect, it } from "vitest";
import { HIT_TOLERANCE, distanceToSegment, hitsSegment, insidePolygon, insideRectangle, rayEnd, yOnLine } from "./geometry";

const a = { x: 100, y: 100 };
const b = { x: 300, y: 200 };

describe("distanceToSegment", () => {
  it("is zero on the segment and at its ends", () => {
    expect(distanceToSegment(a, a, b)).toBe(0);
    expect(distanceToSegment(b, a, b)).toBe(0);
    expect(distanceToSegment({ x: 200, y: 150 }, a, b)).toBeCloseTo(0, 10);
  });

  it("is the perpendicular distance beside the segment", () => {
    expect(distanceToSegment({ x: 200, y: 50 }, { x: 100, y: 100 }, { x: 300, y: 100 })).toBe(50);
  });

  it("is the distance to the nearer end beyond the segment", () => {
    expect(distanceToSegment({ x: 0, y: 100 }, a, b)).toBe(100);
    expect(distanceToSegment({ x: 303, y: 204 }, a, b)).toBe(5);
  });

  it("is the distance to the point for a segment of zero length", () => {
    expect(distanceToSegment({ x: 103, y: 104 }, a, a)).toBe(5);
  });
});

describe("hitsSegment", () => {
  it("hits within the tolerance and misses beyond it", () => {
    const flatA = { x: 0, y: 0 };
    const flatB = { x: 100, y: 0 };

    expect(hitsSegment({ x: 50, y: HIT_TOLERANCE }, flatA, flatB)).toBe(true);
    expect(hitsSegment({ x: 50, y: HIT_TOLERANCE + 0.5 }, flatA, flatB)).toBe(false);
    expect(hitsSegment({ x: 50, y: 10 }, flatA, flatB, 12)).toBe(true);
  });
});

describe("rayEnd", () => {
  it("continues the ray to the edge of the box", () => {
    expect(rayEnd({ x: 100, y: 300 }, { x: 200, y: 300 }, 1000, 600)).toEqual({ x: 1000, y: 300 });
    expect(rayEnd({ x: 100, y: 100 }, { x: 150, y: 50 }, 1000, 600)).toEqual({ x: 200, y: 0 });
  });

  it("keeps the second point for a ray of no length or one that starts outside", () => {
    expect(rayEnd({ x: 5, y: 5 }, { x: 5, y: 5 }, 1000, 600)).toEqual({ x: 5, y: 5 });
    expect(rayEnd({ x: 900, y: 10 }, { x: 1100, y: 10 }, 1000, 600)).toEqual({ x: 1100, y: 10 });
  });
});

describe("insideRectangle and insidePolygon", () => {
  it("knows inside and outside, whatever the order of the corners", () => {
    expect(insideRectangle({ x: 5, y: 5 }, { x: 10, y: 10 }, { x: 0, y: 0 })).toBe(true);
    expect(insideRectangle({ x: 11, y: 5 }, { x: 10, y: 10 }, { x: 0, y: 0 })).toBe(false);

    const slanted = [
      { x: 0, y: 0 },
      { x: 10, y: 5 },
      { x: 10, y: 15 },
      { x: 0, y: 10 },
    ];
    expect(insidePolygon({ x: 5, y: 7 }, slanted)).toBe(true);
    expect(insidePolygon({ x: 5, y: 1 }, slanted)).toBe(false);
  });
});

describe("yOnLine", () => {
  it("gives the height of the line at x, none for a vertical line", () => {
    expect(yOnLine({ x: 0, y: 0 }, { x: 10, y: 20 }, 5)).toBe(10);
    expect(yOnLine({ x: 3, y: 0 }, { x: 3, y: 20 }, 5)).toBeUndefined();
  });
});
