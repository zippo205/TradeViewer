import { describe, expect, it } from "vitest";
import { defaultStyle, type Drawing, type ToolCode } from "../drawing";
import { arrowHead } from "./arrow";
import { shapeOf } from "./index";
import { parallelOf } from "./parallelChannel";

const pane = { width: 1000, height: 600 };

function drawing(tool: ToolCode, extra: Partial<Drawing> = {}): Drawing {
  return { id: "x", tool, locked: false, anchors: [], style: defaultStyle(tool), ...extra };
}

function hits(tool: ToolCode, points: { x: number; y: number }[], at: { x: number; y: number }, extra: Partial<Drawing> = {}): boolean {
  return shapeOf(tool)!.hits(at, points, pane, drawing(tool, extra));
}

describe("trend line and arrow", () => {
  it.each<ToolCode>(["trendLine", "arrow"])("%s is hit along its segment only", (tool) => {
    const points = [
      { x: 100, y: 100 },
      { x: 300, y: 300 },
    ];

    expect(hits(tool, points, { x: 200, y: 203 })).toBe(true);
    expect(hits(tool, points, { x: 200, y: 215 })).toBe(false);
    expect(hits(tool, points, { x: 400, y: 400 })).toBe(false); // beyond the second anchor
  });

  it("draws the head of an arrow at the second anchor, pointing back along the line", () => {
    const [left, right] = arrowHead({ x: 0, y: 0 }, { x: 100, y: 0 }, 2)!;

    expect(left.x).toBeLessThan(100);
    expect(right.x).toBeCloseTo(left.x, 10);
    expect(left.y).toBeCloseTo(-right.y, 10);
    expect(arrowHead({ x: 5, y: 5 }, { x: 5, y: 5 }, 2)).toBeUndefined();
  });
});

describe("ray", () => {
  it("is hit beyond its second anchor up to the edge of the pane, not before the first", () => {
    const points = [
      { x: 100, y: 300 },
      { x: 200, y: 300 },
    ];

    expect(hits("ray", points, { x: 900, y: 302 })).toBe(true);
    expect(hits("ray", points, { x: 50, y: 300 })).toBe(false);
  });
});

describe("horizontal and vertical line", () => {
  it("a horizontal line is hit across the whole pane at its height", () => {
    expect(hits("horizontalLine", [{ x: 500, y: 200 }], { x: 3, y: 204 })).toBe(true);
    expect(hits("horizontalLine", [{ x: 500, y: 200 }], { x: 3, y: 220 })).toBe(false);
  });

  it("a vertical line is hit over the whole height at its time", () => {
    expect(hits("verticalLine", [{ x: 500, y: 200 }], { x: 497, y: 590 })).toBe(true);
    expect(hits("verticalLine", [{ x: 500, y: 200 }], { x: 480, y: 200 })).toBe(false);
  });
});

describe("rectangle", () => {
  const points = [
    { x: 100, y: 100 },
    { x: 300, y: 200 },
  ];

  it("is hit inside when filled, on its edges only when not", () => {
    expect(hits("rectangle", points, { x: 200, y: 150 })).toBe(true);
    expect(hits("rectangle", points, { x: 200, y: 150 }, { style: { ...defaultStyle("rectangle"), fill: null } })).toBe(false);
    expect(hits("rectangle", points, { x: 302, y: 150 }, { style: { ...defaultStyle("rectangle"), fill: null } })).toBe(true);
    expect(hits("rectangle", points, { x: 320, y: 150 })).toBe(false);
  });

  it("has a handle on each corner; a free corner changes one coordinate of each anchor", () => {
    const shape = shapeOf("rectangle")!;

    expect(shape.handles(points, pane)).toEqual([
      { x: 100, y: 100 },
      { x: 300, y: 200 },
      { x: 100, y: 200 },
      { x: 300, y: 100 },
    ]);
    expect(shape.reshape(points, 3, { x: 350, y: 50 })).toEqual([
      { x: 100, y: 50 },
      { x: 350, y: 200 },
    ]);
  });

  it("keeps the anchor that a handle does not move as the same object", () => {
    const moved = shapeOf("rectangle")!.reshape(points, 1, { x: 400, y: 250 });

    expect(moved[0]).toBe(points[0]);
    expect(moved[1]).toEqual({ x: 400, y: 250 });
  });
});

