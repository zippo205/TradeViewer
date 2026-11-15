import { describe, expect, it } from "vitest";
import type { Bar } from "./data/bars";
import { DEFAULT_RIGHT_GAP, DEFAULT_VISIBLE_BARS, captureView, indexAtOrBefore, initialRange, latestRange, restoreView } from "./range";

function bars(count: number, step: number, start = 0): Bar[] {
  return Array.from({ length: count }, (_, index) => ({
    time: start + index * step,
    open: 1,
    high: 2,
    low: 0.5,
    close: 1.5,
    volume: 1,
  }));
}

const HOUR = 3600;
const quarters = bars(4000, 900); // 15m candles, 1000 hours
const hours = bars(1000, HOUR); // the same time span in 1h candles

describe("initialRange", () => {
  it("shows the latest candles with some empty space to the right", () => {
    const range = initialRange(2000);

    expect(range.to).toBe(1999 + DEFAULT_RIGHT_GAP);
    expect(range.to - range.from).toBe(DEFAULT_VISIBLE_BARS);
  });
});

describe("latestRange", () => {
  it("jumps to the latest candle and keeps the number of candles in view", () => {
    expect(latestRange(80, 2000)).toEqual({ from: 1999 + DEFAULT_RIGHT_GAP - 80, to: 1999 + DEFAULT_RIGHT_GAP });
  });
});

describe("indexAtOrBefore", () => {
  it("finds the candle that contains a time", () => {
    expect(indexAtOrBefore(hours, 5 * HOUR)).toBe(5);
    expect(indexAtOrBefore(hours, 5 * HOUR + 1799)).toBe(5); // a time between two candles belongs to the earlier one
    expect(indexAtOrBefore(hours, 999 * HOUR + 10 * HOUR)).toBe(999);
  });

  it("is -1 before the first candle and without candles", () => {
    expect(indexAtOrBefore(bars(10, HOUR, 100 * HOUR), 5)).toBe(-1);
    expect(indexAtOrBefore([], 5)).toBe(-1);
  });
});

describe("changing the timeframe (CH3)", () => {
  it("stays at the latest candles with the same number of candles and the same space to the right", () => {
    const view = captureView({ from: 3899.5, to: 4004.5 }, quarters); // 105 candles, 5.5 bars of space

    const range = restoreView(view, hours, false);

    expect(range.to).toBe(999 + 5.5);
    expect(range.to - range.from).toBe(105);
  });

  it("keeps the same time at the right edge when the chart was scrolled back", () => {
    // The right edge is the 15m candle of hour 500, third quarter.
    const view = captureView({ from: 1902, to: 2002 }, quarters);

    const range = restoreView(view, hours, false);

    expect(view?.atLatest).toBe(false);
    expect(range.to).toBe(500); // the 1h candle that contains that time
    expect(range.to - range.from).toBe(100);
  });

  it("works from a larger to a smaller timeframe as well", () => {
    const view = captureView({ from: 400, to: 500 }, hours);

    const range = restoreView(view, quarters, false);

    expect(range.to).toBe(2000); // the 15m candle that opens at hour 500
    expect(range.to - range.from).toBe(100);
  });

  it("stays at the first candle when the time at the edge is older than the new history", () => {
    const view = captureView({ from: 0, to: 100 }, quarters);
    const shortHistory = bars(200, HOUR, 800 * HOUR);

    const range = restoreView(view, shortHistory, false);

    expect(range.to).toBe(0);
  });
});

describe("changing the asset (CH3)", () => {
  it("keeps the number of candles and shows the latest ones, also when the chart was scrolled back", () => {
    const view = captureView({ from: 1902, to: 2002 }, quarters);
    const other = bars(700, 900, 123456);

    const range = restoreView(view, other, true);

    expect(range.to).toBe(699 + DEFAULT_RIGHT_GAP);
    expect(range.to - range.from).toBe(100);
  });

  it("keeps the space to the right when the chart was at the latest candles", () => {
    const view = captureView({ from: 3900, to: 4002 }, quarters);

    const range = restoreView(view, bars(700, 900), true);

    expect(range.to).toBe(699 + 3);
    expect(range.to - range.from).toBe(102);
  });
});

describe("a view that cannot be kept", () => {
  it("falls back to a newly opened chart", () => {
    expect(restoreView(undefined, hours, false)).toEqual(initialRange(1000));
    expect(captureView({ from: 0, to: 10 }, [])).toBeUndefined();
    expect(restoreView(captureView({ from: 0, to: 10 }, hours), [], false)).toEqual(initialRange(0));
  });
});
