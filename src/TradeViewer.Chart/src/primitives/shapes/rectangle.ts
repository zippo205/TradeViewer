import { hitsSegment, insideRectangle, type Point } from "../../tools/geometry";
import type { Drawing } from "../drawing";
import { drawHandles, moveAnchor, strokeStyle, tolerance, type Look, type PaneSize, type Shape } from "../shape";

/**
 * A rectangle between two opposite corners, the anchors; pale fill. It has four handles: the two anchors, then
 * the two other corners, each of which changes the time of one anchor and the price of the other.
 */
export class RectangleShape implements Shape {
  readonly anchors = 2;

  draw(context: CanvasRenderingContext2D, points: readonly Point[], pane: PaneSize, drawing: Drawing, look: Look): void {
    const [a, b] = points;
    if (a === undefined || b === undefined) {
      return;
    }

    const x = Math.min(a.x, b.x);
    const y = Math.min(a.y, b.y);
    const width = Math.abs(b.x - a.x);
    const height = Math.abs(b.y - a.y);
    if (drawing.style.fill) {
      context.fillStyle = drawing.style.fill;
      context.fillRect(x, y, width, height);
    }

    strokeStyle(context, drawing);
    context.strokeRect(x, y, width, height);
    if (look.selected) {
      drawHandles(context, this.handles(points), drawing.style.color);
    }
  }

  hits(point: Point, points: readonly Point[], _pane: PaneSize, drawing: Drawing): boolean {
    const [a, b] = points;
    if (a === undefined || b === undefined) {
      return false;
    }

    if (drawing.style.fill && insideRectangle(point, a, b)) {
      return true;
    }

    const corners = this.corners(a, b);
    const reach = tolerance(drawing);
    return corners.some((corner, index) => hitsSegment(point, corner, corners[(index + 1) % 4]!, reach));
  }

  handles(points: readonly Point[]): Point[] {
    const [a, b] = points;
    if (a === undefined || b === undefined) {
      return [...points];
    }

    return [a, b, { x: a.x, y: b.y }, { x: b.x, y: a.y }];
  }

  reshape(points: readonly Point[], handle: number, to: Point): Point[] {
    const [a, b] = points;
    if (a === undefined || b === undefined || handle < 2) {
      return moveAnchor(points, handle, to);
    }

    // The corner below or above a: a takes its time, b its price; the corner of b the other way round.
    return handle === 2 ? [{ x: to.x, y: a.y }, { x: b.x, y: to.y }] : [{ x: a.x, y: to.y }, { x: to.x, y: b.y }];
  }

  private corners(a: Point, b: Point): Point[] {
    return [a, { x: b.x, y: a.y }, b, { x: a.x, y: b.y }];
  }
}
