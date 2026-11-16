import { describe, expect, it } from "vitest";
import type { Bar } from "./bars";
import { fetchGaps, gapText, gapsAddress, placeGaps } from "./gaps";

const HOUR = 3600;
const start = Date.UTC(2021, 7, 13, 0, 0, 0) / 1000; // 2021-08-13 00:00 UTC

/** Hourly bars with an outage from 02:00 to 06:00: the bars of 02, 03, 04 and 05 are missing. */
const bars: Bar[] = [0, 1, 6, 7, 8].map((hour) => ({ time: start + hour * HOUR, open: 1, high: 2, low: 0.5, close: 1.5, volume: 1 }));
const outage = { from: start + 2 * HOUR, to: start + 6 * HOUR, reason: "missingUpstream" };

describe("placeGaps", () => {
  it("puts a gap between the last candle before it and the first one after it", () => {
    expect(placeGaps(bars, [outage])).toEqual([{ gap: outage, beforeTime: start + HOUR, afterTime: start + 6 * HOUR }]);
  });

  it("has no place for a gap that is older than the candles in memory", () => {
    const older = { from: start - 10 * HOUR, to: start - 8 * HOUR, reason: "missingUpstream" };

    expect(placeGaps(bars, [older, outage])).toHaveLength(1);
    expect(placeGaps([], [outage])).toEqual([]);
  });

  it("puts a gap that reaches to the newest candle at that candle", () => {
    const open = { from: start + 9 * HOUR, to: start + 12 * HOUR, reason: "missingUpstream" };

    expect(placeGaps(bars, [open])).toEqual([{ gap: open, beforeTime: start + 8 * HOUR, afterTime: undefined }]);
  });

  it("keeps the order of the gaps", () => {
    const later = { from: start + 7 * HOUR + 1, to: start + 8 * HOUR, reason: "missingUpstream" };

    expect(placeGaps(bars, [outage, later]).map((marker) => marker.beforeTime)).toEqual([start + HOUR, start + 7 * HOUR]);
  });
});

describe("gapText", () => {
  it("says from when to when candles are missing and why", () => {
    expect(gapText(outage)).toBe("No candles from 2021-08-13 02:00 to 06:00 UTC. The source has no candles for this time.");
  });

  it("names both days when the gap spans midnight", () => {
    const long = { from: start + 22 * HOUR, to: start + 27 * HOUR, reason: "missingUpstream" };

    expect(gapText(long)).toBe("No candles from 2021-08-13 22:00 to 2021-08-14 03:00 UTC. The source has no candles for this time.");
  });

  it("passes on a reason it does not know", () => {
    expect(gapText({ ...outage, reason: "maintenance" })).toContain("Reason given by the Downloader: maintenance.");
  });
});

describe("fetchGaps", () => {
  const key = { source: "binance", symbol: "BTCUSDT", timeframe: "1h" };

  it("asks the Api for the gaps of the series", async () => {
    let asked = "";
    const fetcher: typeof fetch = async (input) => {
      asked = String(input);
      return new Response(JSON.stringify([outage]), { status: 200 });
    };

    expect(await fetchGaps(key, fetcher)).toEqual([outage]);
    expect(asked).toBe("api/gaps/binance/BTCUSDT/1h");
    expect(gapsAddress({ source: "yahoo", symbol: "EURUSD=X", timeframe: "1d" })).toBe("api/gaps/yahoo/EURUSD%3DX/1d");
  });

  it("shows the chart without markers when the gaps cannot be read", async () => {
    const failing: typeof fetch = async () => {
      throw new TypeError("Failed to fetch");
    };
    const refused: typeof fetch = async () => new Response("{}", { status: 404 });

    expect(await fetchGaps(key, failing)).toEqual([]);
    expect(await fetchGaps(key, refused)).toEqual([]);
  });
});
