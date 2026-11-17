import type {
  IChartApi,
  IPrimitivePaneRenderer,
  IPrimitivePaneView,
  ISeriesApi,
  ISeriesPrimitive,
  ISeriesPrimitiveAxisView,
  Logical,
  PrimitiveHoveredItem,
  SeriesAttachedParameter,
  SeriesType,
  Time,
} from "lightweight-charts";
import type { Bar } from "../data/bars";
import type { Point } from "../tools/geometry";
import type { DrawingInteraction, DrawingSurface } from "../tools/interaction";
import { snapPrice } from "../tools/magnet";
import type { TimeMapping } from "../tools/timeMapping";
import type { Anchor, Drawing } from "./drawing";
import type { PaneSize } from "./shape";
import { shapeOf } from "./shapes";
import { waveMarkOf, type WaveSide } from "./waveNotation";

type RenderTarget = Parameters<IPrimitivePaneRenderer["draw"]>[0];

/** The id the chart reports for a hovered drawing. */
export const DRAWING_HOVER_ID = "drawing";

/**
 * The drawing layer: one series primitive that draws all drawings of the chart, each by the shape of its tool,
 * and the preview of the one being placed. It is also the surface of the interaction: it converts between
 * pixels and anchors through the time mapping (whole bar indices) and the price scale.
 */
export class DrawingLayer implements ISeriesPrimitive<Time>, DrawingSurface {
  private chart: IChartApi | undefined;
  private series: ISeriesApi<SeriesType> | undefined;
  private requestUpdate: (() => void) | undefined;
  private interaction: DrawingInteraction | undefined;
  private size: PaneSize = { width: 0, height: 0 };

  private readonly view: IPrimitivePaneView = {
    zOrder: () => "normal",
    renderer: () => ({ draw: (target: RenderTarget) => this.draw(target) }),
  };

  /**
   * @param mapping the time mapping of the candles shown, undefined while there are none
   * @param bars the candles in memory, index 0 the oldest, for the magnet
   */
  constructor(
    private readonly mapping: () => TimeMapping | undefined,
    private readonly bars: () => readonly Bar[],
  ) {}

  /** The interaction whose drawings, preview and selection this layer shows. */
  bind(interaction: DrawingInteraction): void {
    this.interaction = interaction;
  }

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

  // --- DrawingSurface ---

  pointsOf(drawing: Drawing): Point[] | undefined {
    if (drawing.tool === "waveLabel") {
      const at = this.wavePoint(drawing);
      return at === undefined ? undefined : [at];
    }

    const points: Point[] = [];
    for (const anchor of drawing.anchors) {
      const point = this.pointOf(anchor);
      if (point === undefined) {
        return undefined;
      }

      points.push(point);
    }

    return points;
  }

  pointOf(anchor: Anchor): Point | undefined {
    const index = this.mapping()?.indexOf(anchor.time);
    if (index === undefined || this.chart === undefined || this.series === undefined || anchor.price === null) {
      return undefined;
    }

    const x = this.chart.timeScale().logicalToCoordinate(index as Logical);
    const y = this.series.priceToCoordinate(anchor.price);
    return x === null || y === null ? undefined : { x, y };
  }

  anchorAt(point: Point, magnet: boolean): Anchor | undefined {
    const mapping = this.mapping();
    const logical = this.chart?.timeScale().coordinateToLogical(point.x);
    if (mapping === undefined || logical === null || logical === undefined) {
      return undefined;
    }

    const index = Math.round(logical);
    const time = mapping.timeOf(index);
    const bar = this.bars()[index];
    const price =
      magnet && bar !== undefined ? snapPrice(bar, point.y, (value) => this.series?.priceToCoordinate(value) ?? undefined) : this.priceAt(point.y);

    return time === undefined || price === undefined ? undefined : { time, price };
  }

  sideAt(point: Point): WaveSide | undefined {
    const logical = this.chart?.timeScale().coordinateToLogical(point.x);
    const bar = logical === null || logical === undefined ? undefined : this.bars()[Math.round(logical)];
    const high = bar === undefined ? null : (this.series?.priceToCoordinate(bar.high) ?? null);
    const low = bar === undefined ? null : (this.series?.priceToCoordinate(bar.low) ?? null);
    return high === null || low === null ? undefined : point.y < (high + low) / 2 ? "above" : "below";
  }

