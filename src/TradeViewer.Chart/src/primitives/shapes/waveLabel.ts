import { insideRectangle, type Point } from "../../tools/geometry";
import type { Drawing } from "../drawing";
import type { Look, PaneSize, Shape } from "../shape";
import { drawWaveLabel, labelBox } from "../waveLabelDrawing";
import { waveMarkOf } from "../waveNotation";

/**
 * A wave label (DR5): one anchor without a price. The layer puts its point on the high (above) or the low
 * (below) of the candle that contains its time, in the timeframe shown, so it follows the candle.
 */
export class WaveLabelShape implements Shape {
  readonly anchors = 1;

  draw(context: CanvasRenderingContext2D, points: readonly Point[], _pane: PaneSize, drawing: Drawing, look: Look): void {
    const [at] = points;
    const mark = waveMarkOf(drawing["wave"]);
    if (at === undefined || mark === undefined) {
      return;
    }

    const box = drawWaveLabel(context, at, mark, drawing.style.color);
    if (look.selected && box !== undefined) {
      context.setLineDash([3, 3]);
      context.lineWidth = 1;
      context.strokeStyle = drawing.style.color;
      context.strokeRect(box.from.x - 2, box.from.y - 2, box.to.x - box.from.x + 4, box.to.y - box.from.y + 4);
    }
  }

  hits(point: Point, points: readonly Point[], _pane: PaneSize, drawing: Drawing): boolean {
    const [at] = points;
    const mark = waveMarkOf(drawing["wave"]);
    const box = at === undefined || mark === undefined ? undefined : labelBox(at, mark);
    return box !== undefined && insideRectangle(point, { x: box.from.x - 2, y: box.from.y - 2 }, { x: box.to.x + 2, y: box.to.y + 2 });
  }

  /** None: a label is moved as a whole, to another candle or to the other side. */
  handles(): Point[] {
    return [];
  }

  reshape(points: readonly Point[]): Point[] {
    return [...points];
  }
}
