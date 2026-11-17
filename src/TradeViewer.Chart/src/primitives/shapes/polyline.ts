import { hitsSegment, insideRectangle, type Point } from "../../tools/geometry";
import type { Drawing } from "../drawing";
import { drawHandles, moveAnchor, strokeStyle, tolerance, type Look, type PaneSize, type Shape } from "../shape";
import { drawWaveLabel, labelBox } from "../waveLabelDrawing";
import { waveMarkOf, type WaveMark } from "../waveNotation";

/** A wave label on a point of a polyline, as the record holds it. */
interface PointLabel {
  readonly point: number;
  readonly mark: WaveMark;
}

/**
 * Connected points, for wave counts (DR1, DR5): as many as the user clicks, ended by a double-click or Enter.
 * Wave labels can sit on its points; with "line hidden" only the labels show.
 */
export class PolylineShape implements Shape {
  readonly anchors = Number.POSITIVE_INFINITY;
  readonly minimum = 2;

  draw(context: CanvasRenderingContext2D, points: readonly Point[], pane: PaneSize, drawing: Drawing, look: Look): void {
    const hidden = drawing["lineHidden"] === true;
    if (!hidden && points.length >= 2) {
      strokeStyle(context, drawing);
      context.lineJoin = "round";
      context.beginPath();
      points.forEach((point, index) => (index === 0 ? context.moveTo(point.x, point.y) : context.lineTo(point.x, point.y)));
      context.stroke();
    }

    for (const label of labelsOf(drawing)) {
      const at = points[label.point];
      if (at !== undefined) {
        drawWaveLabel(context, at, label.mark, drawing.style.color);
      }
    }

    if (look.selected || (hidden && labelsOf(drawing).length === 0)) {
      drawHandles(context, points, drawing.style.color);
    }
  }

  hits(point: Point, points: readonly Point[], _pane: PaneSize, drawing: Drawing): boolean {
    const reach = tolerance(drawing);
    if (drawing["lineHidden"] !== true) {
      for (let index = 1; index < points.length; index++) {
        if (hitsSegment(point, points[index - 1]!, points[index]!, reach)) {
          return true;
        }
      }
    }

    // The labels, and the points themselves, so that a polyline with its line hidden can still be taken.
    for (const label of labelsOf(drawing)) {
      const at = points[label.point];
      const box = at === undefined ? undefined : labelBox(at, label.mark);
      if (box !== undefined && insideRectangle(point, box.from, box.to)) {
        return true;
      }
    }

    return points.some((at) => Math.hypot(at.x - point.x, at.y - point.y) <= reach);
  }

  handles(points: readonly Point[]): Point[] {
    return [...points];
  }

  reshape(points: readonly Point[], handle: number, to: Point): Point[] {
    return moveAnchor(points, handle, to);
  }
}

/** The valid labels on the points of a polyline. */
export function labelsOf(drawing: Drawing): PointLabel[] {
  const labels = drawing["pointLabels"];
  if (!Array.isArray(labels)) {
    return [];
  }

  const valid: PointLabel[] = [];
  for (const label of labels as { point?: unknown; mark?: unknown }[]) {
    const mark = waveMarkOf(label?.mark);
    if (mark !== undefined && typeof label.point === "number") {
      valid.push({ point: label.point, mark });
    }
  }

  return valid;
}
