/** The answer of GET /api/candles: column arrays, oldest first, times in Unix seconds (UTC). */
export interface CandleColumns {
  readonly t: readonly number[];
  readonly o: readonly number[];
  readonly h: readonly number[];
  readonly l: readonly number[];
  readonly c: readonly number[];
  readonly v: readonly number[];
  /** True: the last element is the current candle, still in progress (MD8). */
  readonly partial: boolean;
}

/** One candle as the chart draws it. `time` is the open time in Unix seconds (UTC). */
export interface Bar {
  readonly time: number;
  readonly open: number;
  readonly high: number;
  readonly low: number;
  readonly close: number;
  readonly volume: number;
}

/** A page of candles, oldest first. */
export interface BarPage {
  readonly bars: readonly Bar[];
  /** True: the last bar is the current candle. */
  readonly partial: boolean;
}

export function toBarPage(columns: CandleColumns): BarPage {
  const bars: Bar[] = [];
  for (let index = 0; index < columns.t.length; index++) {
    bars.push({
      time: columns.t[index]!,
      open: columns.o[index]!,
      high: columns.h[index]!,
      low: columns.l[index]!,
      close: columns.c[index]!,
      volume: columns.v[index]!,
    });
  }

  return { bars, partial: columns.partial && bars.length > 0 };
}

export function sameBar(a: Bar, b: Bar): boolean {
  return (
    a.time === b.time &&
    a.open === b.open &&
    a.high === b.high &&
    a.low === b.low &&
    a.close === b.close &&
    a.volume === b.volume
  );
}
