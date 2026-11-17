import type { ToolCode } from "../drawing";
import type { Shape } from "../shape";
import { ArrowShape } from "./arrow";
import { HorizontalLineShape } from "./horizontalLine";
import { ParallelChannelShape } from "./parallelChannel";
import { RayShape } from "./ray";
import { RectangleShape } from "./rectangle";
import { TextNoteShape } from "./textNote";
import { TrendLineShape } from "./trendLine";
import { VerticalLineShape } from "./verticalLine";

/** One shape per tool; polyline, Fibonacci and wave labels follow with steps 7 and 8. */
const shapes: Partial<Record<ToolCode, Shape>> = {
  trendLine: new TrendLineShape(),
  ray: new RayShape(),
  horizontalLine: new HorizontalLineShape(),
  verticalLine: new VerticalLineShape(),
  rectangle: new RectangleShape(),
  arrow: new ArrowShape(),
  textNote: new TextNoteShape(),
  parallelChannel: new ParallelChannelShape(),
};

/** The shape of a tool, or undefined for a tool this build cannot draw yet (its drawings are kept, not shown). */
export function shapeOf(tool: ToolCode): Shape | undefined {
  return shapes[tool];
}
