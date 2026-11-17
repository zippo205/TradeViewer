import { hitsSegment, type Point } from "../../tools/geometry";
import type { Drawing } from "../drawing";
import { drawHandles, moveAnchor, strokeLine, strokeStyle, tolerance, type Look, type PaneSize, type Shape } from "../shape";

/** A straight line between two anchors. */
export class TrendLineShape implements Shape {
  readonly anchors = 2;

  draw(context: CanvasRenderingContext2D, points: readonly Point[], pane: PaneSize, drawing: Drawing, look: Look): void {
    const [a, b] = points;
    if (a === undefined || b === undefined) {
      return;
    }

    strokeStyle(context, drawing);
    strokeLine(context, a, b);
    if (look.selected) {
      drawHandles(context, this.handles(points), drawing.style.color);
    }
  }

  hits(point: Point, points: readonly Point[], _pane: PaneSize, drawing: Drawing): boolean {
    const [a, b] = points;
    return a !== undefined && b !== undefined && hitsSegment(point, a, b, tolerance(drawing));
  }

  handles(points: readonly Point[]): Point[] {
    return [...points];
  }

  reshape(points: readonly Point[], handle: number, to: Point): Point[] {
    return moveAnchor(points, handle, to);
  }
}
