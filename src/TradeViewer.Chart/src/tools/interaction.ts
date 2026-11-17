import { defaultStyle, NEW_NOTE_TEXT, type Anchor, type Drawing, type DrawingStyle, type ToolCode } from "../primitives/drawing";
import { HANDLE_HIT, type PaneSize } from "../primitives/shape";
import { shapeOf } from "../primitives/shapes";
import { hasDragged, movedAnchors, type AnchorStart } from "./drag";
import { labelsOf } from "../primitives/shapes/polyline";
import { WAVE_DEGREES, type WaveDegree, type WaveSide } from "../primitives/waveNotation";
import { patternLabels, withSides } from "../primitives/wavePatterns";
import type { Point } from "./geometry";

/** What the interaction needs of the chart: conversions between pixels and anchors, and a new frame. */
export interface DrawingSurface {
  /**
   * The anchors of the drawing in pixels of the pane; undefined when one of them cannot be placed. The point of a
   * wave label is the high (above) or low (below) of the candle that contains its time.
   */
  pointsOf(drawing: Drawing): Point[] | undefined;
  /** One anchor with a price in pixels of the pane. */
  pointOf(anchor: Anchor): Point | undefined;
  /** Whether the point is above or below the middle of the candle under it; undefined beside the candles. */
  sideAt(point: Point): WaveSide | undefined;
  /** The anchor at a point: the time of a whole bar and the price at the height, or the magnet's price. */
  anchorAt(point: Point, magnet: boolean): Anchor | undefined;
  /** The whole bars between two horizontal positions; undefined without candles. */
  barsBetween(fromX: number, toX: number): number | undefined;
  indexOf(time: number): number | undefined;
  timeOf(index: number): number | undefined;
  priceAt(y: number): number | undefined;
  paneSize(): PaneSize;
  redraw(): void;
}

/** What the interaction reports. Created and changed are reported once, when the user lets go (NF3). */
export interface DrawingEvents {
  drawingCreated(drawing: Drawing): void;
  drawingChanged(drawing: Drawing): void;
  selectionChanged(id: string | undefined): void;
  /** The tool in use; undefined is the pointer, to which the module returns after a drawing is finished. */
  toolChanged(tool: ToolCode | undefined): void;
}

export interface DrawingOptions {
  /** Snaps anchors to the open, high, low or close of their candle while placing and reshaping (DR2). */
  readonly magnet: boolean;
  /** All drawings hidden (DR6): nothing is drawn, hit or placed. */
  readonly hidden: boolean;
}

/** What is under the pointer: a drawing, and the handle when it is the selected one's. */
export interface Hit {
  readonly id: string;
  readonly handle?: number;
}

interface Placing {
  readonly tool: ToolCode;
  readonly style: DrawingStyle;
  /** Fields the new drawing gets as they are, e.g. the wave mark chosen for a wave label. */
  readonly extras: Readonly<Record<string, unknown>>;
  readonly anchors: Anchor[];
  /** Where the pointer is, for the preview. */
  pointer: Point | undefined;
}

type Gesture =
  | { readonly kind: "move"; readonly original: Drawing; readonly from: Point; readonly starts: readonly AnchorStart[]; moved: boolean }
  | { readonly kind: "reshape"; readonly original: Drawing; readonly from: Point; readonly points: readonly Point[]; readonly handle: number; moved: boolean };

const PREVIEW_ID = "preview";

/** A press this close to the last point of a polyline is the second click of a double-click: it ends the line. */
const DOUBLE_CLICK_REACH = 4;

/**
 * The tool state machine of the chart module (DR2): place a drawing with click, move, click; select it; move
 * it; reshape it by its handles; cancel with Escape. A locked drawing can be selected, never moved or reshaped.
 * It holds the drawings of the chart; Web gives them with setDrawings and hears every change through the events.
 */
export class DrawingInteraction {
  private drawings: Drawing[] = [];
  private selectedId: string | undefined;
  private placing: Placing | undefined;
  private gesture: Gesture | undefined;
  private options: DrawingOptions = { magnet: true, hidden: false };

  /** Drawings whose creation is not reported yet (their exact times are looked up): changes to them go with it. */
  private readonly pendingCreation = new Set<string>();

