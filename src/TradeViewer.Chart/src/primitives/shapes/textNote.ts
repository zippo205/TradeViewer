import { insideRectangle, type Point } from "../../tools/geometry";
import type { Drawing } from "../drawing";
import { drawHandles, moveAnchor, type Look, type PaneSize, type Shape } from "../shape";

const FONT_SIZE = 14;
const FONT = `${FONT_SIZE}px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif`;
const LINE_HEIGHT = 18;

/**
 * A text at an anchor: the anchor is the left end of the first line, in its middle. Lines are split at line
 * breaks. The widths come from the last drawing (canvas text measurement); before that, an estimate.
 */
export class TextNoteShape implements Shape {
  readonly anchors = 1;
  private readonly widths = new Map<string, number>();

  draw(context: CanvasRenderingContext2D, points: readonly Point[], pane: PaneSize, drawing: Drawing, look: Look): void {
    const [a] = points;
    if (a === undefined) {
      return;
    }

    context.font = FONT;
    context.textBaseline = "middle";
    context.textAlign = "left";
    context.fillStyle = drawing.style.color;
    lines(drawing).forEach((line, index) => {
      this.widths.set(line, context.measureText(line).width);
      context.fillText(line, a.x, a.y + index * LINE_HEIGHT);
    });

    if (look.selected) {
      const [from, to] = this.box(a, drawing);
      context.setLineDash([3, 3]);
      context.lineWidth = 1;
      context.strokeStyle = drawing.style.color;
      context.strokeRect(from.x, from.y, to.x - from.x, to.y - from.y);
      drawHandles(context, this.handles(points), drawing.style.color);
    }
  }

  hits(point: Point, points: readonly Point[], _pane: PaneSize, drawing: Drawing): boolean {
    const [a] = points;
    if (a === undefined) {
      return false;
    }

    const [from, to] = this.box(a, drawing);
    return insideRectangle(point, from, to);
  }

  handles(points: readonly Point[]): Point[] {
    return [...points];
  }

  reshape(points: readonly Point[], handle: number, to: Point): Point[] {
    return moveAnchor(points, handle, to);
  }

  /** The corners of the text with a margin of 3 pixels. */
  box(anchor: Point, drawing: Drawing): [Point, Point] {
    const all = lines(drawing);
    const width = Math.max(...all.map((line) => this.widths.get(line) ?? line.length * FONT_SIZE * 0.6));
    return [
      { x: anchor.x - 3, y: anchor.y - LINE_HEIGHT / 2 - 3 },
      { x: anchor.x + width + 3, y: anchor.y + (all.length - 0.5) * LINE_HEIGHT + 3 },
    ];
  }
}

function lines(drawing: Drawing): string[] {
  const text = drawing.text ?? "";
  return text === "" ? [""] : text.split(/\r?\n/);
}
