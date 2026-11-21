import type { IPrimitivePaneRenderer, IPrimitivePaneView, ISeriesPrimitive, PrimitiveHoveredItem, SeriesAttachedParameter, Time } from "lightweight-charts";
import type { RangeSelection, RangeSurface } from "../tools/rangeSelection";

type RenderTarget = Parameters<IPrimitivePaneRenderer["draw"]>[0];

/** The id the chart reports while the range tool is chosen. */
export const RANGE_BAND_ID = "rangeBand";

const FILL = "rgba(41, 98, 255, 0.12)";
const EDGE = "#2962FF";

/**
 * The band of the range tool (SC22): from the left edge of the first candle to the right edge of the last, the whole
 * height of the price pane, with its two edges as lines.
 */
export class RangeBand implements ISeriesPrimitive<Time> {
  private requestUpdate: (() => void) | undefined;
  private selection: RangeSelection | undefined;

  private readonly view: IPrimitivePaneView = {
    zOrder: () => "top",
    renderer: () => ({ draw: (target: RenderTarget) => this.draw(target) }),
  };

  constructor(
    private readonly surface: RangeSurface,
    private readonly barWidth: () => number,
  ) {}

  bind(selection: RangeSelection): void {
    this.selection = selection;
  }

  attached(parameters: SeriesAttachedParameter<Time>): void {
    this.requestUpdate = parameters.requestUpdate;
  }

  detached(): void {
    this.requestUpdate = undefined;
  }

  redraw(): void {
    this.requestUpdate?.();
  }

  paneViews(): readonly IPrimitivePaneView[] {
    return [this.view];
  }

  hitTest(): PrimitiveHoveredItem | null {
    const cursor = this.selection?.cursorAt();
    return cursor === undefined ? null : { externalId: RANGE_BAND_ID, zOrder: "top", cursorStyle: cursor };
  }

  private draw(target: RenderTarget): void {
    const band = this.selection?.band();
    if (band === undefined) {
      return;
    }

    const from = this.surface.xOf(band.from);
    const to = this.surface.xOf(band.to);
    if (from === undefined || to === undefined) {
      return;
    }

    target.useMediaCoordinateSpace((scope) => {
      const half = this.barWidth() / 2;
      const left = from - half;
      const right = to + half;
      const height = scope.mediaSize.height;
      const context = scope.context;
      context.fillStyle = FILL;
      context.fillRect(left, 0, right - left, height);
      context.strokeStyle = EDGE;
      context.lineWidth = 1;
      context.beginPath();
      context.moveTo(Math.round(left) + 0.5, 0);
      context.lineTo(Math.round(left) + 0.5, height);
      context.moveTo(Math.round(right) + 0.5, 0);
      context.lineTo(Math.round(right) + 0.5, height);
      context.stroke();
    });
  }
}
