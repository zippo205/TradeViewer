import type {
  IChartApi,
  IPrimitivePaneView,
  ISeriesApi,
  ISeriesPrimitive,
  SeriesAttachedParameter,
  SeriesType,
  Time,
  UTCTimestamp,
} from "lightweight-charts";
import { lowerBound } from "./indicatorData";

/** The two edges of a band: lines of values along times, or a constant price (the 30 and 70 of RSI). */
export interface Band {
  readonly times: () => readonly number[];
  readonly upper: () => readonly (number | null)[] | number;
  readonly lower: () => readonly (number | null)[] | number;
}

/**
 * Fills between two edges behind the lines of its series (IN1): the background of Bollinger Bands, the zone of RSI
 * between 30 and 70. Drawn only over the visible times, so panning costs no more with a long history (NF2).
 */
export class BandFill implements ISeriesPrimitive<Time> {
  private chart: IChartApi | undefined;
  private series: ISeriesApi<SeriesType> | undefined;
  private requestUpdate: (() => void) | undefined;
  private colour: string;
  private visible = true;

  private readonly view: IPrimitivePaneView = {
    zOrder: () => "bottom",
    renderer: () => ({
      draw: () => {},
      drawBackground: (target) => target.useMediaCoordinateSpace(({ context }) => this.draw(context)),
    }),
  };

  constructor(
    private readonly band: Band,
    colour: string,
  ) {
    this.colour = colour;
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

  paneViews(): readonly IPrimitivePaneView[] {
    return [this.view];
  }

  set(colour: string, visible: boolean): void {
    this.colour = colour;
    this.visible = visible;
    this.requestUpdate?.();
  }

  private draw(context: CanvasRenderingContext2D): void {
    const chart = this.chart;
    const series = this.series;
    const range = chart?.timeScale().getVisibleRange();
    if (!this.visible || chart === undefined || series === undefined || range === null || range === undefined) {
      return;
    }

    const times = this.band.times();
    const upper = this.band.upper();
    const lower = this.band.lower();
    const valueAt = (edge: readonly (number | null)[] | number, index: number) => (typeof edge === "number" ? edge : edge[index] ?? null);

    // One candle beyond each side, so the fill reaches the edges of the pane.
    const from = Math.max(0, lowerBound(times, range.from as number) - 1);
    const to = Math.min(times.length - 1, lowerBound(times, range.to as number) + 1);

    const top: [number, number][] = [];
    const bottom: [number, number][] = [];
    const flush = () => {
      if (top.length >= 2) {
        context.beginPath();
        top.forEach(([x, y], index) => (index === 0 ? context.moveTo(x, y) : context.lineTo(x, y)));
        for (let index = bottom.length - 1; index >= 0; index--) {
          context.lineTo(bottom[index]![0], bottom[index]![1]);
        }

        context.closePath();
        context.fill();
      }

      top.length = 0;
      bottom.length = 0;
    };

    context.fillStyle = this.colour;
    for (let index = from; index <= to; index++) {
      const high = valueAt(upper, index);
      const low = valueAt(lower, index);
      const x = chart.timeScale().timeToCoordinate(times[index] as UTCTimestamp);
      const yHigh = high === null ? null : series.priceToCoordinate(high);
      const yLow = low === null ? null : series.priceToCoordinate(low);
      if (x === null || yHigh === null || yLow === null) {
        flush(); // a hole (the start of the history): the fill starts again after it
        continue;
      }

      top.push([x, yHigh]);
      bottom.push([x, yLow]);
    }

    flush();
  }
}
