import { describe, expect, it } from "vitest";
import { parseGroups, parseShape } from "./overlay";
import { roleColour } from "./roleColours";

describe("parseGroups", () => {
  it("reads the records of the Api: ISO times become Unix seconds, nulls become missing", () => {
    const groups = parseGroups([
      {
        key: "waves",
        title: "Waves 1 and 2",
        kind: "waves",
        timeframe: "1d",
        level: "context",
        shapes: [
          { type: "line", role: "wave", points: [{ time: "2024-10-25T00:00:00Z", price: 0.486 }, { time: "2024-10-29T00:00:00+00:00", price: 0.5305 }], weight: "strong", text: null },
          { type: "label", role: "wave", time: "2024-10-29T00:00:00Z", price: 0.5305, text: "1", side: "above", detail: "1: 0.5305" },
        ],
      },
    ]);

    expect(groups).toHaveLength(1);
    expect(groups[0]!.level).toBe("context");
    expect(groups[0]!.shapes[0]!.points).toEqual([
      { time: 1_729_814_400, price: 0.486 },
      { time: 1_730_160_000, price: 0.5305 },
    ]);
    expect(groups[0]!.shapes[0]!.weight).toBe("strong");
    expect(groups[0]!.shapes[0]!.text).toBeUndefined();
    expect(groups[0]!.shapes[1]!.side).toBe("above");
  });

  it("drops shapes of unknown types and shapes it cannot read, as a client must (ewts-interface.md)", () => {
    const shapes = [
      { type: "spiral", role: "wave", time: "2024-10-29T00:00:00Z", price: 1 },
      { type: "line", role: "wave", points: [{ time: "2024-10-29T00:00:00Z", price: 1 }] },
      { type: "marker", role: "entry", time: "not a time", price: 1 },
      { type: "level", role: "stop", price: 1 },
      { type: "level", role: "stop", price: 1, from: "2024-10-29T00:00:00Z", to: null },
    ];

    const parsed = shapes.map(parseShape);

    expect(parsed.map((shape) => shape?.type)).toEqual([undefined, undefined, undefined, undefined, "level"]);
    expect(parsed[4]!.to).toBeUndefined(); // open to the right
    expect(parseGroups(null)).toEqual([]);
  });
});

describe("roleColour", () => {
  it("takes the colours of OV6 and draws an unknown role gray", () => {
    expect(roleColour("wave")).toBe("#2962FF");
    expect(roleColour("lowerDegree")).toBe("#9C27B0");
    expect(roleColour("target")).toBe("#64B5F6");
    expect(roleColour("support")).toBe("#FDD835");
    expect(roleColour("somethingNew")).toBe("#787B86");
  });
});
