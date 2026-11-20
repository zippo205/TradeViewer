import type { Anchor } from "../primitives/drawing";
import { hasDragged } from "./drag";
import type { Point } from "./geometry";

// The named points of an expert scenario in the editor (SC10, decision 0012): handles over the shapes the Api builds
// from them. The module only reports where a point was placed or let go; the server snaps it (architecture chapter
// 8) and Web gives the points back with setScenarioPoints. No call to Blazor while a point is dragged (NF3).

/** A named point as Web gives it: its field in the file, its position, and whether it can be moved on this level. */
export interface ScenarioHandle {
  /** The field of the point (waves.wave1Start, wave1.iEnd, trade.exits, ...). */
  readonly field: string;
  /** The position of a stop move or an exit. */
  readonly index?: number;
  readonly time: number;
  readonly price: number;
  /** False for a point of another level: it is drawn by the overlay, it has no handle here (decision 0012). */
  readonly editable: boolean;
  /** The colour of its role (OV6), for the ring of the handle. */
  readonly colour?: string;
}

/**
 * A point tool: the fields it places one after the other ("1-2": wave 1 start, wave 1 end, wave 2 end), and for an
 * exit its kind (target or stop). A field of a list (trade.stopMoves, trade.exits) adds an item.
 */
export interface ScenarioTool {
  readonly code: string;
  readonly fields: readonly string[];
  readonly kind?: string;
}

/** What the point interaction reports. */
export interface ScenarioPointEvents {
  /** A point placed with a tool (index undefined: a new item of a list) or let go after a drag, where the pointer was. */
  pointPlaced(field: string, index: number | undefined, time: number, price: number, kind: string | undefined): void;
  /** The selected handle: "<field>" or "<field>#<index>", undefined when none. */
  pointSelected(key: string | undefined): void;
  /** The point tool in use; undefined after its last point was placed or it was cancelled. */
  scenarioToolChanged(code: string | undefined): void;
}

/** What the interaction needs of the chart: pixels to whole-bar anchors and back, without the magnet, and a new frame. */
export interface PointSurface {
  pointOf(anchor: Anchor): Point | undefined;
  anchorAt(point: Point, magnet: boolean): Anchor | undefined;
  redraw(): void;
}

/** How near the pointer must come to a handle to take it, in pixels; the handle is drawn with radius 5. */
export const POINT_HIT = 8;

interface Placing {
  readonly tool: ScenarioTool;
  next: number;
}

interface Dragging {
  readonly handle: ScenarioHandle;
  readonly from: Point;
  moved: boolean;
}

export function keyOf(handle: { readonly field: string; readonly index?: number }): string {
  return handle.index === undefined ? handle.field : handle.field + "#" + handle.index;
}

export class ScenarioPointInteraction {
  private handles: readonly ScenarioHandle[] = [];
  private placing: Placing | undefined;
  private dragging: Dragging | undefined;
  /** Where a dragged or just dropped point is drawn until Web gives the snapped points. */
  private pending: { key: string; anchor: Anchor } | undefined;
  private selectedKey: string | undefined;

  constructor(
    private readonly surface: PointSurface,
    private readonly events: Partial<ScenarioPointEvents>,
  ) {}

  /** The points of the scenario; a dropped point is drawn where Web says from now on. */
  setPoints(handles: readonly ScenarioHandle[]): void {
    this.handles = handles;
    this.pending = undefined;
    if (this.selectedKey !== undefined && !handles.some((handle) => handle.editable && keyOf(handle) === this.selectedKey)) {
      this.select(undefined);
    }

    this.surface.redraw();
  }

  /** Chooses a point tool, or the pointer with undefined. Placing starts with the first field of the tool. */
  setTool(tool: ScenarioTool | undefined): void {
    this.dragging = undefined;
    this.placing = tool === undefined || tool.fields.length === 0 ? undefined : { tool, next: 0 };
    this.events.scenarioToolChanged?.(this.placing?.tool.code);
    this.surface.redraw();
  }

  tool(): string | undefined {
    return this.placing?.tool.code;
  }

