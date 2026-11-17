import { describe, expect, it } from "vitest";
import { TimeMapping, timeframeSeconds } from "./timeMapping";

const MINUTE = 60;
const HOUR = 3600;
const DAY = 86_400;

/** Open times of `count` candles of `step` seconds from `start`. */
function times(count: number, step: number, start = 1_700_000_000 - (1_700_000_000 % DAY)): number[] {
  return Array.from({ length: count }, (_, index) => start + index * step);
}

describe("timeframeSeconds", () => {
  it("knows the timeframes of the Downloader", () => {
    expect(timeframeSeconds("15m")).toBe(15 * MINUTE);
    expect(timeframeSeconds("1h")).toBe(HOUR);
    expect(timeframeSeconds("4h")).toBe(4 * HOUR);
    expect(timeframeSeconds("1d")).toBe(DAY);
    expect(timeframeSeconds("1w")).toBe(7 * DAY);
  });

  it("has no length for codes it cannot count", () => {
    expect(timeframeSeconds("1M")).toBeUndefined(); // calendar months: later
    expect(timeframeSeconds("0h")).toBeUndefined();
    expect(timeframeSeconds("h")).toBeUndefined();
    expect(timeframeSeconds("")).toBeUndefined();
  });
});

describe("TimeMapping.indexOf", () => {
  const hours = times(100, HOUR);
  const mapping = new TimeMapping(hours, HOUR);
  const first = hours[0]!;
  const last = hours[99]!;

  it("puts a candle's own open time on that candle", () => {
    expect(mapping.indexOf(first)).toBe(0);
    expect(mapping.indexOf(hours[42]!)).toBe(42);
    expect(mapping.indexOf(last)).toBe(99);
  });

  it("puts a time between two open times on the candle that contains it, never in between", () => {
    // The high of the 10:45 candle on 15m belongs on the 10:00 candle on 1h.
    expect(mapping.indexOf(hours[10]! + 45 * MINUTE)).toBe(10);
    expect(mapping.indexOf(hours[11]! - 1)).toBe(10);
  });

  it("counts whole timeframes after the last candle (DR3)", () => {
    expect(mapping.indexOf(last + 30 * MINUTE)).toBe(99); // still inside the last candle
    expect(mapping.indexOf(last + HOUR)).toBe(100);
    expect(mapping.indexOf(last + 5 * HOUR + 59 * MINUTE)).toBe(104);
  });

  it("counts whole timeframes before the first candle, rounded outwards", () => {
    expect(mapping.indexOf(first - HOUR)).toBe(-1);
    expect(mapping.indexOf(first - 1)).toBe(-1); // inside the candle before the first
    expect(mapping.indexOf(first - HOUR - 1)).toBe(-2);
    expect(mapping.indexOf(first - 10 * HOUR)).toBe(-10);
  });

  it("puts a time inside a gap of the source on the last candle before the gap", () => {
    const withGap = [...times(5, HOUR), ...times(5, HOUR, hours[0]! + 20 * HOUR)];
    const gapped = new TimeMapping(withGap, HOUR);

    expect(gapped.indexOf(withGap[4]! + 3 * HOUR)).toBe(4);
    expect(gapped.indexOf(withGap[5]!)).toBe(5);
  });

  it("knows no index without candles or for a time that is not a number", () => {
    expect(new TimeMapping([], HOUR).indexOf(first)).toBeUndefined();
    expect(mapping.indexOf(Number.NaN)).toBeUndefined();
    expect(mapping.indexOf(Number.POSITIVE_INFINITY)).toBeUndefined();
  });

  it("works with a single candle", () => {
    const one = new TimeMapping([first], DAY);

    expect(one.indexOf(first + 2 * DAY)).toBe(2);
    expect(one.indexOf(first - 1)).toBe(-1);
  });

  it("finds the candle in a long series", () => {
    const quarters = times(200_000, 15 * MINUTE); // about 5.7 years of 15m candles
    const big = new TimeMapping(quarters, 15 * MINUTE);

    for (const index of [0, 1, 12_345, 99_999, 199_998, 199_999]) {
      expect(big.indexOf(quarters[index]! + 7 * MINUTE)).toBe(index);
    }
  });
});

describe("TimeMapping.timeOf", () => {
  const days = times(30, DAY);
  const mapping = new TimeMapping(days, DAY);

  it("gives the open time of a candle", () => {
    expect(mapping.timeOf(0)).toBe(days[0]);
    expect(mapping.timeOf(17)).toBe(days[17]);
    expect(mapping.timeOf(29)).toBe(days[29]);
  });

  it("rounds a fractional index to the nearest candle", () => {
    expect(mapping.timeOf(16.6)).toBe(days[17]);
    expect(mapping.timeOf(16.4)).toBe(days[16]);
  });

  it("counts whole timeframes outside the candles", () => {
    expect(mapping.timeOf(32)).toBe(days[29]! + 3 * DAY);
    expect(mapping.timeOf(-2)).toBe(days[0]! - 2 * DAY);
  });

  it("is the way back of indexOf, also outside the candles", () => {
    for (const index of [-50, -1, 0, 5, 29, 30, 300]) {
      expect(mapping.indexOf(mapping.timeOf(index)!)).toBe(index);
    }
  });

  it("knows no time without candles", () => {
    expect(new TimeMapping([], DAY).timeOf(0)).toBeUndefined();
    expect(mapping.timeOf(Number.NaN)).toBeUndefined();
  });

  it("refuses a timeframe without a length", () => {
    expect(() => new TimeMapping(days, 0)).toThrow(RangeError);
  });
});
