import type { IChartApi, ISeriesApi, SeriesType } from "lightweight-charts";
import type { DrawingLayer, TrendLine } from "../primitives/drawingLayer";
import { hasDragged, movedAnchors, wholeBarsBetween, type AnchorStart } from "./drag";
import type { Point } from "./geometry";
import type { TimeMapping } from "./timeMapping";

interface Drag {
  readonly original: TrendLine;
  readonly from: Point;
  readonly fromLogical: number;
  readonly starts: readonly AnchorStart[];
  moved: boolean;
}

/**
 * Takes the pointer before the chart does. A press on a drawing starts dragging it, and the chart never
 * sees that press, so it does not pan; a press anywhere else goes to the chart as before (NF2). While
 * dragging only the module works: the drawing follows the pointer, and on release the result is reported
 * once (NF3). Escape while dragging puts the drawing back.
 */
export class PointerInput {
  private drag: Drag | undefined;
  private readonly onDown = (event: MouseEvent) => this.press(event);
  private readonly onMove = (event: MouseEvent) => this.move(event);
  private readonly onUp = (event: MouseEvent) => this.release(event);
  private readonly onKey = (event: KeyboardEvent) => this.key(event);

  /** Presses that hit a drawing, for checks in the browser. */
  hits = 0;

  constructor(
    private readonly element: HTMLElement,
    private readonly chart: IChartApi,
    private readonly series: ISeriesApi<SeriesType>,
    private readonly layer: DrawingLayer,
    private readonly mapping: () => TimeMapping | undefined,
    private readonly onChanged: (drawing: TrendLine) => void,
  ) {
    // The capture phase on an element around the chart: the library listens on its own elements inside it.
    element.addEventListener("mousedown", this.onDown, { capture: true });
  }

  dispose(): void {
    this.element.removeEventListener("mousedown", this.onDown, { capture: true });
    this.stop();
  }

  private press(event: MouseEvent): void {
    if (event.button !== 0 || this.drag !== undefined) {
      return;
    }

    const point = this.local(event);
    if (point.x < 0 || point.x > this.chart.timeScale().width()) {
      return; // on the price scale: that belongs to the chart
    }

    const id = this.layer.drawingAt(point);
    const drawing = id === undefined ? undefined : this.layer.get(id);
    const mapping = this.mapping();
    const fromLogical = this.chart.timeScale().coordinateToLogical(point.x);
    if (drawing === undefined || mapping === undefined || fromLogical === null) {
      return;
    }

    const starts: AnchorStart[] = [];
    for (const anchor of drawing.anchors) {
      const index = mapping.indexOf(anchor.time);
      const at = this.layer.pointOf(anchor);
      if (index === undefined || at === undefined) {
        return;
      }

      starts.push({ index, y: at.y });
    }

    event.stopPropagation();
    event.preventDefault();
    this.hits++;
    this.drag = { original: drawing, from: point, fromLogical, starts, moved: false };
    window.addEventListener("mousemove", this.onMove, { capture: true });
    window.addEventListener("mouseup", this.onUp, { capture: true });
    window.addEventListener("keydown", this.onKey, { capture: true });
  }

  private move(event: MouseEvent): void {
    const drag = this.drag;
    const mapping = this.mapping();
    if (drag === undefined || mapping === undefined) {
      return;
    }

    const point = this.local(event);
    if (!drag.moved && !hasDragged(drag.from, point)) {
      return;
    }

    const logical = this.chart.timeScale().coordinateToLogical(point.x);
    if (logical === null) {
      return;
    }

    const anchors = movedAnchors(drag.starts, wholeBarsBetween(drag.fromLogical, logical), point.y - drag.from.y, {
      timeOf: (index) => mapping.timeOf(index),
      priceOf: (y) => this.series.coordinateToPrice(y) ?? undefined,
    });

    if (anchors !== undefined) {
      drag.moved = true;
      this.layer.replace({ ...drag.original, anchors: [anchors[0]!, anchors[1]!] });
    }
  }

  private release(event: MouseEvent): void {
    const drag = this.drag;
    if (drag === undefined || event.button !== 0) {
      return;
    }

    this.stop();
    const result = this.layer.get(drag.original.id);
    if (drag.moved && result !== undefined) {
      this.onChanged(result);
    }
  }

  private key(event: KeyboardEvent): void {
    const drag = this.drag;
    if (drag === undefined || event.key !== "Escape") {
      return;
    }

    event.stopPropagation();
    this.stop();
    this.layer.replace(drag.original);
  }

  private stop(): void {
    this.drag = undefined;
    window.removeEventListener("mousemove", this.onMove, { capture: true });
    window.removeEventListener("mouseup", this.onUp, { capture: true });
    window.removeEventListener("keydown", this.onKey, { capture: true });
  }

  /** The pointer in pixels of the pane: the pane starts at the top left of the element (no left scale). */
  private local(event: MouseEvent): Point {
    const box = this.element.getBoundingClientRect();
    return { x: event.clientX - box.left, y: event.clientY - box.top };
  }
}
