import { hitsSegment, rayEnd, type Point } from "../../tools/geometry";
import type { Drawing } from "../drawing";
import { drawHandles, moveAnchor, strokeLine, strokeStyle, tolerance, type Look, type PaneSize, type Shape } from "../shape";

/** A line from the first anchor through the second, continued to the edge of the chart. */
export class RayShape implements Shape {
  readonly anchors = 2;

  draw(context: CanvasRenderingContext2D, points: readonly Point[], pane: PaneSize, drawing: Drawing, look: Look): void {
    const [a, b] = points;
    if (a === undefined || b === undefined) {
      return;
    }

    strokeStyle(context, drawing);
    strokeLine(context, a, rayEnd(a, b, pane.width, pane.height));
    if (look.selected) {
      drawHandles(context, this.handles(points), drawing.style.color);
    }
  }

  hits(point: Point, points: readonly Point[], pane: PaneSize, drawing: Drawing): boolean {
    const [a, b] = points;
    return a !== undefined && b !== undefined && hitsSegment(point, a, rayEnd(a, b, pane.width, pane.height), tolerance(drawing));
  }

  handles(points: readonly Point[]): Point[] {
    return [...points];
  }

  reshape(points: readonly Point[], handle: number, to: Point): Point[] {
    return moveAnchor(points, handle, to);
  }
}
