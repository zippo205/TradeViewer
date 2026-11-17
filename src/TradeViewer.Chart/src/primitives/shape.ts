import type { Point } from "../tools/geometry";
import type { Drawing } from "./drawing";

export interface PaneSize {
  readonly width: number;
  readonly height: number;
}

/** How a drawing looks at the moment: selected drawings show their handles. */
export interface Look {
  readonly selected: boolean;
  /** The price at a height, as the price scale writes it; for levels that show their price. */
  readonly priceText?: (y: number) => string | undefined;
}

/**
 * One drawing type on the drawing layer: its geometry, how it is drawn, its hit test and its handles. All in
 * pixels of the pane; `points` are the anchors of the drawing in pixels, in their order. While a drawing is
 * placed, `points` may hold fewer anchors than the finished drawing (the preview).
 */
export interface Shape {
  /** The number of anchors a finished drawing has; infinite for a polyline, which the user ends. */
  readonly anchors: number;

  /** For a drawing the user ends (Enter, double-click): the fewest anchors it needs. */
  readonly minimum?: number;

  draw(context: CanvasRenderingContext2D, points: readonly Point[], pane: PaneSize, drawing: Drawing, look: Look): void;

  /** True when the point lies on the drawing (its lines, or its area when that is filled). */
  hits(point: Point, points: readonly Point[], pane: PaneSize, drawing: Drawing): boolean;

  /** The handles of a selected drawing. Handle i < anchors stands for anchor i. */
  handles(points: readonly Point[], pane: PaneSize): Point[];

  /**
   * The anchors in pixels after handle `handle` was dragged to `to`. Points that do not change are returned as
   * the same objects, so the layer converts only the ones that moved.
   */
  reshape(points: readonly Point[], handle: number, to: Point): Point[];
}

/** The default of reshape: handle i is anchor i and follows the pointer. */
export function moveAnchor(points: readonly Point[], handle: number, to: Point): Point[] {
  return points.map((point, index) => (index === handle ? to : point));
}

/** Applies the line style and width of a drawing. */
export function strokeStyle(context: CanvasRenderingContext2D, drawing: Drawing): void {
  const width = drawing.style.width;
  context.strokeStyle = drawing.style.color;
  context.lineWidth = width;
  context.lineCap = drawing.style.lineStyle === "dotted" ? "round" : "butt";
  switch (drawing.style.lineStyle) {
    case "dashed":
      context.setLineDash([4 * width + 2, 3 * width + 1]);
      break;
    case "dotted":
      context.setLineDash([0.1, 2 * width + 1]);
      break;
    default:
      context.setLineDash([]);
  }
}

export function strokeLine(context: CanvasRenderingContext2D, a: Point, b: Point): void {
  context.beginPath();
  context.moveTo(a.x, a.y);
  context.lineTo(b.x, b.y);
  context.stroke();
}

/** The radius of a handle, and how far from it a press still takes it. */
export const HANDLE_RADIUS = 5;
export const HANDLE_HIT = 8;

export function drawHandles(context: CanvasRenderingContext2D, handles: readonly Point[], color: string): void {
  context.setLineDash([]);
  context.lineWidth = 1.5;
  for (const handle of handles) {
    context.beginPath();
    context.arc(handle.x, handle.y, HANDLE_RADIUS, 0, 2 * Math.PI);
    context.fillStyle = "#ffffff";
    context.fill();
    context.strokeStyle = color;
    context.stroke();
  }
}

/** The tolerance of a hit on a line of the drawing: wider for thick lines. */
export function tolerance(drawing: Drawing): number {
  return Math.max(drawing.style.width / 2 + 4, 6);
}
