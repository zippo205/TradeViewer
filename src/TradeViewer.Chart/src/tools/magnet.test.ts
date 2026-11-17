import { describe, expect, it } from "vitest";
import { snapPrice } from "./magnet";

const bar = { time: 0, open: 100, high: 120, low: 90, close: 110, volume: 1 };
const linear = (price: number) => 1000 - price * 5;

describe("snapPrice", () => {
  it("takes the open, high, low or close nearest to the pointer", () => {
    expect(snapPrice(bar, linear(118), linear)).toBe(120);
    expect(snapPrice(bar, linear(108), linear)).toBe(110);
    expect(snapPrice(bar, linear(50), linear)).toBe(90);
    expect(snapPrice(bar, linear(101), linear)).toBe(100);
  });

  it("measures in pixels, so a logarithmic scale decides by what the user sees", () => {
    const log = (price: number) => 1000 - Math.log(price) * 1000;
    // 94.9 is nearer to 90 in price (4.9 against 5.1), but on a log scale it lies above the middle
    // between 90 and 100 (the geometric mean, 94.87), so it is nearer to 100 there.
    expect(snapPrice(bar, log(94.9), log)).toBe(100);
    expect(snapPrice(bar, log(94.8), log)).toBe(90);
  });

  it("gives nothing when no value can be placed on the scale", () => {
    expect(snapPrice(bar, 300, () => undefined)).toBeUndefined();
  });
});
