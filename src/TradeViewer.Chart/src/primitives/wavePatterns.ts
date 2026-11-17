// The Elliott wave patterns of DR5, as TradingView offers them: a polyline that starts at the beginning of the move
// and has one labelled point per wave. They are polylines with labels on their points, nothing else, so they can be
// reshaped, relabelled and have their line hidden like any polyline.
import type { Anchor } from "./drawing";
import type { WaveDegree, WaveMark, WaveSide } from "./waveNotation";

/** A label on a point of a polyline, as the record holds it. */
export interface PointLabel {
  readonly point: number;
  readonly mark: WaveMark;
}

/**
 * The labels for the points of a count: label i goes on point i + 1 (point 0 is the start of the move). A point
 * higher than the one before is labelled above it, a lower one below; the start gets no label.
 */
export function patternLabels(anchors: readonly Anchor[], labels: readonly string[], degree: WaveDegree): PointLabel[] {
  const result: PointLabel[] = [];
  for (let point = 1; point < anchors.length && point <= labels.length; point++) {
    result.push({ point, mark: { degree, label: labels[point - 1]!, side: sideOf(anchors, point) } });
  }

  return result;
}

/** The labels with their sides set again from the prices of the points, e.g. after a point was reshaped. */
export function withSides(anchors: readonly Anchor[], labels: readonly PointLabel[]): PointLabel[] {
  return labels.map((label) => ({ ...label, mark: { ...label.mark, side: sideOf(anchors, label.point) } }));
}

/** Above when the point is not lower than the one before (or, for the first point, than the one after). */
function sideOf(anchors: readonly Anchor[], point: number): WaveSide {
  const price = anchors[point]?.price ?? 0;
  const neighbour = point > 0 ? anchors[point - 1]?.price : anchors[point + 1]?.price;
  if (neighbour === undefined || neighbour === null) {
    return "above";
  }

  return price >= neighbour ? "above" : "below";
}
