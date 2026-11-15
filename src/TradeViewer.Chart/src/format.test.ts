import { describe, expect, it } from "vitest";
import { formatPrice, formatTimeUtc, formatVolume, isIntraday, priceFormatOf } from "./format";

describe("priceFormatOf", () => {
  it.each([
    [0.01, 2],
    [0.0001, 4],
    [0.00000001, 8],
    [0.5, 1],
    [0.25, 2],
    [1, 0],
    [5, 0],
  ])("shows prices with the precision of the tick size %d", (tickSize, precision) => {
    expect(priceFormatOf(tickSize)).toEqual({ precision, minMove: tickSize });
  });

  it("falls back to two decimals for a tick size that is none", () => {
    expect(priceFormatOf(0)).toEqual({ precision: 2, minMove: 0.01 });
    expect(priceFormatOf(Number.NaN)).toEqual({ precision: 2, minMove: 0.01 });
  });

  it("formats a price with that precision", () => {
    expect(formatPrice(64975.13, priceFormatOf(0.01))).toBe("64975.13");
    expect(formatPrice(64975.1, priceFormatOf(0.01))).toBe("64975.10");
    expect(formatPrice(0.25, priceFormatOf(0.0001))).toBe("0.2500");
  });
});

describe("formatVolume", () => {
  it.each([
    [0, "0"],
    [950, "950"],
    [507.06141, "507.06"],
    [12300, "12.3K"],
    [4560000, "4.56M"],
    [1200000000, "1.2B"],
  ])("shortens %d to %s", (value, text) => {
    expect(formatVolume(value)).toBe(text);
  });
});

describe("formatTimeUtc", () => {
  it("shows date and time in UTC for intraday candles", () => {
    expect(formatTimeUtc(1794770100, true)).toBe("2026-11-15 19:15");
  });

  it("shows only the date for daily and weekly candles", () => {
    expect(formatTimeUtc(1794700800, false)).toBe("2026-11-15");
  });

  it("knows which timeframes are intraday", () => {
    expect(["15m", "1h", "4h"].map(isIntraday)).toEqual([true, true, true]);
    expect(["1d", "1w"].map(isIntraday)).toEqual([false, false]);
  });
});
