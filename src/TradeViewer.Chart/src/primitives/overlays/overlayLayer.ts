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
import type { TimeMapping } from "../../tools/timeMapping";
import type { OverlayGroup, OverlayShape } from "./overlay";
import { hits, LABEL_OFFSET, MARKER_RADIUS, place, type Placed, type Projector } from "./placement";
import { roleColour } from "./roleColours";

type RenderTarget = Parameters<IPrimitivePaneRenderer["draw"]>[0];

/** The prefix of the ids the chart reports for a hovered overlay shape; group and shape index follow. */
export const OVERLAY_ID_PREFIX = "overlay:";

/** What the pane shows when the user points at a shape: its detail, or its text, under the title of its group. */
export interface OverlayHover {
  readonly group: OverlayGroup;
  readonly shape: OverlayShape;
}

interface Drawn {
  readonly group: number;
  readonly shape: number;
  readonly placed: Placed;
}

/**
 * The read-only shapes of overlays and scenarios (OV1-OV6, SC2, SC8): one series primitive that draws every group not
 * hidden, each shape in the colour of its role, below the user's drawings. Positions are times and prices: a time
 * between two candles is drawn at the candle that contains it, on every timeframe (architecture, chapter 10).
 */
export class OverlayLayer implements ISeriesPrimitive<Time> {
  private chart: IChartApi | undefined;
  private series: ISeriesApi<SeriesType> | undefined;
  private requestUpdate: (() => void) | undefined;
  private groups: readonly OverlayGroup[] = [];
  private hidden: ReadonlySet<string> = new Set();
  private drawn: Drawn[] = [];

  private readonly view: IPrimitivePaneView = {
    zOrder: () => "normal",
    renderer: () => ({ draw: (target: RenderTarget) => this.draw(target) }),
  };

  /**
   * @param mapping the time mapping of the candles shown, undefined while there are none
   * @param background the background of the chart, drawn around texts so they stay readable on candles
   */
  constructor(
    private readonly mapping: () => TimeMapping | undefined,
    private readonly background: () => string,
  ) {}

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

  /** The groups to draw and the keys of those hidden (OV2, SC2); the order of the groups is the order of drawing. */
  set(groups: readonly OverlayGroup[], hiddenKeys: readonly string[] = []): void {
    this.groups = groups;
    this.hidden = new Set(hiddenKeys);
    this.requestUpdate?.();
  }

  /** The number of shapes drawn at the last paint, for checks. */
  drawnCount(): number {
    return this.drawn.length;
  }

  /** The shape a hovered id of this layer names. */
  hovered(id: string): OverlayHover | undefined {
    if (!id.startsWith(OVERLAY_ID_PREFIX)) {
      return undefined;
    }

    const [group, shape] = id.slice(OVERLAY_ID_PREFIX.length).split(":").map(Number);
    const g = this.groups[group ?? -1];
    const s = g?.shapes[shape ?? -1];
    return g === undefined || s === undefined ? undefined : { group: g, shape: s };
  }

  paneViews(): readonly IPrimitivePaneView[] {
    return [this.view];
  }

  /** The topmost shape under the pointer that has something to show. */
  hitTest(x: number, y: number): PrimitiveHoveredItem | null {
    for (let i = this.drawn.length - 1; i >= 0; i--) {
      const drawn = this.drawn[i]!;
      const shape = this.groups[drawn.group]?.shapes[drawn.shape];
      if ((shape?.detail ?? shape?.text) !== undefined && hits(drawn.placed, { x, y })) {
        return { externalId: OVERLAY_ID_PREFIX + drawn.group + ":" + drawn.shape, zOrder: "normal", cursorStyle: "help" };
      }
    }

    return null;
  }

  private projector(width: number, height: number): Projector | undefined {
    const chart = this.chart;
    const series = this.series;
    const mapping = this.mapping();
    if (chart === undefined || series === undefined || mapping === undefined || mapping.isEmpty) {
      return undefined;
    }

    return {
      width,
      height,
      x: (time) => {
        const index = mapping.indexOf(time);
        const x = index === undefined ? null : chart.timeScale().logicalToCoordinate(index as Logical);
        return x ?? undefined;
      },
      y: (price) => {
        const y = series.priceToCoordinate(price);
        return y === null || !Number.isFinite(y) ? undefined : y;
      },
    };
  }

