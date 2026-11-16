/** The price format of an asset: prices are shown with the precision of its tick size (CH1). */
export interface PriceFormat {
  readonly precision: number;
  readonly minMove: number;
}

export function priceFormatOf(tickSize: number): PriceFormat {
  if (!(tickSize > 0) || !Number.isFinite(tickSize)) {
    return { precision: 2, minMove: 0.01 };
  }

  // 0.01 -> 2, 0.0001 -> 4, 0.5 -> 1, 1 -> 0, 1e-8 -> 8; never more than 10 digits.
  let precision = 0;
  let scaled = tickSize;
  while (precision < 10 && Math.abs(scaled - Math.round(scaled)) > 1e-9) {
    scaled *= 10;
    precision++;
  }

  return { precision, minMove: tickSize };
}

export function formatPrice(value: number, format: PriceFormat): string {
  return value.toFixed(format.precision);
}

/** A volume in a short form: 950, 12.3K, 4.56M, 1.2B. */
export function formatVolume(value: number): string {
  const units: [number, string][] = [
    [1e9, "B"],
    [1e6, "M"],
    [1e3, "K"],
  ];

  for (const [size, unit] of units) {
    if (Math.abs(value) >= size) {
      return trimZeros((value / size).toFixed(2)) + unit;
    }
  }

  return trimZeros(value.toFixed(2));
}

/** All times are UTC (MD7): "2026-11-15 19:15", or only the date for candles of a day or more. */
export function formatTimeUtc(unixSeconds: number, withTime: boolean): string {
  const iso = new Date(unixSeconds * 1000).toISOString();
  return withTime ? `${iso.slice(0, 10)} ${iso.slice(11, 16)}` : iso.slice(0, 10);
}

/** True for the timeframes whose candles open during a day (15m, 1h, 4h), false for 1d and 1w. */
export function isIntraday(timeframe: string): boolean {
  return /^[0-9]+[mh]$/.test(timeframe);
}

function trimZeros(text: string): string {
  return text.includes(".") ? text.replace(/0+$/, "").replace(/\.$/, "") : text;
}
