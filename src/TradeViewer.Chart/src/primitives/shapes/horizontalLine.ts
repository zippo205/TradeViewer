import type { Point } from "../../tools/geometry";
import type { Drawing } from "../drawing";
import { drawHandles, moveAnchor, strokeLine, strokeStyle, tolerance, type Look, type PaneSize, type Shape } from "../shape";

/** A line across the whole chart at the price of its anchor; the layer shows the price on the scale. */
export class HorizontalLineShape implements Shape {
  readonly anchors = 1;

  draw(context: CanvasRenderingContext2D, points: readonly Point[], pane: PaneSize, drawing: Drawing, look: Look): void {
    const [a] = points;
    if (a === undefined) {
      return;
    }

    strokeStyle(context, drawing);
    strokeLine(context, { x: 0, y: a.y }, { x: pane.width, y: a.y });
    if (look.selected) {
      drawHandles(context, this.handles(points), drawing.style.color);
    }
  }

  hits(point: Point, points: readonly Point[], _pane: PaneSize, drawing: Drawing): boolean {
    const [a] = points;
    return a !== undefined && Math.abs(point.y - a.y) <= tolerance(drawing);
  }

  handles(points: readonly Point[]): Point[] {
    return [...points];
  }

  reshape(points: readonly Point[], handle: number, to: Point): Point[] {
    return moveAnchor(points, handle, to);
  }
}
