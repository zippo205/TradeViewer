import type { IndicatorPage } from "./indicatorClient";

/**
 * The values of one indicator the window holds, along the candles in memory: the newest page first, older pages
 * added in front while the user scrolls back (CH4), the newest page taken again on a refresh (MD2). Times are
 * unique and increasing; each line has one value per time, null where the indicator is not defined yet.
 */
export class IndicatorValues {
  private times: number[] = [];
  private values: Record<string, (number | null)[]> = {};

  constructor(private readonly lineNames: readonly string[]) {
    for (const line of lineNames) {
      this.values[line] = [];
    }
  }

  get t(): readonly number[] {
    return this.times;
  }

  get oldestTime(): number | undefined {
    return this.times[0];
  }

  get newestTime(): number | undefined {
    return this.times[this.times.length - 1];
  }

  line(name: string): readonly (number | null)[] {
    return this.values[name] ?? [];
  }

  /** The values at a time, or undefined when there is no candle at that time. */
  at(time: number): Record<string, number | null> | undefined {
    const index = lowerBound(this.times, time);
    if (this.times[index] !== time) {
      return undefined;
    }

    const result: Record<string, number | null> = {};
    for (const line of this.lineNames) {
      result[line] = this.values[line]![index] ?? null;
    }

    return result;
  }

  /**
   * Takes the newest page: it replaces everything from its first time on; older values stay. A page that does
   * not reach back to the values held replaces them as a whole.
   * @returns True when the page joined the values held, false when it replaced them.
   */
  setLatest(page: IndicatorPage): boolean {
    const first = page.t[0];
    const newest = this.newestTime;
    if (first === undefined || newest === undefined || first > newest) {
      this.replace(page);
      return first === undefined || newest === undefined;
    }

    const keep = lowerBound(this.times, first);
    this.times = [...this.times.slice(0, keep), ...page.t];
    for (const line of this.lineNames) {
      this.values[line] = [...this.values[line]!.slice(0, keep), ...(page.lines[line] ?? page.t.map(() => null))];
    }

    return true;
  }

  /** Adds an older page in front; what is already held stays. */
  addOlder(page: IndicatorPage): number {
    const oldest = this.oldestTime;
    const count = oldest === undefined ? page.t.length : lowerBound(page.t, oldest);
    this.times = [...page.t.slice(0, count), ...this.times];
    for (const line of this.lineNames) {
      const older = (page.lines[line] ?? page.t.map(() => null)).slice(0, count);
      this.values[line] = [...older, ...this.values[line]!];
    }

    return count;
  }

  private replace(page: IndicatorPage): void {
    this.times = [...page.t];
    for (const line of this.lineNames) {
      this.values[line] = [...(page.lines[line] ?? page.t.map(() => null))];
    }
  }
}

/** Index of the first time that is not before the given time. */
export function lowerBound(times: readonly number[], time: number): number {
  let low = 0;
  let high = times.length;
  while (low < high) {
    const middle = (low + high) >> 1;
    if (times[middle]! < time) {
      low = middle + 1;
    } else {
      high = middle;
    }
  }

  return low;
}
