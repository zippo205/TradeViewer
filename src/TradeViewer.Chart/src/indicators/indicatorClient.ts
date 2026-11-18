import type { SeriesKey } from "../data/candleClient";
import { parametersOf, type IndicatorInstance } from "./indicator";

/** The answer of GET /api/indicators: the times of the candle page and one value per time for each line. */
export interface IndicatorPage {
  readonly t: readonly number[];
  readonly lines: Readonly<Record<string, readonly (number | null)[]>>;
  /** True: the last value belongs to the current candle (MD8). */
  readonly partial: boolean;
}

export function indicatorAddress(key: SeriesKey, instance: IndicatorInstance, to: number | undefined, limit: number): string {
  const path = [key.source, key.symbol, key.timeframe].map(encodeURIComponent).join("/");
  const query = [...parametersOf(instance), `limit=${limit}`, ...(to === undefined ? [] : [`to=${to}`])].join("&");
  return `api/indicators/${path}?${query}`;
}

/** Reads the values of one indicator for the candle page that to and limit name, as /api/candles does. */
export async function fetchIndicator(
  key: SeriesKey,
  instance: IndicatorInstance,
  to: number | undefined,
  limit: number,
  fetcher: typeof fetch = fetch,
): Promise<IndicatorPage> {
  const response = await fetcher(indicatorAddress(key, instance, to, limit));
  if (!response.ok) {
    throw new Error(`The indicator could not be read (${response.status}).`);
  }

  return (await response.json()) as IndicatorPage;
}
