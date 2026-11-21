import { beforeEach, describe, expect, it } from "vitest";
import { FakeSurface, HOUR, T0 } from "./fakeSurface";
import { RangeSelection } from "./rangeSelection";

describe("RangeSelection", () => {
  let surface: FakeSurface;
  let events: string[];
  let selection: RangeSelection;

  beforeEach(() => {
    surface = new FakeSurface();
    events = [];
    const rangeSurface = {
      anchorAt: surface.anchorAt.bind(surface),
      xOf: (time: number) => ((time - T0) / HOUR) * 10,
      redraw: () => surface.redraw(),
    };
    selection = new RangeSelection(rangeSurface, {
      rangeSelected: (from, to) => events.push(`range bar ${(from - T0) / HOUR} to ${(to - T0) / HOUR}`),
      rangeToolChanged: (on) => events.push(`tool ${on ? "on" : "off"}`),
    });
  });

  it("takes no press while it is not chosen, so the chart pans as before", () => {
    expect(selection.isBusy()).toBe(false);
    expect(selection.pointerDown(FakeSurface.point(10, 500))).toBe(false);
    expect(events).toEqual([]);
  });

  it("drags a band by whole candles and reports its first and last candle, then puts the tool away", () => {
    selection.setActive(true);
    expect(selection.isBusy()).toBe(true);

    expect(selection.pointerDown(FakeSurface.point(40, 500))).toBe(true);
    selection.pointerMove({ x: 254, y: 400 }); // between bars 25 and 26: the bar under the pointer
    expect(selection.band()).toEqual({ from: T0 + 25 * HOUR, to: T0 + 40 * HOUR });
    selection.pointerMove(FakeSurface.point(60, 300));
    expect(selection.pointerUp()).toBe(true);

    expect(events).toEqual(["tool on", "tool off", "range bar 40 to 60"]); // the rail is free again before Web navigates
    expect(selection.isActive()).toBe(false);
    expect(selection.band()).toBeUndefined();
  });

  it("reports the earlier candle first when dragged to the left", () => {
    selection.setActive(true);
    selection.pointerDown(FakeSurface.point(70, 500));
    selection.pointerMove(FakeSurface.point(30, 500));
    selection.pointerUp();

    expect(events).toContain("range bar 30 to 70");
  });

  it("Escape removes the band and puts the tool away without a range", () => {
    selection.setActive(true);
    selection.pointerDown(FakeSurface.point(40, 500));
    selection.pointerMove(FakeSurface.point(50, 500));

    expect(selection.cancel()).toBe(true);

    expect(events).toEqual(["tool on", "tool off"]);
    expect(selection.band()).toBeUndefined();
    expect(selection.cancel()).toBe(false);
  });

  it("choosing the tool again changes nothing and putting it away reports it once", () => {
    selection.setActive(true);
    selection.setActive(true);
    selection.setActive(false);
    selection.setActive(false);

    expect(events).toEqual(["tool on", "tool off"]);
  });
});
