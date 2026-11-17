import { beforeEach, describe, expect, it } from "vitest";
import { defaultStyle, type Drawing, type ToolCode } from "../primitives/drawing";
import { FakeSurface, HOUR, T0 } from "./fakeSurface";
import { DrawingInteraction } from "./interaction";

const at = FakeSurface.point;
const anchor = FakeSurface.anchor;

/** The events of the interaction in their order, e.g. "created d1", "tool -". */
let events: string[];
let reported: Drawing[];
let surface: FakeSurface;
let interaction: DrawingInteraction;

beforeEach(() => {
  events = [];
  reported = [];
  surface = new FakeSurface();
  let next = 0;
  interaction = new DrawingInteraction(
    surface,
    {
      drawingCreated: (drawing) => {
        events.push("created " + drawing.id);
        reported.push(drawing);
      },
      drawingChanged: (drawing) => {
        events.push("changed " + drawing.id);
        reported.push(drawing);
      },
      selectionChanged: (id) => events.push("selected " + (id ?? "-")),
      toolChanged: (tool) => events.push("tool " + (tool ?? "-")),
    },
    () => "d" + ++next,
  );
  interaction.setOptions({ magnet: false });
});

function line(id: string, from: [number, number], to: [number, number], locked = false): Drawing {
  return { id, tool: "trendLine", locked, anchors: [anchor(...from), anchor(...to)], style: defaultStyle("trendLine") };
}

/** A press, moves in a few steps and the release, as the pointer input passes them on. */
function drag(from: { x: number; y: number }, to: { x: number; y: number }): boolean {
  const taken = interaction.pointerDown(from);
  for (let step = 1; step <= 4; step++) {
    interaction.pointerMove({ x: from.x + ((to.x - from.x) * step) / 4, y: from.y + ((to.y - from.y) * step) / 4 });
  }

  interaction.pointerUp();
  return taken;
}

function click(point: { x: number; y: number }): boolean {
  const taken = interaction.pointerDown(point);
  interaction.pointerUp();
  return taken;
}

describe("placing", () => {
  it("places a trend line with click, move, click and goes back to the pointer", () => {
    interaction.setTool("trendLine");
    expect(click(at(10, 400))).toBe(true);
    interaction.pointerMove(at(15, 450));

    expect(interaction.preview()?.anchors).toEqual([anchor(10, 400), anchor(15, 450)]); // the preview follows the pointer
    expect(reported).toEqual([]);

    click(at(20, 500));

    expect(reported).toEqual([{ id: "d1", tool: "trendLine", locked: false, anchors: [anchor(10, 400), anchor(20, 500)], style: defaultStyle("trendLine") }]);
    expect(events).toEqual(["tool trendLine", "created d1", "tool -", "selected d1"]);
    expect(interaction.tool()).toBeUndefined();
    expect(interaction.preview()).toBeUndefined();
    expect(interaction.isBusy()).toBe(false);
  });

  it.each<[ToolCode, number]>([
    ["horizontalLine", 1],
    ["verticalLine", 1],
    ["textNote", 1],
    ["ray", 2],
    ["arrow", 2],
    ["rectangle", 2],
    ["parallelChannel", 3],
  ])("a %s is finished after %i clicks", (tool, clicks) => {
    interaction.setTool(tool);
    for (let index = 0; index < clicks; index++) {
      click(at(10 + index * 5, 400 + index * 20));
    }

    expect(reported).toHaveLength(1);
    expect(reported[0]!.tool).toBe(tool);
    expect(reported[0]!.anchors).toHaveLength(clicks);
    expect(reported[0]!.style).toEqual(defaultStyle(tool));
  });

  it("gives a new text note a text, because a note without one would be refused", () => {
    interaction.setTool("textNote");
    click(at(10, 400));

    expect(reported[0]!.text).toBe("Text");
  });

  it("takes the style chosen for the tool", () => {
    const style = { color: "#F23645", width: 3, lineStyle: "dashed" as const };
    interaction.setTool("ray", style);
    click(at(1, 400));
    click(at(2, 400));

    expect(reported[0]!.style).toEqual(style);
  });

  it("puts anchors after the last candle on whole bars (DR3)", () => {
    interaction.setTool("trendLine");
    click(at(90, 400));
    click({ x: 1234, y: 450 }); // bar 123.4, beyond the 100 candles

    expect(reported[0]!.anchors[1]).toEqual({ time: T0 + 123 * HOUR, price: 550 });
  });

  it("snaps to the open, high, low or close of the candle with the magnet", () => {
    interaction.setOptions({ magnet: true });
    interaction.setTool("trendLine");
    click({ x: 100, y: 1000 - 523 }); // near the high 520
    click({ x: 200, y: 1000 - 484 }); // near the low 480

    expect(reported[0]!.anchors).toEqual([anchor(10, 520), anchor(20, 480)]);
  });

  it("does not snap beside the candles, where there is nothing to snap to", () => {
    interaction.setOptions({ magnet: true });
    interaction.setTool("horizontalLine");
    click({ x: 1500, y: 1000 - 523 });

    expect(reported[0]!.anchors).toEqual([anchor(150, 523)]);
  });

  it("gives up with Escape and goes back to the pointer", () => {
    interaction.setTool("parallelChannel");
    click(at(10, 400));
    click(at(20, 420));

    expect(interaction.cancel()).toBe(true);
    expect(interaction.preview()).toBeUndefined();
    expect(interaction.tool()).toBeUndefined();
    expect(reported).toEqual([]);
    expect(events).toEqual(["tool parallelChannel", "tool -"]);
    expect(interaction.cancel()).toBe(false); // nothing left to cancel
  });

  it("takes every press while placing, so the chart does not pan", () => {
    interaction.setTool("trendLine");

    expect(interaction.pointerDown(at(50, 300))).toBe(true);
    expect(interaction.isBusy()).toBe(true);
  });

  it("knows no tool it cannot draw yet", () => {
    interaction.setTool("fibonacciRetracement");

    expect(interaction.tool()).toBeUndefined();
    expect(events).toEqual(["tool -"]);
  });
});

