// The indicators as the chart knows them (IN1): the shape Blazor passes to setIndicators is the IndicatorRecord of
// the Api (chart settings), so nothing is converted in between. The values never pass through Blazor: the module
// fetches them itself (architecture, chapter 10).

export type IndicatorType = "sma" | "ema" | "bb" | "rsi" | "macd" | "atr";

/** The look of one line: colour #RRGGBB or #RRGGBBAA, width 1 to 4 pixels. */
export interface IndicatorStyle {
  readonly line: string;
  readonly color: string;
  readonly width: number;
}

/** One indicator on the chart, as the chart settings hold it. Parameters the type does not have are null. */
export interface IndicatorInstance {
  readonly id: string;
  readonly type: IndicatorType;
  readonly length?: number | null;
  readonly deviation?: number | null;
  readonly fast?: number | null;
  readonly slow?: number | null;
  readonly signal?: number | null;
  readonly price?: string | null;
  readonly styles: readonly IndicatorStyle[];
  readonly hidden: boolean;
}

/** The style entries of the MACD histogram: columns above and below zero. */
export const HISTOGRAM_ABOVE = "histogramAbove";
export const HISTOGRAM_BELOW = "histogramBelow";

const LINES: Record<IndicatorType, readonly string[]> = {
  sma: ["value"],
  ema: ["value"],
  bb: ["basis", "upper", "lower"],
  rsi: ["value"],
  macd: ["macd", "signal", "histogram"],
  atr: ["value"],
};

/** The lines the Api returns for an indicator, in the order the legend shows them. */
export function linesOf(type: IndicatorType): readonly string[] {
  return LINES[type];
}

/** True: drawn on the price, in the price pane; false: in a pane of its own below. */
export function onPrice(type: IndicatorType): boolean {
  return type === "sma" || type === "ema" || type === "bb";
}

export function isIndicatorType(value: unknown): value is IndicatorType {
  return typeof value === "string" && value in LINES;
}

/**
 * What the values depend on: type and parameters, not the styles. Two instances with the same key have the same
 * values; a changed key means fetching again, a changed colour does not.
 */
export function valuesKey(instance: IndicatorInstance): string {
  return parametersOf(instance).join("&");
}

/** The parameters of the request, by the names of /api/indicators. */
export function parametersOf(instance: IndicatorInstance): string[] {
  const parameters = [`type=${instance.type}`];
  const add = (name: string, value: number | string | null | undefined) => {
    if (value !== null && value !== undefined) {
      parameters.push(`${name}=${encodeURIComponent(String(value))}`);
    }
  };

  add("length", instance.length);
  add("deviation", instance.deviation);
  add("fast", instance.fast);
  add("slow", instance.slow);
  add("signal", instance.signal);
  add("price", instance.price);
  return parameters;
}

/** The title of the legend line, as TradingView writes it: "SMA 9 close", "MACD close 12 26 9", "ATR 14". */
export function titleOf(instance: IndicatorInstance): string {
  const parts = (...values: (number | string | null | undefined)[]) =>
    values.filter((value) => value !== null && value !== undefined && value !== "").join(" ");

  switch (instance.type) {
    case "sma":
      return parts("SMA", instance.length, instance.price);
    case "ema":
      return parts("EMA", instance.length, instance.price);
    case "bb":
      return parts("BB", instance.length, instance.price, instance.deviation);
    case "rsi":
      return parts("RSI", instance.length, instance.price);
    case "macd":
      return parts("MACD", instance.price, instance.fast, instance.slow, instance.signal);
    case "atr":
      return parts("ATR", instance.length);
  }
}

/** The style of a line of the instance; the MACD histogram takes the colour of its columns above zero. */
export function styleOf(instance: IndicatorInstance, line: string): IndicatorStyle {
  const name = line === "histogram" ? HISTOGRAM_ABOVE : line;
  return instance.styles.find((style) => style.line === name) ?? { line: name, color: "#2962FF", width: 1 };
}

/**
 * The colour of a MACD column, in TradingView's four shades: above zero growing in the colour above and falling in
 * its lighter shade; below zero falling in the colour below and growing in its lighter shade. As in TradingView, a
 * column grows only when the one before is smaller; without one before it counts as falling.
 */
export function histogramColour(value: number, previous: number | null | undefined, above: string, below: string): string {
  const rising = previous !== null && previous !== undefined && previous < value;
  if (value >= 0) {
    return rising ? above : lighter(above);
  }

  return rising ? lighter(below) : below;
}

/** A colour mixed with white: #26A69A -> about #B2DFDB, #FF5252 -> about #FFCDD2, as TradingView's pale shades. */
export function lighter(colour: string, share = 0.68): string {
  const match = /^#([0-9a-f]{6})([0-9a-f]{2})?$/i.exec(colour);
  if (match === null) {
    return colour;
  }

  const rgb = [0, 2, 4].map((offset) => parseInt(match[1]!.slice(offset, offset + 2), 16));
  const mixed = rgb.map((channel) => Math.round(channel + (255 - channel) * share));
  return "#" + mixed.map((channel) => channel.toString(16).padStart(2, "0")).join("").toUpperCase() + (match[2] ?? "");
}

/** A colour with an opacity, for fills: "#7E57C2" and 0.1 -> "rgba(126, 87, 194, 0.1)". */
export function withOpacity(colour: string, opacity: number): string {
  const match = /^#([0-9a-f]{6})/i.exec(colour);
  if (match === null) {
    return colour;
  }

  const [r, g, b] = [0, 2, 4].map((offset) => parseInt(match[1]!.slice(offset, offset + 2), 16));
  return `rgba(${r}, ${g}, ${b}, ${opacity})`;
}
