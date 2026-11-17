import type {
  IChartApi,
  IPrimitivePaneRenderer,
  IPrimitivePaneView,
  ISeriesApi,
  ISeriesPrimitive,
  Logical,
  PrimitiveHoveredItem,
  SeriesAttachedParameter,
  SeriesType,
  Time,
} from "lightweight-charts";
import { hitsSegment, type Point } from "../tools/geometry";
import type { TimeMapping } from "../tools/timeMapping";

type RenderTarget = Parameters<IPrimitivePaneRenderer["draw"]>[0];

/** A point of a drawing: a time (Unix seconds, UTC) and a price (DR3). */
export interface Anchor {
  readonly time: number;
  readonly price: number;
}

/**
 * A trend line, the one drawing of the proof (plan step 1). Step 5 brings the tools of DR1 as one class
 * per tool on this layer.
 */
export interface TrendLine {
  readonly id: string;
  readonly anchors: readonly [Anchor, Anchor];
  readonly color: string;
  /** Line width in pixels, 1 to 4. */
  readonly width: number;
}

/** The prefix of the ids the chart reports for a hovered drawing; the id of the drawing follows. */
export const DRAWING_ID_PREFIX = "drawing:";

/**
 * The drawing layer: one series primitive that holds all drawings of a chart, draws them and says which
 * one is under the pointer. One layer, because the order of drawing and the question "what is under the
 * pointer" need one place. Anchors are placed on whole bar indices through the time mapping.
 */
export class DrawingLayer implements ISeriesPrimitive<Time> {
  private chart: IChartApi | undefined;
  private series: ISeriesApi<SeriesType> | undefined;
  private requestUpdate: (() => void) | undefined;
  private drawings: TrendLine[] = [];

  private readonly view: IPrimitivePaneView = {
    zOrder: () => "normal",
    renderer: () => ({ draw: (target: RenderTarget) => this.draw(target) }),
  };

  /** @param mapping the time mapping of the candles shown, undefined while there are none */
  constructor(private readonly mapping: () => TimeMapping | undefined) {}

  attached(parameters: SeriesAttachedParameter<Time>): void {
    this.chart = parameters.chart;
    this.series = parameters.series;
    this.requestUpdate = parameters.requestUpdate;
  }

  detached(): void {
    this.chart = undefined;
    this.series = undefined;
    this.requestUpdate = undefined;
  }

  /** Replaces all drawings. The last one is drawn on top and wins a hit. */
  set(drawings: readonly TrendLine[]): void {
    this.drawings = [...drawings];
    this.requestUpdate?.();
  }

  all(): readonly TrendLine[] {
    return this.drawings;
  }

  get(id: string): TrendLine | undefined {
    return this.drawings.find((drawing) => drawing.id === id);
  }

  /** Puts a changed drawing in place of the one with its id, as while it is dragged. */
  replace(drawing: TrendLine): void {
    const index = this.drawings.findIndex((existing) => existing.id === drawing.id);
    if (index < 0) {
      return;
    }

    this.drawings[index] = drawing;
    this.requestUpdate?.();
  }

  /** Where an anchor is drawn, in pixels of the pane; undefined without candles or price scale. */
  pointOf(anchor: Anchor): Point | undefined {
    const index = this.mapping()?.indexOf(anchor.time);
    if (index === undefined || this.chart === undefined || this.series === undefined) {
      return undefined;
    }

    const x = this.chart.timeScale().logicalToCoordinate(index as Logical);
    const y = this.series.priceToCoordinate(anchor.price);
    return x === null || y === null ? undefined : { x, y };
  }

  /** The id of the topmost drawing under a point of the pane, or undefined. */
  drawingAt(point: Point): string | undefined {
    for (let index = this.drawings.length - 1; index >= 0; index--) {
      const drawing = this.drawings[index]!;
      const a = this.pointOf(drawing.anchors[0]);
      const b = this.pointOf(drawing.anchors[1]);
      if (a !== undefined && b !== undefined && hitsSegment(point, a, b, Math.max(drawing.width / 2 + 4, 6))) {
        return drawing.id;
      }
    }

    return undefined;
  }

  paneViews(): readonly IPrimitivePaneView[] {
    return [this.view];
  }

  /** The library asks on every pointer move; a hit gives the "move" cursor and a hovered object id. */
  hitTest(x: number, y: number): PrimitiveHoveredItem | null {
    const id = this.drawingAt({ x, y });
    return id === undefined ? null : { externalId: DRAWING_ID_PREFIX + id, zOrder: "normal", cursorStyle: "move" };
  }

  private draw(target: RenderTarget): void {
    if (this.drawings.length === 0) {
      return;
    }

    target.useMediaCoordinateSpace((scope) => {
      const context = scope.context;
      context.lineCap = "round";

      for (const drawing of this.drawings) {
        const a = this.pointOf(drawing.anchors[0]);
        const b = this.pointOf(drawing.anchors[1]);
        if (a === undefined || b === undefined) {
          continue;
        }

        context.beginPath();
        context.moveTo(a.x, a.y);
        context.lineTo(b.x, b.y);
        context.strokeStyle = drawing.color;
        context.lineWidth = drawing.width;
        context.stroke();
      }
    });
  }
}
