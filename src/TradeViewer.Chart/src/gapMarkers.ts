import type {
  IChartApi,
  IPrimitivePaneRenderer,
  IPrimitivePaneView,
  ISeriesPrimitive,
  PrimitiveHoveredItem,
  SeriesAttachedParameter,
  Time,
  UTCTimestamp,
} from "lightweight-charts";
import type { GapMarker } from "./data/gaps";

type RenderTarget = Parameters<IPrimitivePaneRenderer["draw"]>[0];

/** The size of an icon and how far its centre sits above the lower edge of the pane, in pixels. */
const RADIUS = 8;
const FROM_BOTTOM = 14;
const COLOUR = "#f7a600";

/** The prefix of the ids the chart reports for a hovered gap icon; the index of the marker follows. */
export const GAP_ID_PREFIX = "gap:";

/**
 * Draws a small icon at the bottom of the chart for every gap the source confirmed (MD4), where the gap
 * is: between the last candle before it and the first one after it. Pointing at an icon is reported by
 * the chart as a hovered object, and the pane shows the text of the gap.
 */
export class GapMarkers implements ISeriesPrimitive<Time> {
  private chart: IChartApi | undefined;
  private requestUpdate: (() => void) | undefined;
  private markers: readonly GapMarker[] = [];
  private drawn: { x: number; y: number; index: number }[] = [];

  private readonly view: IPrimitivePaneView = {
    zOrder: () => "top",
    renderer: () => ({ draw: (target: RenderTarget) => this.draw(target) }),
  };

  attached(parameters: SeriesAttachedParameter<Time>): void {
    this.chart = parameters.chart;
    this.requestUpdate = parameters.requestUpdate;
  }

  detached(): void {
    this.chart = undefined;
    this.requestUpdate = undefined;
  }

  set(markers: readonly GapMarker[]): void {
    this.markers = markers;
    this.requestUpdate?.();
  }

  get(index: number): GapMarker | undefined {
    return this.markers[index];
  }

  paneViews(): readonly IPrimitivePaneView[] {
    return [this.view];
  }

  hitTest(x: number, y: number): PrimitiveHoveredItem | null {
    for (const icon of this.drawn) {
      if (Math.abs(x - icon.x) <= RADIUS + 2 && Math.abs(y - icon.y) <= RADIUS + 2) {
        return { externalId: GAP_ID_PREFIX + icon.index, zOrder: "top", cursorStyle: "help" };
      }
    }

    return null;
  }

  private draw(target: RenderTarget): void {
    const chart = this.chart;
    this.drawn = [];
    if (chart === undefined || this.markers.length === 0) {
      return;
    }

    target.useMediaCoordinateSpace((scope) => {
      const context = scope.context;
      const y = scope.mediaSize.height - FROM_BOTTOM;

      this.markers.forEach((marker, index) => {
        const before = chart.timeScale().timeToCoordinate(marker.beforeTime as UTCTimestamp);
        if (before === null) {
          return;
        }

        const after = marker.afterTime === undefined ? null : chart.timeScale().timeToCoordinate(marker.afterTime as UTCTimestamp);
        const x = after === null ? before : (before + after) / 2;
        if (x < -RADIUS || x > scope.mediaSize.width + RADIUS) {
          return;
        }

        context.beginPath();
        context.arc(x, y, RADIUS, 0, 2 * Math.PI);
        context.fillStyle = COLOUR;
        context.fill();

        context.fillStyle = "#ffffff";
        context.font = "bold 12px sans-serif";
        context.textAlign = "center";
        context.textBaseline = "middle";
        context.fillText("!", x, y + 0.5);

        this.drawn.push({ x, y, index });
      });
    });
  }
}
