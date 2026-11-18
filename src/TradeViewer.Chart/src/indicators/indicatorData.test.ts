import { describe, expect, it } from "vitest";
import type { IndicatorPage } from "./indicatorClient";
import { IndicatorValues } from "./indicatorData";
import { indicatorAddress } from "./indicatorClient";

/** Values at 0, 900, 1800, ...: the value of index i is i + seed, null before the first defined one. */
function page(from: number, count: number, seed = 0, firstDefined = 0, partial = false): IndicatorPage {
  const t = Array.from({ length: count }, (_, i) => (from + i) * 900);
  return { t, lines: { value: t.map((_, i) => (from + i < firstDefined ? null : from + i + seed)) }, partial };
}

describe("IndicatorValues", () => {
  it("holds the newest page and gives the values at a time", () => {
    const values = new IndicatorValues(["value"]);

    expect(values.setLatest(page(0, 10, 0, 3))).toBe(true);

    expect(values.t).toHaveLength(10);
    expect(values.at(0)).toEqual({ value: null });
    expect(values.at(900 * 5)).toEqual({ value: 5 });
    expect(values.at(450)).toBeUndefined();
  });

  it("adds an older page in front and keeps what it holds", () => {
    const values = new IndicatorValues(["value"]);
    values.setLatest(page(100, 50));

    expect(values.addOlder(page(40, 61, 1000))).toBe(60); // the candle at 100 is held already

    expect(values.oldestTime).toBe(40 * 900);
    expect(values.at(99 * 900)).toEqual({ value: 1099 });
    expect(values.at(100 * 900)).toEqual({ value: 100 });
  });

  it("takes a refresh from its first time on: the current candle changes, a new one is added", () => {
    const values = new IndicatorValues(["value"]);
    values.setLatest(page(0, 100, 0, 0, true));

    expect(values.setLatest(page(95, 6, 1000, 0, true))).toBe(true);

    expect(values.t).toHaveLength(101);
    expect(values.at(94 * 900)).toEqual({ value: 94 });
    expect(values.at(99 * 900)).toEqual({ value: 1099 });
    expect(values.at(100 * 900)).toEqual({ value: 1100 });
  });

  it("tells when a refresh does not join the values held, after a long sleep", () => {
    const values = new IndicatorValues(["value"]);
    values.setLatest(page(0, 100));

    expect(values.setLatest(page(2000, 500))).toBe(false);

    expect(values.oldestTime).toBe(2000 * 900); // replaced; the caller reads back to the oldest candle
  });

  it("has one value per time in every line, also when the Api leaves a line out", () => {
    const values = new IndicatorValues(["macd", "signal", "histogram"]);
    values.setLatest({ t: [0, 900], lines: { macd: [1, 2] }, partial: false });

    expect(values.line("signal")).toEqual([null, null]);
    expect(values.line("histogram")).toHaveLength(2);
  });
});

describe("indicatorAddress", () => {
  it("asks for the page of the candles with the parameters of the indicator", () => {
    const key = { source: "binance", symbol: "BRK.B", timeframe: "1d" };
    const bb = { id: "x", type: "bb" as const, length: 20, deviation: 2, price: "close", styles: [], hidden: false };

    expect(indicatorAddress(key, bb, undefined, 2000)).toBe("api/indicators/binance/BRK.B/1d?type=bb&length=20&deviation=2&price=close&limit=2000");
    expect(indicatorAddress(key, bb, 1735603200, 5000)).toBe("api/indicators/binance/BRK.B/1d?type=bb&length=20&deviation=2&price=close&limit=5000&to=1735603200");
  });
});