  /** The high (above) or low (below) of the candle that contains the time of a wave label: it follows the candle (DR5). */
  private wavePoint(drawing: Drawing): Point | undefined {
    const anchor = drawing.anchors[0];
    const mark = waveMarkOf(drawing["wave"]);
    const index = anchor === undefined ? undefined : this.mapping()?.indexOf(anchor.time);
    const bar = index === undefined ? undefined : this.bars()[index];
    if (bar === undefined || mark === undefined || this.chart === undefined || this.series === undefined) {
      return undefined;
    }

    const x = this.chart.timeScale().logicalToCoordinate(index as Logical);
    const y = this.series.priceToCoordinate(mark.side === "above" ? bar.high : bar.low);
    return x === null || y === null ? undefined : { x, y };
  }

  /** The price at a height with the precision of the asset, as the price scale writes it. */
  private priceText(y: number): string | undefined {
    const price = this.priceAt(y);
    return price === undefined || this.series === undefined ? undefined : this.series.priceFormatter().format(price);
  }

  barsBetween(fromX: number, toX: number): number | undefined {
    const timeScale = this.chart?.timeScale();
    const from = timeScale?.coordinateToLogical(fromX);
    const to = timeScale?.coordinateToLogical(toX);
    return from === null || from === undefined || to === null || to === undefined ? undefined : Math.round(to - from);
  }

  indexOf(time: number): number | undefined {
    return this.mapping()?.indexOf(time);
  }

  timeOf(index: number): number | undefined {
    return this.mapping()?.timeOf(index);
  }

  priceAt(y: number): number | undefined {
    const price = this.series?.coordinateToPrice(y);
    return price === null || price === undefined || !Number.isFinite(price) ? undefined : price;
  }

  paneSize(): PaneSize {
    return this.size.width > 0 ? this.size : { width: this.chart?.timeScale().width() ?? 0, height: this.chart?.paneSize().height ?? 0 };
  }

  redraw(): void {
    this.requestUpdate?.();
  }

  // --- ISeriesPrimitive ---

  paneViews(): readonly IPrimitivePaneView[] {
    return [this.view];
  }

  /** The library asks on every pointer move; the answer sets the cursor. */
  hitTest(x: number, y: number): PrimitiveHoveredItem | null {
    const cursor = this.interaction?.cursorAt({ x, y });
    return cursor === undefined ? null : { externalId: DRAWING_HOVER_ID, zOrder: "normal", cursorStyle: cursor };
  }

  /** The price of every horizontal line on the price scale, in the colour of the line. */
  priceAxisViews(): readonly ISeriesPrimitiveAxisView[] {
    const interaction = this.interaction;
    const series = this.series;
    if (interaction === undefined || series === undefined) {
      return [];
    }

    const lines = [...interaction.visible()];
    const preview = interaction.preview();
    if (preview !== undefined) {
      lines.push(preview);
    }

    const views: ISeriesPrimitiveAxisView[] = [];
    for (const drawing of lines) {
      const price = drawing.anchors[0]?.price;
      if (drawing.tool !== "horizontalLine" || price === null || price === undefined) {
        continue;
      }

      const y = series.priceToCoordinate(price);
      if (y === null) {
        continue;
      }

      views.push({
        coordinate: () => y,
        text: () => series.priceFormatter().format(price),
        textColor: () => "#ffffff",
        backColor: () => drawing.style.color,
      });
    }

    return views;
  }

  private draw(target: RenderTarget): void {
    const interaction = this.interaction;
    target.useMediaCoordinateSpace((scope) => {
      this.size = { width: scope.mediaSize.width, height: scope.mediaSize.height };
      if (interaction === undefined) {
        return;
      }

      const context = scope.context;
      const selected = interaction.selected();
      const preview = interaction.preview();
      const drawings = preview === undefined ? interaction.visible() : [...interaction.visible(), preview];

      for (const drawing of drawings) {
        const shape = shapeOf(drawing.tool);
        const points = shape === undefined ? undefined : this.pointsOf(drawing);
        if (points === undefined) {
          continue;
        }

        context.save();
        shape!.draw(context, points, this.size, drawing, { selected: drawing.id === selected, priceText: (y) => this.priceText(y) });
        context.restore();
      }
    });
  }
}
