import { hitsSegment, insidePolygon, yOnLine, type Point } from "../../tools/geometry";
import type { Drawing } from "../drawing";
import { drawHandles, strokeLine, strokeStyle, tolerance, type Look, type PaneSize, type Shape } from "../shape";

/**
 * Two parallel lines: the first two anchors make the line, the third lies on the parallel and so sets its
 * distance. Dragging the line keeps the distance; dragging the third anchor changes it.
 */
export class ParallelChannelShape implements Shape {
  readonly anchors = 3;

  draw(context: CanvasRenderingContext2D, points: readonly Point[], pane: PaneSize, drawing: Drawing, look: Look): void {
    const [a, b] = points;
    if (a === undefined || b === undefined) {
      return;
    }

    const parallel = parallelOf(points);
    if (parallel !== undefined && drawing.style.fill) {
      context.beginPath();
      context.moveTo(a.x, a.y);
      context.lineTo(b.x, b.y);
      context.lineTo(parallel[1].x, parallel[1].y);
      context.lineTo(parallel[0].x, parallel[0].y);
      context.closePath();
      context.fillStyle = drawing.style.fill;
      context.fill();
    }

    strokeStyle(context, drawing);
    strokeLine(context, a, b);
    if (parallel !== undefined) {
      strokeLine(context, parallel[0], parallel[1]);
    }

    if (look.selected) {
      drawHandles(context, this.handles(points), drawing.style.color);
    }
  }

  hits(point: Point, points: readonly Point[], _pane: PaneSize, drawing: Drawing): boolean {
    const [a, b] = points;
    if (a === undefined || b === undefined) {
      return false;
    }

    const reach = tolerance(drawing);
    if (hitsSegment(point, a, b, reach)) {
      return true;
    }

    const parallel = parallelOf(points);
    if (parallel === undefined) {
      return false;
    }

    return hitsSegment(point, parallel[0], parallel[1], reach) || (!!drawing.style.fill && insidePolygon(point, [a, b, parallel[1], parallel[0]]));
  }

  handles(points: readonly Point[]): Point[] {
    return [...points];
  }

  reshape(points: readonly Point[], handle: number, to: Point): Point[] {
    const [a, b, c] = points;
    if (a === undefined || b === undefined || c === undefined || handle === 2) {
      return points.map((point, index) => (index === handle ? to : point));
    }

    const offset = offsetOf(a, b, c);
    const moved = handle === 0 ? [to, b] : [a, to];
    const lineY = yOnLine(moved[0]!, moved[1]!, c.x);
    const third = lineY === undefined ? { x: moved[0]!.x + offset.x, y: c.y } : { x: c.x, y: lineY + offset.y };
    return [moved[0]!, moved[1]!, third];
  }
}

/**
 * How far the parallel lies from the line: vertically (the third anchor's height above or below the line at its
 * time), or sideways when the line is vertical.
 */
function offsetOf(a: Point, b: Point, c: Point): Point {
  const lineY = yOnLine(a, b, c.x);
  return lineY === undefined ? { x: c.x - a.x, y: 0 } : { x: 0, y: c.y - lineY };
}

/** The ends of the parallel, opposite the first two anchors; undefined while the third is not placed. */
export function parallelOf(points: readonly Point[]): [Point, Point] | undefined {
  const [a, b, c] = points;
  if (a === undefined || b === undefined || c === undefined) {
    return undefined;
  }

  const offset = offsetOf(a, b, c);
  return [
    { x: a.x + offset.x, y: a.y + offset.y },
    { x: b.x + offset.x, y: b.y + offset.y },
  ];
}