  /** The field the tool in use places next, for the hint of the page. */
  nextField(): string | undefined {
    return this.placing === undefined ? undefined : this.placing.tool.fields[this.placing.next];
  }

  selected(): string | undefined {
    return this.selectedKey;
  }

  /** The editable handles where they are drawn now: a dragged one follows the pointer. */
  visible(): { readonly handle: ScenarioHandle; readonly at: Anchor; readonly selected: boolean }[] {
    return this.handles
      .filter((handle) => handle.editable)
      .map((handle) => {
        const key = keyOf(handle);
        const at = this.pending?.key === key ? this.pending.anchor : { time: handle.time, price: handle.price };
        return { handle, at, selected: key === this.selectedKey };
      });
  }

  isBusy(): boolean {
    return this.placing !== undefined || this.dragging !== undefined;
  }

  cursorAt(point: Point): string | undefined {
    if (this.placing !== undefined) {
      return "crosshair";
    }

    return this.hitAt(point) !== undefined ? "pointer" : undefined;
  }

  /** The editable handle under the point, the topmost (last) first. */
  hitAt(point: Point): ScenarioHandle | undefined {
    const visible = this.visible();
    for (let i = visible.length - 1; i >= 0; i--) {
      const at = this.surface.pointOf(visible[i]!.at);
      if (at !== undefined && Math.hypot(at.x - point.x, at.y - point.y) <= POINT_HIT) {
        return visible[i]!.handle;
      }
    }

    return undefined;
  }

  /** True: the module takes the press (placing, or a handle), the chart must not pan. */
  pointerDown(point: Point): boolean {
    const placing = this.placing;
    if (placing !== undefined) {
      const anchor = this.surface.anchorAt(point, false);
      if (anchor !== undefined && anchor.price !== null) {
        const field = placing.tool.fields[placing.next]!;
        placing.next++;
        if (placing.next >= placing.tool.fields.length) {
          this.placing = undefined;
          this.events.scenarioToolChanged?.(undefined);
        }

        this.events.pointPlaced?.(field, undefined, anchor.time, anchor.price, placing.tool.kind);
        this.surface.redraw();
      }

      return true;
    }

    const handle = this.hitAt(point);
    if (handle === undefined) {
      if (this.selectedKey !== undefined) {
        this.select(undefined);
      }

      return false;
    }

    this.select(keyOf(handle));
    this.dragging = { handle, from: point, moved: false };
    return true;
  }

  pointerMove(point: Point): boolean {
    const dragging = this.dragging;
    if (dragging === undefined) {
      return this.placing !== undefined;
    }

    if (!dragging.moved && !hasDragged(dragging.from, point)) {
      return true;
    }

    const anchor = this.surface.anchorAt(point, false);
    if (anchor !== undefined && anchor.price !== null) {
      dragging.moved = true;
      this.pending = { key: keyOf(dragging.handle), anchor };
      this.surface.redraw();
    }

    return true;
  }

  /** Letting go of a moved handle reports where it is; it stays there until Web gives the snapped points. */
  pointerUp(): boolean {
    const dragging = this.dragging;
    if (dragging === undefined) {
      return false;
    }

    this.dragging = undefined;
    const pending = this.pending;
    if (dragging.moved && pending !== undefined && pending.anchor.price !== null) {
      this.events.pointPlaced?.(dragging.handle.field, dragging.handle.index, pending.anchor.time, pending.anchor.price, undefined);
    }

    return true;
  }

  /** Escape: a drag goes back, a tool stops (the points it placed stay). */
  cancel(): boolean {
    if (this.dragging !== undefined) {
      this.dragging = undefined;
      this.pending = undefined;
      this.surface.redraw();
      return true;
    }

    if (this.placing !== undefined) {
      this.setTool(undefined);
      return true;
    }

    return false;
  }

  finish(): boolean {
    return false;
  }

  select(key: string | undefined): void {
    if (this.selectedKey === key) {
      return;
    }

    this.selectedKey = key;
    this.events.pointSelected?.(key);
    this.surface.redraw();
  }
}
