import { hitsSegment, type Point } from "../../tools/geometry";
import type { Drawing } from "../drawing";
import { fibonacciLevels, levelsOf, ratioText } from "../fibonacci";
import { drawHandles, moveAnchor, strokeLine, strokeStyle, tolerance, type Look, type PaneSize, type Shape } from "../shape";

const FONT = `11px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif`;

/**
 * Fibonacci retracement (2 anchors) or extension (3 anchors), DR4. Its levels are those copied from the preset
 * when it was drawn. Each level is a line from the leftmost anchor to the right edge of the chart with its ratio
 * and price at the left end; a dashed line joins the anchors. One colour for all levels.
 */
export class FibonacciShape implements Shape {
  readonly anchors: number;

  constructor(private readonly extension: boolean) {
    this.anchors = extension ? 3 : 2;
  }

  draw(context: CanvasRenderingContext2D, points: readonly Point[], pane: PaneSize, drawing: Drawing, look: Look): void {
    // The dashed line through the anchors shows the move the levels are measured on.
    if (points.length >= 2) {
      context.setLineDash([4, 4]);
      context.lineWidth = 1;
      context.strokeStyle = drawing.style.color;
      context.beginPath();
      points.forEach((point, index) => (index === 0 ? context.moveTo(point.x, point.y) : context.lineTo(point.x, point.y)));
      context.stroke();
    }

    const levels = fibonacciLevels(points, this.extension, levelsOf(drawing["levels"]));
    if (levels !== undefined) {
      const left = Math.min(...points.map((point) => point.x));
      strokeStyle(context, drawing);
      context.font = FONT;
      context.textAlign = "left";
      context.textBaseline = "bottom";
      context.fillStyle = drawing.style.color;
      for (const level of levels) {
        strokeLine(context, { x: left, y: level.y }, { x: pane.width, y: level.y });
        const price = look.priceText?.(level.y);
        context.fillText(price === undefined ? ratioText(level.ratio) : `${ratioText(level.ratio)} (${price})`, left + 3, level.y - 2);
      }
    }

    if (look.selected) {
      drawHandles(context, points, drawing.style.color);
    }
  }

  hits(point: Point, points: readonly Point[], pane: PaneSize, drawing: Drawing): boolean {
    const reach = tolerance(drawing);
    for (let index = 1; index < points.length; index++) {
      if (hitsSegment(point, points[index - 1]!, points[index]!, reach)) {
        return true;
      }
    }

    const levels = fibonacciLevels(points, this.extension, levelsOf(drawing["levels"]));
    if (levels === undefined) {
      return false;
    }

    const left = Math.min(...points.map((at) => at.x));
    return levels.some((level) => hitsSegment(point, { x: left, y: level.y }, { x: pane.width, y: level.y }, reach));
  }

  handles(points: readonly Point[]): Point[] {
    return [...points];
  }

  reshape(points: readonly Point[], handle: number, to: Point): Point[] {
    return moveAnchor(points, handle, to);
  }
}
