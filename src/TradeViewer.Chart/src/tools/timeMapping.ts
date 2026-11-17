// Time to bar index and back (DR3, architecture chapter 10). A drawing keeps times and prices; the
// chart places it by bar index. An anchor always sits on a whole index: on the candle that contains
// its time, or, outside the candles in memory, so many whole timeframes before the first or after
// the last candle. Lightweight Charts 5.3 draws a fractional index at x = 0, so whole indices are
// also the only ones that are safe to hand to the library.

const UNIT_SECONDS: Readonly<Record<string, number>> = { m: 60, h: 3600, d: 86_400, w: 604_800 };

/**
 * The length of a timeframe in seconds (15m, 1h, 4h, 1d, 1w), or undefined for a code without a fixed
 * length. Calendar months (1M, later) need their own counting and are not handled yet.
 */
export function timeframeSeconds(code: string): number | undefined {
  const match = /^([0-9]+)([mhdw])$/.exec(code);
  if (match === null) {
    return undefined;
  }

  const count = Number(match[1]);
  return count > 0 ? count * UNIT_SECONDS[match[2]!]! : undefined;
}

/**
 * Maps between times (Unix seconds, UTC) and whole bar indices for the candles in memory. Index 0 is
 * the oldest candle; indices below 0 and above the last one are the space before and after them.
 */
export class TimeMapping {
  /**
   * @param times open times of the candles in memory, oldest first, strictly increasing
   * @param step the length of the timeframe in seconds, used outside the candles
   */
  constructor(
    private readonly times: readonly number[],
    private readonly step: number,
  ) {
    if (!(step > 0)) {
      throw new RangeError("The timeframe needs a positive length.");
    }
  }

  get isEmpty(): boolean {
    return this.times.length === 0;
  }

  /**
   * The index of the candle that contains the time: the last candle whose open time is not after it.
   * Before the first candle: whole timeframes back, rounded outwards (a time inside the timeframe
   * before the first candle belongs to that earlier candle). After the last: whole timeframes on.
   * Undefined without candles.
   */
  indexOf(time: number): number | undefined {
    const times = this.times;
    const count = times.length;
    if (count === 0 || !Number.isFinite(time)) {
      return undefined;
    }

    const first = times[0]!;
    if (time < first) {
      return -Math.ceil((first - time) / this.step);
    }

    const lastIndex = count - 1;
    const last = times[lastIndex]!;
    if (time >= last) {
      return lastIndex + Math.floor((time - last) / this.step);
    }

    // Binary search for the last open time that is not after the time.
    let low = 0;
    let high = lastIndex;
    while (low < high) {
      const middle = (low + high + 1) >> 1;
      if (times[middle]! <= time) {
        low = middle;
      } else {
        high = middle - 1;
      }
    }

    return low;
  }

  /**
   * The time of a whole index: the open time of its candle, or the same counting of whole timeframes
   * outside the candles. A fractional index is first rounded to the nearest whole one. Undefined
   * without candles.
   */
  timeOf(index: number): number | undefined {
    const times = this.times;
    const count = times.length;
    if (count === 0 || !Number.isFinite(index)) {
      return undefined;
    }

    const whole = Math.round(index);
    if (whole < 0) {
      return times[0]! + whole * this.step;
    }

    const lastIndex = count - 1;
    if (whole > lastIndex) {
      return times[lastIndex]! + (whole - lastIndex) * this.step;
    }

    return times[whole]!;
  }
}
