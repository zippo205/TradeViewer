import { describe, expect, it } from "vitest";
import { toBarPage, type Bar, type BarPage } from "./bars";
import { SeriesData, shouldLoadOlder } from "./seriesData";

const STEP = 900;

/** Bars at 0, 900, 1800, ... with prices that tell them apart. */
function bars(fromIndex: number, count: number, seed = 0): Bar[] {
  return Array.from({ length: count }, (_, offset) => {
    const index = fromIndex + offset;
    const price = 100 + index + seed;
    return { time: index * STEP, open: price, high: price + 2, low: price - 1, close: price + 1, volume: 10 + index };
  });
}

function page(fromIndex: number, count: number, partial = false, seed = 0): BarPage {
  return { bars: bars(fromIndex, count, seed), partial };
}

describe("toBarPage", () => {
  it("turns the column arrays into bars, oldest first", () => {
    const result = toBarPage({ t: [900, 1800], o: [1, 2], h: [3, 4], l: [0.5, 1.5], c: [2, 3], v: [10, 20], partial: true });

    expect(result.bars).toEqual([
      { time: 900, open: 1, high: 3, low: 0.5, close: 2, volume: 10 },
      { time: 1800, open: 2, high: 4, low: 1.5, close: 3, volume: 20 },
    ]);
    expect(result.partial).toBe(true);
  });

  it("has no current candle without candles", () => {
    expect(toBarPage({ t: [], o: [], h: [], l: [], c: [], v: [], partial: true })).toEqual({ bars: [], partial: false });
  });
});

describe("SeriesData", () => {
  it("starts with the newest page and knows whether older candles may exist", () => {
    const full = new SeriesData();
    const short = new SeriesData();

    expect(full.setLatest(page(100, 4), 4)).toEqual({ firstChanged: 0, reset: false });
    short.setLatest(page(100, 3), 4);

    expect(full.bars).toEqual(bars(100, 4));
    expect(full.oldestTime).toBe(100 * STEP);
    expect(full.hasMore).toBe(true);
    expect(short.hasMore).toBe(false); // fewer than asked for: this is the whole history
  });

  it("does not count the current candle when it decides whether the page was full", () => {
    const data = new SeriesData();

    data.setLatest(page(100, 5, true), 4); // 4 closed candles and the current one

    expect(data.hasMore).toBe(true);
    expect(data.partial).toBe(true);
  });

  it("adds older pages in front until a short page marks the start of the history", () => {
    const data = new SeriesData();
    data.setLatest(page(100, 4), 4);

    expect(data.addOlder(page(90, 10), 10)).toBe(10);
    expect(data.hasMore).toBe(true);
    expect(data.addOlder(page(87, 3), 10)).toBe(3);

    expect(data.bars).toEqual(bars(87, 17));
    expect(data.hasMore).toBe(false);
    expect(data.oldestTime).toBe(87 * STEP);
  });

  it("ignores candles of an older page that it has already", () => {
    const data = new SeriesData();
    data.setLatest(page(100, 4), 4);

    expect(data.addOlder(page(95, 7), 7)).toBe(5); // 100 and 101 are there already

    expect(data.bars).toEqual(bars(95, 9));
  });

  it("appends a new candle on a refresh and keeps the older pages", () => {
    const data = new SeriesData();
    data.setLatest(page(100, 4), 4);
    data.addOlder(page(90, 10), 10);

    const result = data.setLatest(page(101, 4), 4); // one candle later

    expect(result).toEqual({ firstChanged: 14, reset: false });
    expect(data.bars).toEqual(bars(90, 15));
    expect(data.hasMore).toBe(true); // a refresh says nothing about the old end
  });

  it("reports nothing when a refresh brings the same candles", () => {
    const data = new SeriesData();
    data.setLatest(page(100, 4, true), 4);

    expect(data.setLatest(page(100, 4, true), 4)).toEqual({ firstChanged: -1, reset: false });
  });

  it("replaces a candle the Downloader rewrote and everything after it", () => {
    const data = new SeriesData();
    data.setLatest(page(100, 6), 6);

    const rewritten = [...bars(100, 2), ...bars(102, 4, 50)];
    const result = data.setLatest({ bars: rewritten, partial: false }, 6);

    expect(result).toEqual({ firstChanged: 2, reset: false });
    expect(data.bars).toEqual(rewritten);
  });

  it("replaces the current candle by the closed one and starts the next", () => {
    const data = new SeriesData();
    data.setLatest(page(100, 3, true), 4); // 102 is in progress

    const closed = [...bars(100, 2), ...bars(102, 1, 7), ...bars(103, 1)];
    const result = data.setLatest({ bars: closed, partial: true }, 4);

    expect(result.firstChanged).toBe(2);
    expect(data.bars).toEqual(closed);
    expect(data.partial).toBe(true);
  });

  it("notices when the current candle only stops being in progress", () => {
    const data = new SeriesData();
    data.setLatest(page(100, 3, true), 4);

    const result = data.setLatest(page(100, 3, false), 4);

    expect(result).toEqual({ firstChanged: 2, reset: false });
    expect(data.partial).toBe(false);
  });

  it("drops candles at the end that the store no longer has", () => {
    const data = new SeriesData();
    data.setLatest(page(100, 4), 4);

    const result = data.setLatest(page(100, 3), 4);

    expect(result).toEqual({ firstChanged: 3, reset: false });
    expect(data.bars).toEqual(bars(100, 3));
  });

  it("starts over when the newest page does not reach back to the candles in memory", () => {
    const data = new SeriesData();
    data.setLatest(page(100, 4), 4);
    data.addOlder(page(98, 2), 10);

    const result = data.setLatest(page(500, 4), 4); // the window slept for days

    expect(result).toEqual({ firstChanged: 0, reset: true });
    expect(data.bars).toEqual(bars(500, 4));
    expect(data.hasMore).toBe(true);
  });

  it("is empty when the store has no candles", () => {
    const data = new SeriesData();

    expect(data.setLatest({ bars: [], partial: false })).toEqual({ firstChanged: -1, reset: false });
    expect(data.bars).toEqual([]);
    expect(data.hasMore).toBe(false);
    expect(data.oldestTime).toBeUndefined();
  });

  it("is emptied when the candles are gone (the asset was rebuilt)", () => {
    const data = new SeriesData();
    data.setLatest(page(100, 4), 4);

    expect(data.setLatest({ bars: [], partial: false })).toEqual({ firstChanged: 0, reset: true });
    expect(data.bars).toEqual([]);
  });
});

describe("shouldLoadOlder", () => {
  it("asks for older candles when the view comes close to the oldest one", () => {
    expect(shouldLoadOlder(499, true, false)).toBe(true);
    expect(shouldLoadOlder(-20, true, false)).toBe(true); // the user scrolled past the edge
    expect(shouldLoadOlder(500, true, false)).toBe(false);
  });

  it("does not ask twice at once or after the start of the history", () => {
    expect(shouldLoadOlder(10, true, true)).toBe(false);
    expect(shouldLoadOlder(10, false, false)).toBe(false);
  });
});
