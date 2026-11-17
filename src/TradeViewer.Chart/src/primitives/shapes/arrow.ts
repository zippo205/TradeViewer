import { hitsSegment, type Point } from "../../tools/geometry";
import type { Drawing } from "../drawing";
import { drawHandles, moveAnchor, strokeLine, strokeStyle, tolerance, type Look, type PaneSize, type Shape } from "../shape";

/** A line from the first anchor to the second with an arrow head at the second. */
export class ArrowShape implements Shape {
  readonly anchors = 2;

  draw(context: CanvasRenderingContext2D, points: readonly Point[], pane: PaneSize, drawing: Drawing, look: Look): void {
    const [a, b] = points;
    if (a === undefined || b === undefined) {
      return;
    }

    strokeStyle(context, drawing);
    strokeLine(context, a, b);

    const head = arrowHead(a, b, drawing.style.width);
    if (head !== undefined) {
      context.setLineDash([]);
      context.beginPath();
      context.moveTo(head[0].x, head[0].y);
      context.lineTo(b.x, b.y);
      context.lineTo(head[1].x, head[1].y);
      context.stroke();
    }

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

/** The two outer points of the head at b, 30 degrees off the line; undefined for a line of no length. */
export function arrowHead(a: Point, b: Point, width: number): [Point, Point] | undefined {
  const length = Math.hypot(b.x - a.x, b.y - a.y);
  if (length === 0) {
    return undefined;
  }

  const size = 6 + 3 * width;
  const angle = Math.atan2(b.y - a.y, b.x - a.x);
  const side = (turn: number): Point => ({ x: b.x - size * Math.cos(angle + turn), y: b.y - size * Math.sin(angle + turn) });
  return [side(Math.PI / 6), side(-Math.PI / 6)];
}