  /**
   * @param refine gives the anchors of a new or reshaped drawing their exact times (step 9 of the plan); it
   * returns undefined when there is nothing to look up, and the drawing is then reported at once
   */
  constructor(
    private readonly surface: DrawingSurface,
    private readonly events: DrawingEvents,
    private readonly newId: () => string = () => crypto.randomUUID(),
    private readonly refine: (drawing: Drawing, changed: readonly number[]) => Promise<Drawing> | undefined = () => undefined,
  ) {}

  /** The drawings to draw, in their order; none while hidden. */
  visible(): readonly Drawing[] {
    return this.options.hidden ? [] : this.drawings;
  }

  /** The drawing being placed, with the pointer as its next anchor. */
  preview(): Drawing | undefined {
    const placing = this.placing;
    if (placing === undefined) {
      return undefined;
    }

    if (placing.tool === "waveLabel") {
      return placing.pointer === undefined ? undefined : this.newWaveLabel(PREVIEW_ID, placing, placing.pointer);
    }

    const pointer = placing.pointer === undefined ? undefined : this.surface.anchorAt(placing.pointer, this.options.magnet);
    const anchors = pointer === undefined ? placing.anchors : [...placing.anchors, pointer];
    return anchors.length === 0 ? undefined : this.newDrawing(PREVIEW_ID, placing, anchors);
  }

  selected(): string | undefined {
    return this.selectedId;
  }

  all(): readonly Drawing[] {
    return this.drawings;
  }

  /** The tool being placed, undefined for the pointer. */
  tool(): ToolCode | undefined {
    return this.placing?.tool;
  }

  /** True while a drawing is placed or dragged: then the pointer belongs to the module. */
  isBusy(): boolean {
    return this.placing !== undefined || this.gesture !== undefined;
  }

  /** Replaces the drawings, e.g. after another window changed them. A drag in progress keeps its drawing. */
  setDrawings(drawings: readonly Drawing[]): void {
    const dragged = this.gesture === undefined ? undefined : this.find(this.gesture.original.id);
    this.drawings = drawings.map((drawing) => (dragged !== undefined && drawing.id === dragged.id ? dragged : drawing));
    if (this.selectedId !== undefined && this.find(this.selectedId) === undefined) {
      this.setSelected(undefined);
    }

    this.surface.redraw();
  }

  /**
   * Chooses a tool to place, or the pointer (undefined). A style given replaces the default of the tool; extras
   * are fields the new drawing gets, such as `wave` (degree and label) for a wave label.
   */
  setTool(tool: ToolCode | undefined, style?: DrawingStyle, extras: Readonly<Record<string, unknown>> = {}): void {
    this.cancelGesture();
    if (tool === undefined || this.options.hidden || shapeOf(tool) === undefined) {
      const wasPlacing = this.placing !== undefined;
      this.placing = undefined;
      if (wasPlacing || tool !== undefined) {
        this.events.toolChanged(undefined);
      }
    } else {
      this.placing = { tool, style: style ?? defaultStyle(tool), extras, anchors: [], pointer: this.placing?.pointer };
      this.setSelected(undefined);
      this.events.toolChanged(tool);
    }

    this.surface.redraw();
  }

  setOptions(options: Partial<DrawingOptions>): void {
    this.options = { ...this.options, ...options };
    if (this.options.hidden) {
      this.cancelGesture();
      this.setSelected(undefined);
      if (this.placing !== undefined) {
        this.placing = undefined;
        this.events.toolChanged(undefined);
      }
    }

    this.surface.redraw();
  }

  /** Selects a drawing (or none) as if the user had clicked it. */
  select(id: string | undefined): void {
    this.setSelected(id !== undefined && this.find(id) !== undefined ? id : undefined);
    this.surface.redraw();
  }

  /** The cursor the chart shows at a point, or undefined for its own. */
  cursorAt(point: Point): string | undefined {
    if (this.placing !== undefined) {
      return "crosshair";
    }

    const hit = this.hitAt(point);
    if (hit === undefined) {
      return undefined;
    }

    if (this.find(hit.id)?.locked === true) {
      return "default";
    }

    return hit.handle !== undefined ? "pointer" : "move";
  }

