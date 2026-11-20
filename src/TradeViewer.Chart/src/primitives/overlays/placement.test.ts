import { describe, expect, it } from "vitest";
import type { OverlayShape } from "./overlay";
import { hits, LABEL_OFFSET, place, type Projector } from "./placement";

/** One pixel per second from time 0, y = 1000 - price; times before 0 and prices above 1000 cannot be placed. */
const projector: Projector = {
  width: 800,
  height: 600,
  x: (time) => (time < 0 ? undefined : time),
  y: (price) => (price > 1000 ? undefined : 1000 - price),
};

const shape = (fields: Partial<OverlayShape> & { type: string }): OverlayShape => ({ role: "neutral", ...fields });

describe("place", () => {
  it("puts a line through its points", () => {
    const placed = place(shape({ type: "line", points: [{ time: 10, price: 900 }, { time: 50, price: 950 }, { time: 90, price: 920 }] }), projector);

    expect(placed).toEqual({ kind: "polyline", points: [{ x: 10, y: 100 }, { x: 50, y: 50 }, { x: 90, y: 80 }] });
  });

  it("puts a label's text above or below its point", () => {
    expect(place(shape({ type: "label", time: 40, price: 900, text: "1", side: "above" }), projector)).toMatchObject({ text: { x: 40, y: 100 - LABEL_OFFSET } });
    expect(place(shape({ type: "label", time: 40, price: 900, text: "2", side: "below" }), projector)).toMatchObject({ text: { x: 40, y: 100 + LABEL_OFFSET } });
  });

  it("runs a level from its time to its end, or to the right edge when it is open", () => {
    expect(place(shape({ type: "level", price: 700, from: 100, to: 300 }), projector)).toEqual({ kind: "level", fromX: 100, toX: 300, y: 300 });
    expect(place(shape({ type: "level", price: 700, from: 100 }), projector)).toEqual({ kind: "level", fromX: 100, toX: 800, y: 300 });
  });

  it("puts a zone between its prices whatever their order", () => {
    expect(place(shape({ type: "zone", lower: 600, upper: 700, from: 100 }), projector)).toEqual({ kind: "zone", fromX: 100, toX: 800, top: 300, bottom: 400 });
  });

  it("places nothing when a point cannot be placed", () => {
    expect(place(shape({ type: "line", points: [{ time: -5, price: 900 }, { time: 50, price: 950 }] }), projector)).toBeUndefined();
    expect(place(shape({ type: "marker", time: 40, price: 2000 }), projector)).toBeUndefined();
    expect(place(shape({ type: "unknown", time: 40, price: 900 }), projector)).toBeUndefined();
  });
});

describe("hits", () => {
  it("hits a line near any of its segments", () => {
    const line = place(shape({ type: "line", points: [{ time: 0, price: 900 }, { time: 100, price: 900 }, { time: 100, price: 800 }] }), projector)!;

    expect(hits(line, { x: 50, y: 103 })).toBe(true);
    expect(hits(line, { x: 102, y: 150 })).toBe(true);
    expect(hits(line, { x: 50, y: 120 })).toBe(false);
  });

  it("hits a marker, a label and a level close to them, a zone inside it", () => {
    expect(hits(place(shape({ type: "marker", time: 40, price: 900 }), projector)!, { x: 47, y: 104 })).toBe(true);
    expect(hits(place(shape({ type: "marker", time: 40, price: 900 }), projector)!, { x: 60, y: 100 })).toBe(false);
    expect(hits(place(shape({ type: "label", time: 40, price: 900, side: "above" }), projector)!, { x: 44, y: 88 })).toBe(true);
    expect(hits(place(shape({ type: "level", price: 700, from: 100, to: 300 }), projector)!, { x: 200, y: 304 })).toBe(true);
    expect(hits(place(shape({ type: "level", price: 700, from: 100, to: 300 }), projector)!, { x: 340, y: 300 })).toBe(false);
    expect(hits(place(shape({ type: "zone", lower: 600, upper: 700, from: 100 }), projector)!, { x: 500, y: 350 })).toBe(true);
    expect(hits(place(shape({ type: "verticalLine", time: 70 }), projector)!, { x: 73, y: 5 })).toBe(true);
  });
});
