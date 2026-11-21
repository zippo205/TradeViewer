import { beforeEach, describe, expect, it } from "vitest";
import { OverlaySelection, type OverlaySelectionLayer } from "./overlaySelection";

/** A layer with one form at x 0..100 (group wave1) and a trade marker at x 200 (key trade.exits#0). */
class FakeLayer implements OverlaySelectionLayer {
  current: { group: string; key?: string } | undefined;

  shapeAt(point: { x: number; y: number }) {
    if (point.x >= 0 && point.x <= 100) return { group: "wave1" };
    if (Math.abs(point.x - 200) <= 5) return { group: "trade", key: "trade.exits#0" };
    return undefined;
  }

  select(group: string | undefined, key?: string): void {
    this.current = group === undefined ? undefined : { group, key };
  }

  selected() {
    return this.current;
  }
}

describe("OverlaySelection", () => {
  let layer: FakeLayer;
  let events: string[];
  let selection: OverlaySelection;

  beforeEach(() => {
    layer = new FakeLayer();
    events = [];
    selection = new OverlaySelection(layer, { overlayClicked: (group, key) => events.push(`${group ?? "none"} ${key ?? "-"}`) });
  });

  it("a click on a form selects the whole form and keeps the chart from panning", () => {
    expect(selection.pointerDown({ x: 50, y: 10 })).toBe(true);

    expect(layer.selected()).toEqual({ group: "wave1", key: undefined });
    expect(events).toEqual(["wave1 -"]);
  });

  it("a click on a marker of the trade selects that point", () => {
    selection.pointerDown({ x: 201, y: 10 });

    expect(layer.selected()).toEqual({ group: "trade", key: "trade.exits#0" });
  });

  it("a click elsewhere clears the selection once and is passed on to the chart", () => {
    selection.pointerDown({ x: 50, y: 10 });

    expect(selection.pointerDown({ x: 400, y: 10 })).toBe(false);
    expect(selection.pointerDown({ x: 400, y: 10 })).toBe(false);

    expect(layer.selected()).toBeUndefined();
    expect(events).toEqual(["wave1 -", "none -"]);
  });

  it("Escape clears a selection and is not taken without one", () => {
    selection.pointerDown({ x: 50, y: 10 });

    expect(selection.cancel()).toBe(true);
    expect(selection.cancel()).toBe(false);
    expect(events).toEqual(["wave1 -", "none -"]);
  });
});
