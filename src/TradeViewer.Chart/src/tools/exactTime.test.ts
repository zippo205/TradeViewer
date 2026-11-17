import { describe, expect, it } from "vitest";
import { extremeOf, finestOf, madeAt } from "./exactTime";

const bar = (time: number, high: number, low: number) => ({ time, open: low, high, low, close: high, volume: 1 });

describe("extremeOf", () => {
  it("knows the high and the low of a candle, and nothing else", () => {
    const candle = bar(0, 120, 90);

    expect(extremeOf(candle, 120)).toBe("high");
    expect(extremeOf(candle, 90)).toBe("low");
    expect(extremeOf(candle, 100)).toBeUndefined();
  });
});

describe("madeAt", () => {
  // The 15m candles of one hour: the high of the hour (130) was made at 00:30, the low (95) at 00:45.
  const quarters = [bar(0, 120, 100), bar(900, 125, 99), bar(1800, 130, 101), bar(2700, 128, 95)];

  it("finds the candle that made the high or the low", () => {
    expect(madeAt(quarters, "high", 130)).toBe(1800);
    expect(madeAt(quarters, "low", 95)).toBe(2700);
  });

  it("takes the first when the price was made twice, and none when it was never made", () => {
    expect(madeAt([...quarters, bar(3600, 130, 110)], "high", 130)).toBe(1800);
    expect(madeAt(quarters, "high", 131)).toBeUndefined();
  });
});

describe("finestOf", () => {
  it("is the timeframe with the shortest candles, whatever the order", () => {
    expect(finestOf(["1d", "15m", "4h"])).toEqual({ code: "15m", seconds: 900 });
    expect(finestOf(["1w", "1d"])).toEqual({ code: "1d", seconds: 86_400 });
    expect(finestOf(["1M"])).toBeUndefined();
    expect(finestOf([])).toBeUndefined();
  });
});