describe("selecting", () => {
  beforeEach(() => {
    interaction.setDrawings([line("a", [10, 400], [30, 400]), line("b", [10, 400], [30, 600])]);
  });

  it("selects the topmost drawing under the pointer and takes the press", () => {
    expect(click(at(20, 400))).toBe(true);
    expect(interaction.selected()).toBe("a");

    expect(click(at(20, 500))).toBe(true); // on b only
    expect(interaction.selected()).toBe("b");
    expect(events).toEqual(["selected a", "selected b"]);
  });

  it("leaves a press beside every drawing to the chart and clears the selection", () => {
    click(at(20, 400));

    expect(interaction.pointerDown(at(60, 100))).toBe(false);
    expect(interaction.selected()).toBeUndefined();
  });

  it("drops the selection when its drawing goes", () => {
    interaction.select("a");
    interaction.setDrawings([line("b", [10, 400], [30, 600])]);

    expect(interaction.selected()).toBeUndefined();
    expect(events).toEqual(["selected a", "selected -"]);
  });

  it("shows the move cursor on a drawing, the pointer on a handle of the selected one", () => {
    interaction.select("a");

    expect(interaction.cursorAt(at(20, 400))).toBe("move");
    expect(interaction.cursorAt(at(30, 402))).toBe("pointer");
    expect(interaction.cursorAt(at(60, 100))).toBeUndefined();
  });
});

describe("moving and reshaping", () => {
  beforeEach(() => {
    interaction.setDrawings([line("a", [10, 400], [30, 500])]);
  });

  it("moves the whole drawing by whole bars and reports it once on release", () => {
    expect(drag(at(20, 450), { x: 254, y: 1000 - 470 })).toBe(true); // 5.4 bars right, 20 up

    expect(reported).toEqual([{ ...line("a", [15, 420], [35, 520]) }]);
    expect(events).toEqual(["selected a", "changed a"]);
  });

  it("reports nothing for a click without a move", () => {
    click(at(20, 450));

    expect(reported).toEqual([]);
  });

  it("puts the drawing back on Escape while dragging", () => {
    interaction.pointerDown(at(20, 450));
    interaction.pointerMove(at(40, 300));

    expect(interaction.cancel()).toBe(true);
    interaction.pointerUp();

    expect(interaction.all()).toEqual([line("a", [10, 400], [30, 500])]);
    expect(reported).toEqual([]);
  });

  it("reshapes the selected drawing by a handle; the other anchor stays exactly as it was", () => {
    interaction.select("a");
    drag(at(30, 500), at(40, 650));

    expect(reported).toEqual([line("a", [10, 400], [40, 650])]);
  });

  it("snaps the dragged handle with the magnet", () => {
    interaction.setOptions({ magnet: true });
    interaction.select("a");
    drag(at(30, 500), { x: 400, y: 1000 - 517 });

    expect(reported[0]!.anchors).toEqual([anchor(10, 400), anchor(40, 520)]);
  });

  it("selects a locked drawing but never moves or reshapes it; the press on it does not pan the chart", () => {
    interaction.setDrawings([line("a", [10, 400], [30, 500], true)]);

    expect(drag(at(20, 450), at(40, 300))).toBe(true);
    interaction.select("a");
    drag(at(30, 500), at(50, 650));

    expect(interaction.selected()).toBe("a");
    expect(interaction.all()).toEqual([line("a", [10, 400], [30, 500], true)]);
    expect(reported).toEqual([]);
    expect(interaction.cursorAt(at(20, 450))).toBe("default");
  });

  it("keeps the dragged drawing when the drawings are replaced meanwhile", () => {
    interaction.pointerDown(at(20, 450));
    interaction.pointerMove(at(25, 450));
    interaction.setDrawings([line("a", [10, 400], [30, 500]), line("b", [50, 100], [60, 100])]);
    interaction.pointerUp();

    expect(reported).toEqual([line("a", [15, 400], [35, 500])]);
    expect(interaction.all()).toHaveLength(2);
  });

  it("moves a rectangle's free corner: time of one anchor, price of the other", () => {
    const box: Drawing = { id: "r", tool: "rectangle", locked: false, anchors: [anchor(10, 400), anchor(30, 500)], style: defaultStyle("rectangle") };
    interaction.setDrawings([box]);
    interaction.select("r");
    drag(at(10, 500), at(5, 550)); // handle 2: the corner (time of a, price of b)

    expect(reported[0]!.anchors).toEqual([anchor(5, 400), anchor(30, 550)]);
  });
});