  /** The topmost drawing under the point; on the selected drawing its handles come first. */
  hitAt(point: Point): Hit | undefined {
    if (this.options.hidden) {
      return undefined;
    }

    const pane = this.surface.paneSize();
    const selected = this.selectedId === undefined ? undefined : this.find(this.selectedId);
    if (selected !== undefined) {
      const points = this.surface.pointsOf(selected);
      const handles = points === undefined ? [] : (shapeOf(selected.tool)?.handles(points, pane) ?? []);
      const handle = handles.findIndex((at) => Math.hypot(at.x - point.x, at.y - point.y) <= HANDLE_HIT);
      if (handle >= 0) {
        return { id: selected.id, handle };
      }
    }

    for (let index = this.drawings.length - 1; index >= 0; index--) {
      const drawing = this.drawings[index]!;
      const shape = shapeOf(drawing.tool);
      const points = shape === undefined ? undefined : this.surface.pointsOf(drawing);
      if (points !== undefined && shape!.hits(point, points, pane, drawing)) {
        return { id: drawing.id };
      }
    }

    return undefined;
  }

  /** A press of the left button. True: the module takes it, the chart must not see it (no pan). */
  pointerDown(point: Point): boolean {
    if (this.gesture !== undefined) {
      return true;
    }

    if (this.placing !== undefined) {
      this.place(point);
      return true;
    }

    const hit = this.hitAt(point);
    if (hit === undefined) {
      if (this.selectedId !== undefined) {
        this.setSelected(undefined);
        this.surface.redraw();
      }

      return false; // the chart pans as before (NF2)
    }

    const drawing = this.find(hit.id)!;
    if (this.selectedId !== hit.id) {
      this.setSelected(hit.id);
      this.surface.redraw();
    }

    if (!drawing.locked) {
      this.startGesture(drawing, point, hit.handle);
    }

    return true;
  }

  pointerMove(point: Point): boolean {
    if (this.placing !== undefined) {
      this.placing.pointer = point;
      this.surface.redraw();
      return true;
    }

    const gesture = this.gesture;
    if (gesture === undefined) {
      return false;
    }

    if (!gesture.moved && !hasDragged(gesture.from, point)) {
      return true;
    }

    const changed = gesture.kind === "move" ? this.moved(gesture, point) : this.reshaped(gesture, point);
    if (changed !== undefined) {
      gesture.moved = true;
      this.replace(changed);
      this.surface.redraw();
    }

    return true;
  }

  pointerUp(): boolean {
    const gesture = this.gesture;
    if (gesture === undefined) {
      return this.placing !== undefined;
    }

    this.gesture = undefined;
    const result = this.find(gesture.original.id);
    if (gesture.moved && result !== undefined) {
      // A reshaped anchor may get its exact time; a drawing moved as a whole keeps the times it was moved to.
      const changed = gesture.kind === "reshape" ? changedAnchors(gesture.original, result) : [];
      this.report("changed", result, changed);
    }

    return true;
  }

  /**
   * Reports a new or changed drawing once (NF3): at once, or after its exact times were looked up. Whatever
   * happened to the drawing meanwhile wins: then its latest state is reported and the lookup is dropped.
   */
  private report(kind: "created" | "changed", drawing: Drawing, changed: readonly number[]): void {
    const tell = (reported: Drawing) => {
      if (kind === "created") {
        this.pendingCreation.delete(reported.id);
        this.events.drawingCreated(reported);
      } else if (!this.pendingCreation.has(reported.id)) {
        this.events.drawingChanged(reported); // a drawing not reported as created yet goes with its creation
      }
    };

    const lookup = changed.length === 0 ? undefined : this.refine(drawing, changed);
    if (lookup === undefined) {
      tell(drawing);
      return;
    }

    if (kind === "created") {
      this.pendingCreation.add(drawing.id);
    }

    void lookup
      .catch(() => drawing)
      .then((refined) => {
        const current = this.find(drawing.id);
        if (current === undefined) {
          this.pendingCreation.delete(drawing.id); // gone meanwhile (Web deleted it)
          return;
        }

        if (current === drawing && refined !== drawing) {
          this.replace(refined);
          this.surface.redraw();
          tell(refined);
          return;
        }

        tell(current);
      });
  }

