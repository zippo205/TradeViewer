import type { Point } from "../tools/geometry";
import { waveText, type WaveMark } from "./waveNotation";

const FONT_SIZE = 13;
const FONT = `600 ${FONT_SIZE}px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif`;
/** How far the label keeps from the high or low it stands for. */
const GAP = 6;
const HEIGHT = 20;

/** The box of a label, from its top left to its bottom right corner. */
export interface LabelBox {
  readonly from: Point;
  readonly to: Point;
}

/** An estimate of the text width before the first drawing measured it. */
function estimatedWidth(text: string): number {
  return text.length * FONT_SIZE * 0.62;
}

/**
 * Where the label of a wave mark is drawn: centred over the high (above) or under the low (below) it stands
 * for, at `at`. Circled labels are as wide as they are high at least.
 */
export function labelBox(at: Point, mark: WaveMark, measure: (text: string) => number = estimatedWidth): LabelBox | undefined {
  const written = waveText(mark.degree, mark.label);
  if (written === undefined) {
    return undefined;
  }

  const shown = written.enclosure === "parentheses" ? `(${written.text})` : written.text;
  const width = Math.max(measure(shown) + 8, written.enclosure === "circle" ? HEIGHT : 0);
  const top = mark.side === "above" ? at.y - GAP - HEIGHT : at.y + GAP;
  return { from: { x: at.x - width / 2, y: top }, to: { x: at.x + width / 2, y: top + HEIGHT } };
}

/** Draws the label of a wave mark at the high or low `at`, in the colour; a circle for double brackets. */
export function drawWaveLabel(context: CanvasRenderingContext2D, at: Point, mark: WaveMark, color: string): LabelBox | undefined {
  const written = waveText(mark.degree, mark.label);
  context.font = FONT;
  const box = labelBox(at, mark, (text) => context.measureText(text).width);
  if (written === undefined || box === undefined) {
    return undefined;
  }

  const centre = { x: (box.from.x + box.to.x) / 2, y: (box.from.y + box.to.y) / 2 };
  context.fillStyle = color;
  context.textAlign = "center";
  context.textBaseline = "middle";
  context.fillText(written.enclosure === "parentheses" ? `(${written.text})` : written.text, centre.x, centre.y + 0.5);

  if (written.enclosure === "circle") {
    context.setLineDash([]);
    context.lineWidth = 1.2;
    context.strokeStyle = color;
    context.beginPath();
    context.arc(centre.x, centre.y, (box.to.x - box.from.x) / 2, 0, 2 * Math.PI);
    context.stroke();
  }

  return box;
}
