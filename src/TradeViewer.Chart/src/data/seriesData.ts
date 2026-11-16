import { sameBar, type Bar, type BarPage } from "./bars";

/** The first page of a series and the size of the pages loaded while scrolling back (CH4). */
export const FIRST_PAGE_SIZE = 2000;
export const OLDER_PAGE_SIZE = 5000;

/** Older candles are requested when fewer than this many are left to the left of the view. */
export const LOAD_OLDER_THRESHOLD = 500;

/** What a refresh changed, so the chart redraws no more than needed. */
export interface RefreshResult {
  /** Index of the first bar that is new or different, or -1 when nothing changed. */
  readonly firstChanged: number;
  /** True: the candles in memory were replaced as a whole (too much had happened in between). */
  readonly reset: boolean;
}

/**
 * The candles of one asset and timeframe that the window holds: the newest page first, older pages
 * added in front while the user scrolls back. Times are unique and increasing.
 */
export class SeriesData {
  private items: Bar[] = [];
  private partialLast = false;
  private more = true;

  get bars(): readonly Bar[] {
    return this.items;
  }

  /** True: the last bar is the current candle, still in progress (MD8). */
  get partial(): boolean {
    return this.partialLast;
  }

  /** True: the store may hold candles older than the oldest one here. */
  get hasMore(): boolean {
    return this.more;
  }

  /** The time to ask the next older page with (`to` is exclusive), or undefined without candles. */
  get oldestTime(): number | undefined {
    return this.items[0]?.time;
  }

  /**
   * Takes the newest page: the first one, or a refresh after new candles. The page replaces everything
   * from its first candle on; older candles stay. When the page does not reach back to the candles in
   * memory (the window slept for a long time), they are replaced as a whole.
   */
  setLatest(page: BarPage, pageSize: number = FIRST_PAGE_SIZE): RefreshResult {
    const first = page.bars[0];
    const last = this.items[this.items.length - 1];

    if (first === undefined) {
      const hadBars = this.items.length > 0;
      this.items = [];
      this.partialLast = false;
      this.more = false;
      return { firstChanged: hadBars ? 0 : -1, reset: hadBars };
    }

    if (last === undefined || first.time > last.time) {
      const hadBars = this.items.length > 0;
      this.items = [...page.bars];
      this.partialLast = page.partial;
      this.more = closedCount(page) >= pageSize;
      return { firstChanged: 0, reset: hadBars };
    }

    const keep = lowerBound(this.items, first.time);
    const merged = [...this.items.slice(0, keep), ...page.bars];

    let firstChanged = -1;
    for (let index = keep; index < merged.length; index++) {
      const before = this.items[index];
      if (before === undefined || !sameBar(before, merged[index]!)) {
        firstChanged = index;
        break;
      }
    }

    if (firstChanged < 0 && merged.length !== this.items.length) {
      firstChanged = merged.length; // candles at the end are gone
    }

    if (firstChanged < 0 && this.partialLast !== page.partial) {
      firstChanged = merged.length - 1; // the current candle became a closed one, with the same values
    }

    this.items = merged;
    this.partialLast = page.partial;
    return { firstChanged, reset: false };
  }

  /** Adds an older page in front. A page shorter than asked for means the start of the history is reached. */
  addOlder(page: BarPage, pageSize: number = OLDER_PAGE_SIZE): number {
    const oldest = this.oldestTime;
    const older = oldest === undefined ? [...page.bars] : page.bars.filter((bar) => bar.time < oldest);

    this.items = [...older, ...this.items];
    this.more = page.bars.length >= pageSize;
    return older.length;
  }
}

/**
 * True when an older page should be requested now: the view came close to the oldest candle in memory,
 * there may be more, and no request is on its way.
 */
export function shouldLoadOlder(firstVisibleIndex: number, hasMore: boolean, loading: boolean): boolean {
  return hasMore && !loading && firstVisibleIndex < LOAD_OLDER_THRESHOLD;
}

function closedCount(page: BarPage): number {
  return page.partial ? page.bars.length - 1 : page.bars.length;
}

/** Index of the first bar whose time is not before the given time. */
function lowerBound(bars: readonly Bar[], time: number): number {
  let low = 0;
  let high = bars.length;
  while (low < high) {
    const middle = (low + high) >> 1;
    if (bars[middle]!.time < time) {
      low = middle + 1;
    } else {
      high = middle;
    }
  }

  return low;
}
