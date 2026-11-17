import type { ToolCode } from "../drawing";
import type { Shape } from "../shape";
import { ArrowShape } from "./arrow";
import { FibonacciShape } from "./fibonacci";
import { HorizontalLineShape } from "./horizontalLine";
import { ParallelChannelShape } from "./parallelChannel";
import { PolylineShape } from "./polyline";
import { RayShape } from "./ray";
import { RectangleShape } from "./rectangle";
import { TextNoteShape } from "./textNote";
import { TrendLineShape } from "./trendLine";
import { VerticalLineShape } from "./verticalLine";
import { WaveLabelShape } from "./waveLabel";

/** One shape per tool of DR1. */
const shapes: Partial<Record<ToolCode, Shape>> = {
  trendLine: new TrendLineShape(),
  ray: new RayShape(),
  horizontalLine: new HorizontalLineShape(),
  verticalLine: new VerticalLineShape(),
  rectangle: new RectangleShape(),
  arrow: new ArrowShape(),
  textNote: new TextNoteShape(),
  parallelChannel: new ParallelChannelShape(),
  fibonacciRetracement: new FibonacciShape(false),
  fibonacciExtension: new FibonacciShape(true),
  polyline: new PolylineShape(),
  waveLabel: new WaveLabelShape(),
};

/** The shape of a tool, or undefined for a tool this build cannot draw yet (its drawings are kept, not shown). */
export function shapeOf(tool: ToolCode): Shape | undefined {
  return shapes[tool];
}
