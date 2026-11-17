import { describe, expect, it } from "vitest";
import { hasDragged, movedAnchors, wholeBarsBetween, type DragScale } from "./drag";

const HOUR = 3600;

/** 1h candles from time 0 and a linear scale of 10 price units per pixel, price 1000 at y = 0. */
const linear: DragScale = {
  timeOf: (index) => index * HOUR,
  priceOf: (y) => 1000 - y * 10,
};

describe("movedAnchors", () => {
  it("moves every anchor by the same whole bars and pixels", () => {
    const moved = movedAnchors(
      [
        { index: 10, y: 20 },
        { index: 25, y: 60 },
      ],
      3,
      -5,
      linear,
    );

    expect(moved).toEqual([
      { time: 13 * HOUR, price: 850 },
      { time: 28 * HOUR, price: 450 },
    ]);
  });

  it("converts heights through the scale, so a move on a logarithmic scale keeps the ratio", () => {
    const log: DragScale = { timeOf: (index) => index, priceOf: (y) => 10 ** (4 - y / 100) };
    const moved = movedAnchors(
      [
        { index: 0, y: 100 },
        { index: 1, y: 200 },
      ],
      0,
      -100,
      log,
    )!;

    expect(moved[0]!.price).toBeCloseTo(10_000, 6);
    expect(moved[1]!.price).toBeCloseTo(1_000, 6);
  });

  it("gives nothing when a point cannot be converted", () => {
    const noCandles: DragScale = { timeOf: () => undefined, priceOf: () => 1 };
    const noScale: DragScale = { timeOf: () => 1, priceOf: () => Number.NaN };

    expect(movedAnchors([{ index: 0, y: 0 }], 1, 0, noCandles)).toBeUndefined();
    expect(movedAnchors([{ index: 0, y: 0 }], 1, 0, noScale)).toBeUndefined();
  });
});

describe("wholeBarsBetween", () => {
  it("rounds the pointer's move to whole bars", () => {
    expect(wholeBarsBetween(10.2, 13.6)).toBe(3);
    expect(wholeBarsBetween(10.2, 10.6)).toBe(0);
    expect(wholeBarsBetween(13.6, 10.2)).toBe(-3);
  });
});

describe("hasDragged", () => {
  it("ignores the small shake of a click", () => {
    expect(hasDragged({ x: 10, y: 10 }, { x: 12, y: 8 })).toBe(false);
    expect(hasDragged({ x: 10, y: 10 }, { x: 13, y: 10 })).toBe(true);
  });
});