  /** Escape: a drag puts the drawing back, placing is given up and the pointer returns. True when there was something to cancel. */
  cancel(): boolean {
    if (this.gesture !== undefined) {
      this.cancelGesture();
      this.surface.redraw();
      return true;
    }

    if (this.placing !== undefined) {
      this.setTool(undefined);
      return true;
    }

    return false;
  }

  /**
   * Ends a drawing the user ends (a polyline): Enter. True when there was one with enough points; with fewer
   * the placing goes on.
   */
  finish(): boolean {
    const placing = this.placing;
    const shape = placing === undefined ? undefined : shapeOf(placing.tool);
    if (placing === undefined || shape?.minimum === undefined || placing.anchors.length < shape.minimum || patternOf(placing) !== undefined) {
      return false; // a wave pattern ends by itself when all its points are placed
    }

    this.complete(this.newDrawing(this.newId(), placing, placing.anchors));
    return true;
  }

  private place(point: Point): void {
    const placing = this.placing!;
    if (placing.tool === "waveLabel") {
      this.placeWaveLabel(placing, point);
      return;
    }

    const anchor = this.surface.anchorAt(point, this.options.magnet);
    if (anchor === undefined) {
      return;
    }

    // A second click on the last point (a double-click) ends a polyline; a wave pattern ends at its last point.
    const shape = shapeOf(placing.tool)!;
    const pattern = patternOf(placing);
    const last = placing.anchors[placing.anchors.length - 1];
    const lastPoint = last === undefined ? undefined : this.surface.pointOf(last);
    if (pattern === undefined && shape.minimum !== undefined && lastPoint !== undefined && Math.hypot(lastPoint.x - point.x, lastPoint.y - point.y) <= DOUBLE_CLICK_REACH) {
      this.finish();
      return;
    }

    placing.anchors.push(anchor);
    if (placing.anchors.length < (pattern === undefined ? shape.anchors : pattern.labels.length + 1)) {
      this.surface.redraw();
      return;
    }

    this.complete(this.newDrawing(this.newId(), placing, placing.anchors));
  }

  /** A finished drawing: reported, selected, and the pointer is back, as TradingView does by default. */
  private complete(drawing: Drawing): void {
    this.placing = undefined;
    this.drawings.push(drawing);
    this.report("created", drawing, drawing.anchors.map((_, index) => index));
    this.events.toolChanged(undefined);
    this.setSelected(drawing.id);
    this.surface.redraw();
  }

  /**
   * A wave label is finished with one click; the tool stays, so that a count is placed label after label (Web moves
   * the chooser on to the next label after each one).
   */
  private placeWaveLabel(placing: Placing, point: Point): void {
    const drawing = this.newWaveLabel(this.newId(), placing, point);
    if (drawing === undefined) {
      return;
    }

    this.drawings.push(drawing);
    this.report("created", drawing, [0]);
    this.surface.redraw();
  }

  private newDrawing(id: string, placing: Placing, anchors: readonly Anchor[]): Drawing {
    // A wave pattern is a polyline with labels on its points; its pattern and degree are not part of the record.
    const pattern = patternOf(placing);
    const { pattern: _pattern, degree: _degree, ...extras } = placing.extras;
    const drawing: Drawing = { ...extras, id, tool: placing.tool, locked: false, anchors: [...anchors], style: placing.style };
    if (pattern !== undefined) {
      return { ...drawing, pointLabels: patternLabels(anchors, pattern.labels, pattern.degree) };
    }

    return placing.tool === "textNote" ? { ...drawing, text: NEW_NOTE_TEXT } : drawing;
  }

  /** A wave label at the candle under the point: above it when the point is above the middle of the candle, else below. */
  private newWaveLabel(id: string, placing: Placing, point: Point): Drawing | undefined {
    const anchor = this.surface.anchorAt(point, false);
    const side = this.surface.sideAt(point);
    const mark = placing.extras["wave"] as Record<string, unknown> | undefined;
    if (anchor === undefined || side === undefined || mark === undefined) {
      return undefined;
    }

    return this.newDrawing(id, { ...placing, extras: { ...placing.extras, wave: { ...mark, side } } }, [{ time: anchor.time, price: null }]);
  }

