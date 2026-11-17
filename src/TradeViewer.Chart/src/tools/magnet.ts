import type { Bar } from "../data/bars";

/**
 * The magnet (DR2): the open, high, low or close of the candle that is nearest to the pointer's height, in
 * pixels, so that it works the same on a linear and a logarithmic scale. Undefined when no value can be placed.
 */
export function snapPrice(bar: Bar, y: number, priceToY: (price: number) => number | undefined): number | undefined {
  let best: number | undefined;
  let bestDistance = Number.POSITIVE_INFINITY;
  for (const price of [bar.open, bar.high, bar.low, bar.close]) {
    const at = priceToY(price);
    if (at === undefined) {
      continue;
    }

    const distance = Math.abs(at - y);
    if (distance < bestDistance) {
      best = price;
      bestDistance = distance;
    }
  }

  return best;
}
