import { describe, expect, it } from "vitest";
import { histogramColour, lighter, linesOf, onPrice, parametersOf, titleOf, valuesKey, withOpacity, type IndicatorInstance } from "./indicator";

const style = (line: string, color = "#2962FF") => ({ line, color, width: 1 });

const sma: IndicatorInstance = { id: "a", type: "sma", length: 9, price: "close", styles: [style("value")], hidden: false };
const macd: IndicatorInstance = {
  id: "b",
  type: "macd",
  fast: 12,
  slow: 26,
  signal: 9,
  price: "close",
  styles: [style("macd"), style("signal", "#FF6D00"), style("histogramAbove", "#26A69A"), style("histogramBelow", "#FF5252")],
  hidden: false,
};

describe("indicator", () => {
  it("names the legend line as TradingView does", () => {
    expect(titleOf(sma)).toBe("SMA 9 close");
    expect(titleOf(macd)).toBe("MACD close 12 26 9");
    expect(titleOf({ ...sma, type: "bb", length: 20, deviation: 2 })).toBe("BB 20 close 2");
    expect(titleOf({ id: "c", type: "atr", length: 14, styles: [], hidden: false })).toBe("ATR 14");
  });

  it("asks for the parameters by the names of the Api and leaves out those the type has not", () => {
    expect(parametersOf(macd)).toEqual(["type=macd", "fast=12", "slow=26", "signal=9", "price=close"]);
    expect(parametersOf({ id: "c", type: "atr", length: 14, price: null, styles: [], hidden: false })).toEqual(["type=atr", "length=14"]);
  });

  it("fetches again for other parameters, not for another colour or hidden", () => {
    expect(valuesKey({ ...sma, styles: [style("value", "#000000")], hidden: true })).toBe(valuesKey(sma));
    expect(valuesKey({ ...sma, length: 10 })).not.toBe(valuesKey(sma));
    expect(valuesKey({ ...sma, price: "hl2" })).not.toBe(valuesKey(sma));
  });

  it("draws SMA, EMA and BB on the price and the others in their own pane", () => {
    expect(["sma", "ema", "bb", "rsi", "macd", "atr"].map((type) => onPrice(type as IndicatorInstance["type"]))).toEqual([true, true, true, false, false, false]);
    expect(linesOf("bb")).toEqual(["basis", "upper", "lower"]);
  });

  it("colours MACD columns in TradingView's four shades", () => {
    const above = "#26A69A";
    const below = "#FF5252";

    expect(histogramColour(5, 3, above, below)).toBe(above); // above zero, growing
    expect(histogramColour(3, 5, above, below)).toBe(lighter(above)); // above zero, falling
    expect(histogramColour(-5, -3, above, below)).toBe(below); // below zero, falling
    expect(histogramColour(-3, -5, above, below)).toBe(lighter(below)); // below zero, growing
    expect(histogramColour(3, 3, above, below)).toBe(lighter(above)); // equal: falling, as TradingView
    expect(histogramColour(3, null, above, below)).toBe(lighter(above)); // nothing before: falling
  });

  it("makes the pale shades close to TradingView's", () => {
    const distance = (a: string, b: string) =>
      Math.max(...[1, 3, 5].map((i) => Math.abs(parseInt(a.slice(i, i + 2), 16) - parseInt(b.slice(i, i + 2), 16))));

    expect(distance(lighter("#26A69A"), "#B2DFDB")).toBeLessThanOrEqual(10);
    expect(distance(lighter("#FF5252"), "#FFCDD2")).toBeLessThanOrEqual(10);
    expect(lighter("not a colour")).toBe("not a colour");
    expect(withOpacity("#7E57C2", 0.1)).toBe("rgba(126, 87, 194, 0.1)");
  });
});
