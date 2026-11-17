// Exact anchor times across timeframes (plan of Drawing Tools, step 9, accepted by the owner): an anchor on the
// high or low of a candle gets the time of the candle of the smallest timeframe that made that price. On the
// timeframe it was drawn on nothing changes, because that time lies inside the same candle; on every smaller
// timeframe the point sits on the candle that made the extreme instead of on the first one of the period.
import type { Bar } from "../data/bars";
import { timeframeSeconds } from "./timeMapping";

export type Extreme = "high" | "low";

/** Whether a price is the high or the low of the candle; undefined for any other price. */
export function extremeOf(bar: Bar, price: number): Extreme | undefined {
  if (price === bar.high) {
    return "high";
  }

  return price === bar.low ? "low" : undefined;
}

/** The open time of the first candle whose high (or low) is the price; undefined when none is. */
export function madeAt(bars: readonly Bar[], extreme: Extreme, price: number): number | undefined {
  return bars.find((bar) => (extreme === "high" ? bar.high : bar.low) === price)?.time;
}

/** The smallest of the timeframes, by length; undefined when none has a length. */
export function finestOf(codes: readonly string[]): { readonly code: string; readonly seconds: number } | undefined {
  let finest: { code: string; seconds: number } | undefined;
  for (const code of codes) {
    const seconds = timeframeSeconds(code);
    if (seconds !== undefined && (finest === undefined || seconds < finest.seconds)) {
      finest = { code, seconds };
    }
  }

  return finest;
}
