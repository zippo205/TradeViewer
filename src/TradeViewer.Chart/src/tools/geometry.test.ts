import { describe, expect, it } from "vitest";
import { HIT_TOLERANCE, distanceToSegment, hitsSegment } from "./geometry";

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
