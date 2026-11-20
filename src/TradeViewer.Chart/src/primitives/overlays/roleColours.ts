// OV6: TradeViewer colours every shape by its role with the palette of TradingView; EWTS sends no colours.

const COLOURS: Readonly<Record<string, string>> = {
  neutral: "#787B86",
  context: "#B2B5BE",
  bullish: "#089981",
  bearish: "#F23645",
  wave: "#2962FF",
  lowerDegree: "#9C27B0",
  invalidation: "#E91E63",
  entry: "#00BCD4",
  add: "#8BC34A",
  stop: "#F23645",
  target: "#64B5F6",
  exit: "#FF9800",
  support: "#FDD835",
  resistance: "#F23645",
};

/** The colour of a role; a role not in the table is drawn gray. */
export function roleColour(role: string): string {
  return COLOURS[role] ?? COLOURS["neutral"]!;
}
