import { describe, expect, it } from "vitest";
import { defaultStyle, type Drawing } from "./drawing";
import { fibonacciLevels, levelSpan, levelsOf, ratioText } from "./fibonacci";
import { shapeOf } from "./shapes";

const RETRACEMENT = [0, 0.236, 0.382, 0.5, 0.618, 0.786, 1];
const EXTENSION = [1, 1.272, 1.618, 2, 2.618, 3.618];

describe("fibonacciLevels", () => {
  it("puts level 0 of a retracement at the second anchor and level 1 at the first", () => {
    const levels = fibonacciLevels(
      [
        { x: 100, y: 500 }, // start of the move: a low
        { x: 300, y: 100 }, // end of the move: a high
      ],
      false,
      RETRACEMENT,
    )!;

    expect(levels[0]).toEqual({ ratio: 0, y: 100 });
    expect(levels[6]).toEqual({ ratio: 1, y: 500 });
    expect(levels[3]!.y).toBe(300);
    expect(levels[4]!.y).toBeCloseTo(100 + 400 * 0.618, 10);
  });

  it("lays the move from start to end, times the ratio, from the third anchor of an extension", () => {
    const levels = fibonacciLevels(
      [
        { x: 100, y: 500 },
        { x: 200, y: 300 }, // the move: 200 px up
        { x: 300, y: 400 }, // the retracement
      ],
      true,
      EXTENSION,
    )!;

    expect(levels[0]!.y).toBe(200); // 1: the whole move again, from the retracement
    expect(levels[2]!.y).toBeCloseTo(400 - 200 * 1.618, 10);
    expect(levels[5]!.y).toBeCloseTo(400 - 200 * 3.618, 10);
  });

  it("calculates the levels in log space on a logarithmic scale, because pixels follow the logarithm there", () => {
    // A logarithmic scale: y = 1000 - 200 ln(price). A retracement of the move from 100 to 1000.
    const y = (price: number) => 1000 - 200 * Math.log(price);
    const price = (at: number) => Math.exp((1000 - at) / 200);
    const levels = fibonacciLevels([{ x: 0, y: y(100) }, { x: 1, y: y(1000) }], false, [0.5, 0.618])!;

    // Half way in log space between 1000 and 100 is their geometric mean, not 550.
    expect(price(levels[0]!.y)).toBeCloseTo(Math.sqrt(100 * 1000), 8);
    expect(price(levels[1]!.y)).toBeCloseTo(Math.exp(Math.log(1000) + (Math.log(100) - Math.log(1000)) * 0.618), 8);
  });

  it("calculates them in price on a linear scale", () => {
    const y = (price: number) => 1000 - 0.5 * price;
    const price = (at: number) => (1000 - at) / 0.5;
    const levels = fibonacciLevels([{ x: 0, y: y(100) }, { x: 1, y: y(1000) }], false, [0.5])!;

    expect(price(levels[0]!.y)).toBeCloseTo(550, 8);
  });

  it("has no levels while anchors are missing", () => {
    expect(fibonacciLevels([{ x: 0, y: 0 }], false, RETRACEMENT)).toBeUndefined();
    expect(fibonacciLevels([{ x: 0, y: 0 }, { x: 1, y: 1 }], true, EXTENSION)).toBeUndefined();
  });
});

describe("the Fibonacci shapes", () => {
  const pane = { width: 1000, height: 600 };
  const drawing = (tool: "fibonacciRetracement" | "fibonacciExtension", levels: unknown): Drawing => ({
    id: "f",
    tool,
    locked: false,
    anchors: [],
    style: defaultStyle(tool),
    levels,
  });

  it("have two and three anchors", () => {
    expect(shapeOf("fibonacciRetracement")!.anchors).toBe(2);
    expect(shapeOf("fibonacciExtension")!.anchors).toBe(3);
  });

  it("are hit on a level between the first and the last anchor only, as in TradingView", () => {
    const shape = shapeOf("fibonacciRetracement")!;
    const points = [
      { x: 100, y: 500 },
      { x: 300, y: 100 },
    ];
    const fib = drawing("fibonacciRetracement", RETRACEMENT);

    expect(shape.hits({ x: 250, y: 302 }, points, pane, fib)).toBe(true); // level 0.5
    expect(shape.hits({ x: 900, y: 302 }, points, pane, fib)).toBe(false); // right of the last anchor
    expect(shape.hits({ x: 50, y: 300 }, points, pane, fib)).toBe(false); // left of the first
    expect(shape.hits({ x: 250, y: 330 }, points, pane, fib)).toBe(false); // between two levels
  });

  it("span from the leftmost to the rightmost anchor, also when drawn from right to left", () => {
    expect(levelSpan([{ x: 300 }, { x: 100 }])).toEqual({ left: 100, right: 300 });
    expect(levelSpan([{ x: 100 }, { x: 200 }, { x: 400 }])).toEqual({ left: 100, right: 400 });
  });

  it("draw only the levels switched on, and read plain numbers as levels that are shown", () => {
    expect(levelsOf([{ ratio: 0, visible: true }, { ratio: 0.5, visible: false }, { ratio: 1 }])).toEqual([0, 1]);
    expect(levelsOf([0.5, 1])).toEqual([0.5, 1]);
  });

  it("are not hit on a level that is switched off", () => {
    const shape = shapeOf("fibonacciRetracement")!;
    const points = [
      { x: 100, y: 500 },
      { x: 300, y: 100 },
    ];
    const fib = drawing("fibonacciRetracement", [{ ratio: 0, visible: true }, { ratio: 0.5, visible: false }, { ratio: 1, visible: true }]);

    expect(shape.hits({ x: 250, y: 300 }, points, pane, fib)).toBe(false);
  });

  it("ignore levels that are not numbers", () => {
    expect(levelsOf([0.5, "x", Number.NaN, 1, { ratio: "y" }])).toEqual([0.5, 1]);
    expect(levelsOf(undefined)).toEqual([]);
  });

  it("write the ratio short", () => {
    expect(ratioText(0.618)).toBe("0.618");
    expect(ratioText(1)).toBe("1");
    expect(ratioText(1.2720000001)).toBe("1.272");
  });
});
