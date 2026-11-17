// A drawing as the chart module holds it: the record of the Api (DrawingRecord) with times in Unix seconds.
// The module never invents fields: what it does not know (version, source, symbol, ...) is carried through
// unchanged, so Web gets back what it gave plus the user's change.

/** The tools of DR1, as the Api writes them. */
export type ToolCode =
  | "trendLine"
  | "ray"
  | "horizontalLine"
  | "verticalLine"
  | "parallelChannel"
  | "rectangle"
  | "arrow"
  | "textNote"
  | "polyline"
  | "fibonacciRetracement"
  | "fibonacciExtension"
  | "waveLabel";

export type LineStyle = "solid" | "dashed" | "dotted";

/** A point of a drawing: time in Unix seconds (UTC) and price. Price is null only for a wave label. */
export interface Anchor {
  readonly time: number;
  readonly price: number | null;
}

export interface DrawingStyle {
  /** #RRGGBB or #RRGGBBAA */
  readonly color: string;
  /** Line width in pixels, 1 to 4. */
  readonly width: number;
  readonly lineStyle: LineStyle;
  /** The area of a rectangle or channel; null or missing for none. */
  readonly fill?: string | null;
}

export interface Drawing {
  readonly id: string;
  readonly tool: ToolCode;
  readonly locked: boolean;
  readonly anchors: readonly Anchor[];
  readonly style: DrawingStyle;
  readonly text?: string | null;
  /** Fields of other tools and of Web, carried through unchanged. */
  readonly [other: string]: unknown;
}

/** Colours of the plan (OV6, DR4): lines blue, Fibonacci extension light blue, retracement gray. */
export const LINE_COLOR = "#2962FF";
export const AREA_FILL = "#2962FF1A";

/** The style a new drawing of the tool starts with; the same as DrawingTools.DefaultStyle in the Domain. */
export function defaultStyle(tool: ToolCode): DrawingStyle {
  switch (tool) {
    case "fibonacciRetracement":
      return { color: "#787B86", width: 1, lineStyle: "solid" };
    case "fibonacciExtension":
      return { color: "#64B5F6", width: 1, lineStyle: "solid" };
    case "rectangle":
    case "parallelChannel":
      return { color: LINE_COLOR, width: 1, lineStyle: "solid", fill: AREA_FILL };
    default:
      return { color: LINE_COLOR, width: 2, lineStyle: "solid" };
  }
}

/** The text a new text note starts with; the user changes it in the bar of the selected drawing. */
export const NEW_NOTE_TEXT = "Text";