  private startGesture(drawing: Drawing, point: Point, handle: number | undefined): void {
    const points = this.surface.pointsOf(drawing);
    if (points === undefined) {
      return;
    }

    if (handle !== undefined) {
      this.gesture = { kind: "reshape", original: drawing, from: point, points, handle, moved: false };
      return;
    }

    const starts: AnchorStart[] = [];
    for (let index = 0; index < drawing.anchors.length; index++) {
      const anchorIndex = this.surface.indexOf(drawing.anchors[index]!.time);
      if (anchorIndex === undefined) {
        return;
      }

      starts.push({ index: anchorIndex, y: points[index]!.y });
    }

    this.gesture = { kind: "move", original: drawing, from: point, starts, moved: false };
  }

  private moved(gesture: Extract<Gesture, { kind: "move" }>, point: Point): Drawing | undefined {
    const bars = this.surface.barsBetween(gesture.from.x, point.x);
    if (bars === undefined) {
      return undefined;
    }

    // A wave label moves to another candle and to the side of it the pointer is on; it has no price.
    if (gesture.original.tool === "waveLabel") {
      const time = this.surface.timeOf(gesture.starts[0]!.index + bars);
      const side = this.surface.sideAt(point);
      const mark = gesture.original["wave"] as Record<string, unknown> | undefined;
      return time === undefined || side === undefined ? undefined : { ...gesture.original, anchors: [{ time, price: null }], wave: { ...mark, side } };
    }

    const anchors = movedAnchors(gesture.starts, bars, point.y - gesture.from.y, {
      timeOf: (index) => this.surface.timeOf(index),
      priceOf: (y) => this.surface.priceAt(y),
    });

    return anchors === undefined ? undefined : { ...gesture.original, anchors };
  }

  private reshaped(gesture: Extract<Gesture, { kind: "reshape" }>, point: Point): Drawing | undefined {
    const original = gesture.original;
    const shape = shapeOf(original.tool)!;
    const points = shape.reshape(gesture.points, gesture.handle, point);

    // Only anchors whose point changed are converted; the magnet takes the anchor the handle stands for.
    const anchors: Anchor[] = [];
    for (let index = 0; index < original.anchors.length; index++) {
      if (points[index] === gesture.points[index]) {
        anchors.push(original.anchors[index]!);
        continue;
      }

      const anchor = this.surface.anchorAt(points[index]!, this.options.magnet && index === gesture.handle);
      if (anchor === undefined) {
        return undefined;
      }

      anchors.push(anchor);
    }

    // The labels on the points of a polyline stay above the highs and below the lows when a point moves.
    const labels = labelsOf(original);
    return labels.length === 0 ? { ...original, anchors } : { ...original, anchors, pointLabels: withSides(anchors, labels) };
  }

  private cancelGesture(): void {
    const gesture = this.gesture;
    if (gesture !== undefined) {
      this.gesture = undefined;
      this.replace(gesture.original);
    }
  }

  private replace(drawing: Drawing): void {
    const index = this.drawings.findIndex((existing) => existing.id === drawing.id);
    if (index >= 0) {
      this.drawings[index] = drawing;
    }
  }

  private find(id: string): Drawing | undefined {
    return this.drawings.find((drawing) => drawing.id === id);
  }

  private setSelected(id: string | undefined): void {
    if (this.selectedId !== id) {
      this.selectedId = id;
      this.events.selectionChanged(id);
    }
  }
}

/** The indices of the anchors whose time or price differ between two states of a drawing. */
function changedAnchors(before: Drawing, after: Drawing): number[] {
  const changed: number[] = [];
  after.anchors.forEach((anchor, index) => {
    const old = before.anchors[index];
    if (old === undefined || old.time !== anchor.time || old.price !== anchor.price) {
      changed.push(index);
    }
  });

  return changed;
}

/** The wave pattern being placed: the labels of its points after the start, and their degree (DR5). */
function patternOf(placing: Placing): { readonly labels: readonly string[]; readonly degree: WaveDegree } | undefined {
  const labels = placing.extras["pattern"];
  const degree = placing.extras["degree"];
  if (placing.tool !== "polyline" || !Array.isArray(labels) || labels.length === 0 || !WAVE_DEGREES.includes(degree as WaveDegree)) {
    return undefined;
  }

  return { labels: labels.filter((label): label is string => typeof label === "string"), degree: degree as WaveDegree };
}