describe("hidden drawings (DR6)", () => {
  it("draws, hits and places nothing", () => {
    interaction.setDrawings([line("a", [10, 400], [30, 400])]);
    interaction.select("a");
    interaction.setOptions({ hidden: true });

    expect(interaction.visible()).toEqual([]);
    expect(interaction.selected()).toBeUndefined();
    expect(interaction.pointerDown(at(20, 400))).toBe(false);

    interaction.setTool("trendLine");
    expect(interaction.tool()).toBeUndefined();

    interaction.setOptions({ hidden: false });
    expect(interaction.visible()).toHaveLength(1);
  });
});

describe("polyline", () => {
  it("takes points until a double-click on the last one ends it", () => {
    interaction.setTool("polyline");
    click(at(10, 400));
    click(at(20, 450));
    click(at(30, 420));
    click(at(30, 420)); // the second click of the double-click

    expect(reported).toHaveLength(1);
    expect(reported[0]!.anchors).toEqual([anchor(10, 400), anchor(20, 450), anchor(30, 420)]);
    expect(interaction.tool()).toBeUndefined();
    expect(interaction.selected()).toBe("d1");
  });

  it("is ended by Enter once it has two points, and Enter before that does nothing", () => {
    interaction.setTool("polyline");
    click(at(10, 400));

    expect(interaction.finish()).toBe(false);
    expect(interaction.isBusy()).toBe(true);

    click(at(20, 450));
    expect(interaction.finish()).toBe(true);
    expect(reported[0]!.anchors).toHaveLength(2);
  });

  it("is dropped by Escape", () => {
    interaction.setTool("polyline");
    click(at(10, 400));
    click(at(20, 450));
    interaction.cancel();

    expect(reported).toEqual([]);
    expect(interaction.tool()).toBeUndefined();
  });

  it("shows the pointer as its next point while placing", () => {
    interaction.setTool("polyline");
    click(at(10, 400));
    click(at(20, 450));
    interaction.pointerMove(at(35, 500));

    expect(interaction.preview()?.anchors).toEqual([anchor(10, 400), anchor(20, 450), anchor(35, 500)]);
  });
});

describe("wave labels (DR5)", () => {
  const minor = (label: string) => ({ wave: { degree: "minor", label } });

  it("are placed with one click, above or below the candle by the pointer, without a price", () => {
    interaction.setTool("waveLabel", undefined, minor("1"));
    click(at(10, 530)); // above the middle of the candle (500)
    interaction.setTool("waveLabel", undefined, minor("2"));
    click(at(12, 470));

    expect(reported.map((drawing) => [drawing.anchors, drawing["wave"]])).toEqual([
      [[{ time: T0 + 10 * HOUR, price: null }], { degree: "minor", label: "1", side: "above" }],
      [[{ time: T0 + 12 * HOUR, price: null }], { degree: "minor", label: "2", side: "below" }],
    ]);
  });

  it("keep the tool after each label, so a count is placed label after label", () => {
    interaction.setTool("waveLabel", undefined, minor("1"));
    click(at(10, 530));

    expect(interaction.tool()).toBe("waveLabel");
    expect(interaction.selected()).toBeUndefined();
    expect(events).toEqual(["tool waveLabel", "created d1"]);
  });

  it("are not placed beside the candles, nor without a chosen label", () => {
    interaction.setTool("waveLabel", undefined, minor("1"));
    click({ x: 1500, y: 470 });
    interaction.setTool("waveLabel");
    click(at(10, 530));

    expect(reported).toEqual([]);
  });

  it("show where the next label would go", () => {
    interaction.setTool("waveLabel", undefined, minor("4"));
    interaction.pointerMove(at(15, 460));

    expect(interaction.preview()).toMatchObject({ tool: "waveLabel", anchors: [{ time: T0 + 15 * HOUR, price: null }], wave: { label: "4", side: "below" } });
  });

  it("move to another candle and to the side the pointer is on", () => {
    const label: Drawing = {
      id: "w",
      tool: "waveLabel",
      locked: false,
      anchors: [{ time: T0 + 10 * HOUR, price: null }],
      style: defaultStyle("waveLabel"),
      wave: { degree: "minor", label: "3", side: "above" },
    };
    interaction.setDrawings([label]);

    // The label sits above the high (520) of bar 10; drag it 3 bars on and below the candle.
    drag({ x: 100, y: 1000 - 530 }, { x: 130, y: 1000 - 470 });

    expect(reported).toEqual([{ ...label, anchors: [{ time: T0 + 13 * HOUR, price: null }], wave: { degree: "minor", label: "3", side: "below" } }]);
  });
});