  private draw(target: RenderTarget): void {
    target.useMediaCoordinateSpace((scope) => {
      this.drawn = [];
      const projector = this.projector(scope.mediaSize.width, scope.mediaSize.height);
      if (projector === undefined) {
        return;
      }

      const context = scope.context;
      this.groups.forEach((group, groupIndex) => {
        if (this.hidden.has(group.key)) {
          return;
        }

        group.shapes.forEach((shape, shapeIndex) => {
          const placed = place(shape, projector);
          if (placed === undefined) {
            return;
          }

          context.save();
          this.drawShape(context, shape, placed, projector);
          context.restore();
          this.drawn.push({ group: groupIndex, shape: shapeIndex, placed });
        });
      });
    });
  }

  private drawShape(context: CanvasRenderingContext2D, shape: OverlayShape, placed: Placed, projector: Projector): void {
    const colour = roleColour(shape.role);
    context.strokeStyle = colour;
    context.fillStyle = colour;
    context.lineWidth = shape.weight === "strong" ? 2.5 : 1.5;
    context.setLineDash(shape.style === "dashed" ? [6, 4] : shape.style === "dotted" ? [1.5, 3] : []);

    switch (placed.kind) {
      case "polyline":
        context.beginPath();
        placed.points.forEach((point, index) => (index === 0 ? context.moveTo(point.x, point.y) : context.lineTo(point.x, point.y)));
        context.stroke();
        break;
      case "label":
        this.text(context, shape.text ?? "", placed.text.x, placed.text.y, "center", colour, "bold 12px sans-serif");
        break;
      case "marker":
        context.setLineDash([]);
        context.beginPath();
        context.arc(placed.at.x, placed.at.y, MARKER_RADIUS, 0, 2 * Math.PI);
        context.fill();
        if (shape.text !== undefined) {
          this.text(context, shape.text, placed.at.x + MARKER_RADIUS + 4, placed.at.y, "left", colour, "11px sans-serif");
        }

        break;
      case "level":
        context.beginPath();
        context.moveTo(placed.fromX, placed.y);
        context.lineTo(placed.toX, placed.y);
        context.stroke();
        if (shape.text !== undefined) {
          this.text(context, shape.text, Math.min(placed.toX, projector.width) - 4, placed.y - LABEL_OFFSET / 2 - 2, "right", colour, "11px sans-serif");
        }

        break;
      case "zone":
        context.globalAlpha = 0.15;
        context.fillRect(placed.fromX, placed.top, placed.toX - placed.fromX, placed.bottom - placed.top);
        context.globalAlpha = 1;
        context.strokeRect(placed.fromX, placed.top, placed.toX - placed.fromX, placed.bottom - placed.top);
        if (shape.text !== undefined) {
          this.text(context, shape.text, placed.fromX + 4, placed.top + 8, "left", colour, "11px sans-serif");
        }

        break;
      case "vertical":
        context.beginPath();
        context.moveTo(placed.x, 0);
        context.lineTo(placed.x, projector.height);
        context.stroke();
        if (shape.text !== undefined) {
          this.text(context, shape.text, placed.x + 4, 10, "left", colour, "11px sans-serif");
        }

        break;
    }
  }

  /** A text with a thin border in the chart's background, so it can be read on top of candles. */
  private text(context: CanvasRenderingContext2D, text: string, x: number, y: number, align: CanvasTextAlign, colour: string, font: string): void {
    context.setLineDash([]);
    context.font = font;
    context.textAlign = align;
    context.textBaseline = "middle";
    context.lineWidth = 3;
    context.strokeStyle = this.background();
    context.strokeText(text, x, y);
    context.fillStyle = colour;
    context.fillText(text, x, y);
  }
}
