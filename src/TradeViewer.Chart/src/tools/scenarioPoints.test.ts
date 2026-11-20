import { beforeEach, describe, expect, it } from "vitest";
import { FakeSurface, HOUR, T0 } from "./fakeSurface";
import { keyOf, ScenarioPointInteraction, type ScenarioHandle, type ScenarioTool } from "./scenarioPoints";

const ONE_TWO: ScenarioTool = { code: "oneTwo", fields: ["waves.wave1Start", "waves.wave1End", "waves.wave2End"] };
const EXIT_AT_STOP: ScenarioTool = { code: "exitStop", fields: ["trade.exits"], kind: "stop" };

const handle = (field: string, bar: number, price: number, editable = true, index?: number): ScenarioHandle => ({
  field,
  index,
  time: T0 + bar * HOUR,
  price,
  editable,
});

describe("ScenarioPointInteraction", () => {
  let surface: FakeSurface;
  let events: string[];
  let interaction: ScenarioPointInteraction;

  beforeEach(() => {
    surface = new FakeSurface();
    events = [];
    interaction = new ScenarioPointInteraction(surface, {
      pointPlaced: (field, index, time, price, kind) => events.push(`placed ${field}${index === undefined ? "" : "#" + index} bar ${(time - T0) / HOUR} @${price}${kind ? " " + kind : ""}`),
      pointSelected: (key) => events.push(`selected ${key ?? "none"}`),
      scenarioToolChanged: (code) => events.push(`tool ${code ?? "pointer"}`),
    });
  });

  it("places the points of the 1-2 tool with three clicks, then returns to the pointer", () => {
    interaction.setTool(ONE_TWO);
    expect(interaction.nextField()).toBe("waves.wave1Start");

    expect(interaction.pointerDown(FakeSurface.point(10, 480))).toBe(true);
    expect(interaction.nextField()).toBe("waves.wave1End");
    interaction.pointerDown(FakeSurface.point(20, 530));
    interaction.pointerDown({ x: 304, y: 1000 - 495 }); // between bars: the whole bar under the pointer

    expect(events).toEqual([
      "tool oneTwo",
      "placed waves.wave1Start bar 10 @480",
      "placed waves.wave1End bar 20 @530",
      "tool pointer",
      "placed waves.wave2End bar 30 @495",
    ]);
    expect(interaction.tool()).toBeUndefined();
    expect(interaction.isBusy()).toBe(false);
  });

  it("an exit tool adds one exit with its kind", () => {
    interaction.setTool(EXIT_AT_STOP);
    interaction.pointerDown(FakeSurface.point(40, 470));

    expect(events.slice(1)).toEqual(["tool pointer", "placed trade.exits bar 40 @470 stop"]);
  });

  it("Escape stops a tool; the points it placed stay with Web", () => {
    interaction.setTool(ONE_TWO);
    interaction.pointerDown(FakeSurface.point(10, 480));

    expect(interaction.cancel()).toBe(true);
    expect(interaction.tool()).toBeUndefined();
    expect(events.at(-1)).toBe("tool pointer");
    expect(interaction.cancel()).toBe(false);
  });

  it("a dragged handle follows the pointer and reports where it was let go; it stays there until Web answers", () => {
    interaction.setPoints([handle("waves.wave1End", 20, 530), handle("waves.wave2End", 30, 495)]);

    expect(interaction.pointerDown(FakeSurface.point(20, 532))).toBe(true); // within reach of the handle
    interaction.pointerMove(FakeSurface.point(23, 540));
    expect(interaction.visible()[0]!.at).toEqual(FakeSurface.anchor(23, 540));
    expect(interaction.pointerUp()).toBe(true);

    expect(events).toEqual(["selected waves.wave1End", "placed waves.wave1End bar 23 @540"]);
    expect(interaction.visible()[0]!.at).toEqual(FakeSurface.anchor(23, 540));

    interaction.setPoints([handle("waves.wave1End", 22, 535), handle("waves.wave2End", 30, 495)]); // snapped by the server
    expect(interaction.visible()[0]!.at).toEqual(FakeSurface.anchor(22, 535));
    expect(interaction.selected()).toBe("waves.wave1End");
  });

  it("a click on a handle selects it without moving it", () => {
    interaction.setPoints([handle("trade.exits", 40, 470, true, 1)]);

    interaction.pointerDown(FakeSurface.point(40, 470));
    interaction.pointerMove({ x: 401, y: 1000 - 470 }); // less than a drag
    interaction.pointerUp();

    expect(events).toEqual(["selected trade.exits#1"]);
  });

  it("Escape while dragging puts the handle back and reports nothing", () => {
    interaction.setPoints([handle("wave2.aEnd", 25, 500)]);
    interaction.pointerDown(FakeSurface.point(25, 500));
    interaction.pointerMove(FakeSurface.point(28, 490));

    expect(interaction.cancel()).toBe(true);
    interaction.pointerUp();

    expect(interaction.visible()[0]!.at).toEqual(FakeSurface.anchor(25, 500));
    expect(events).toEqual(["selected wave2.aEnd"]);
  });

  it("points of another level have no handle and cannot be taken; a press beside the handles goes to the chart", () => {
    interaction.setPoints([handle("waves.wave2End", 30, 495, false), handle("wave2.aEnd", 25, 500)]);

    expect(interaction.visible().map((visible) => keyOf(visible.handle))).toEqual(["wave2.aEnd"]);
    expect(interaction.pointerDown(FakeSurface.point(30, 495))).toBe(false);
    interaction.pointerDown(FakeSurface.point(25, 500));
    interaction.pointerUp();
    expect(interaction.pointerDown(FakeSurface.point(60, 400))).toBe(false);
    expect(events).toEqual(["selected wave2.aEnd", "selected none"]);
  });

  it("a selected point that is gone after an update is no longer selected", () => {
    interaction.setPoints([handle("trade.add", 45, 515)]);
    interaction.pointerDown(FakeSurface.point(45, 515));
    interaction.pointerUp();

    interaction.setPoints([]);

    expect(interaction.selected()).toBeUndefined();
    expect(events).toEqual(["selected trade.add", "selected none"]);
  });

  it("the cursor shows a crosshair while placing and a pointer over a handle", () => {
    interaction.setPoints([handle("trade.entry", 41, 512)]);

    expect(interaction.cursorAt(FakeSurface.point(41, 512))).toBe("pointer");
    expect(interaction.cursorAt(FakeSurface.point(70, 300))).toBeUndefined();
    interaction.setTool(ONE_TWO);
    expect(interaction.cursorAt(FakeSurface.point(70, 300))).toBe("crosshair");
  });
});