describe("parallel channel", () => {
  const points = [
    { x: 100, y: 300 },
    { x: 300, y: 200 },
    { x: 200, y: 350 }, // 100 px below the line at x = 200
  ];

  it("draws the parallel through the third anchor", () => {
    expect(parallelOf(points)).toEqual([
      { x: 100, y: 400 },
      { x: 300, y: 300 },
    ]);
    expect(parallelOf(points.slice(0, 2))).toBeUndefined();
  });

  it("is hit on both lines and, filled, between them", () => {
    expect(hits("parallelChannel", points, { x: 300, y: 302 })).toBe(true);
    expect(hits("parallelChannel", points, { x: 200, y: 300 })).toBe(true);
    expect(hits("parallelChannel", points, { x: 200, y: 300 }, { style: { ...defaultStyle("parallelChannel"), fill: null } })).toBe(false);
    expect(hits("parallelChannel", points, { x: 200, y: 450 })).toBe(false);
  });

  it("keeps the distance when the line is reshaped, and changes it by the third handle", () => {
    const shape = shapeOf("parallelChannel")!;

    const turned = shape.reshape(points, 1, { x: 300, y: 300 }); // the line becomes flat at 300
    expect(turned[2]).toEqual({ x: 200, y: 400 });
    expect(parallelOf(turned)).toEqual([
      { x: 100, y: 400 },
      { x: 300, y: 400 },
    ]);

    const wider = shape.reshape(points, 2, { x: 150, y: 500 });
    expect(wider.slice(0, 2)).toEqual(points.slice(0, 2));
    expect(wider[2]).toEqual({ x: 150, y: 500 });
  });

  it("works for a vertical line: the parallel lies beside it", () => {
    const vertical = [
      { x: 100, y: 100 },
      { x: 100, y: 300 },
      { x: 160, y: 200 },
    ];

    expect(parallelOf(vertical)).toEqual([
      { x: 160, y: 100 },
      { x: 160, y: 300 },
    ]);
  });
});

describe("text note", () => {
  it("is hit on its text, estimated before it was first drawn", () => {
    const note = drawing("textNote", { text: "breakout" });
    const shape = shapeOf("textNote")!;

    expect(shape.hits({ x: 130, y: 200 }, [{ x: 100, y: 200 }], pane, note)).toBe(true);
    expect(shape.hits({ x: 130, y: 230 }, [{ x: 100, y: 200 }], pane, note)).toBe(false);
    expect(shape.hits({ x: 90, y: 200 }, [{ x: 100, y: 200 }], pane, note)).toBe(false);
  });

  it("grows by a line for each line break", () => {
    const note = drawing("textNote", { text: "one\ntwo\nthree" });

    expect(shapeOf("textNote")!.hits({ x: 105, y: 236 }, [{ x: 100, y: 200 }], pane, note)).toBe(true);
  });
});

describe("shapes", () => {
  it("exist for every tool but the Fibonacci ones, which come with step 8", () => {
    const tools = ["trendLine", "ray", "horizontalLine", "verticalLine", "rectangle", "arrow", "textNote", "parallelChannel", "polyline", "waveLabel"] as const;
    for (const tool of tools) {
      expect(shapeOf(tool)).toBeDefined();
    }

    expect(shapeOf("fibonacciRetracement")).toBeUndefined();
    expect(shapeOf("fibonacciExtension")).toBeUndefined();
  });

  it("number their anchors as the Domain does (DrawingTools.AnchorCount)", () => {
    expect(shapeOf("horizontalLine")!.anchors).toBe(1);
    expect(shapeOf("trendLine")!.anchors).toBe(2);
    expect(shapeOf("parallelChannel")!.anchors).toBe(3);
  });
});

describe("polyline", () => {
  const points = [
    { x: 100, y: 300 },
    { x: 200, y: 200 },
    { x: 300, y: 260 },
  ];
  const mark = { degree: "minor", label: "1", side: "above" } as const;

  it("is ended by the user and needs two points", () => {
    expect(shapeOf("polyline")!.anchors).toBe(Number.POSITIVE_INFINITY);
    expect(shapeOf("polyline")!.minimum).toBe(2);
  });

  it("is hit on every segment, not beside it", () => {
    expect(hits("polyline", points, { x: 150, y: 252 })).toBe(true);
    expect(hits("polyline", points, { x: 250, y: 230 })).toBe(true);
    expect(hits("polyline", points, { x: 150, y: 200 })).toBe(false);
  });

  it("with its line hidden is hit only on its labels and points", () => {
    const hidden = { lineHidden: true, pointLabels: [{ point: 1, mark }] };

    expect(hits("polyline", points, { x: 150, y: 250 }, hidden)).toBe(false);
    expect(hits("polyline", points, { x: 200, y: 184 }, hidden)).toBe(true); // the label above point 1
    expect(hits("polyline", points, { x: 300, y: 262 }, hidden)).toBe(true); // point 2 itself
  });

  it("ignores labels it cannot read", () => {
    const broken = { lineHidden: true, pointLabels: [{ point: 1, mark: { degree: "huge", label: "1", side: "above" } }] };

    expect(hits("polyline", points, { x: 200, y: 184 }, broken)).toBe(false);
  });
});

describe("wave label", () => {
  const mark = (side: "above" | "below") => ({ wave: { degree: "intermediate", label: "3", side } });

  it("is hit on its text above the high, or below the low", () => {
    const high = [{ x: 200, y: 100 }];

    expect(hits("waveLabel", high, { x: 200, y: 84 }, mark("above"))).toBe(true);
    expect(hits("waveLabel", high, { x: 200, y: 116 }, mark("above"))).toBe(false);
    expect(hits("waveLabel", high, { x: 200, y: 116 }, mark("below"))).toBe(true);
  });

  it("has no handles: it is moved as a whole", () => {
    expect(shapeOf("waveLabel")!.handles([{ x: 1, y: 1 }], pane)).toEqual([]);
  });
});
