import { toBarPage, type BarPage, type CandleColumns } from "./bars";

/** A series the chart shows: an asset (source and symbol) in a timeframe (code such as 15m). */
export interface SeriesKey {
  readonly source: string;
  readonly symbol: string;
  readonly timeframe: string;
}

/** A request for candles that failed, with the message to show in place of the chart. */
export class CandleRequestError extends Error {
  constructor(
    message: string,
    /** The HTTP status, or 0 when the server was not reachable. */
    readonly status: number,
  ) {
    super(message);
    this.name = "CandleRequestError";
  }
}

export function candlesAddress(key: SeriesKey, to: number | undefined, limit: number): string {
  const path = [key.source, key.symbol, key.timeframe].map(encodeURIComponent).join("/");
  const query = to === undefined ? `limit=${limit}` : `limit=${limit}&to=${to}`;
  return `api/candles/${path}?${query}`;
}

/**
 * Reads a page of candles from the Api of TradeViewer. Candles never pass through Blazor: the module
 * fetches the column arrays itself (architecture, chapter 10).
 */
export async function fetchCandles(
  key: SeriesKey,
  to: number | undefined,
  limit: number,
  fetcher: typeof fetch = fetch,
  signal?: AbortSignal,
): Promise<BarPage> {
  let response: Response;
  try {
    response = await fetcher(candlesAddress(key, to, limit), { signal });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }

    throw new CandleRequestError("The server is not reachable.", 0);
  }

  if (!response.ok) {
    throw new CandleRequestError(await problemDetail(response), response.status);
  }

  return toBarPage((await response.json()) as CandleColumns);
}

/** The detail of the problem details (RFC 9457) the Api answers with. */
async function problemDetail(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { detail?: unknown };
    if (typeof problem.detail === "string" && problem.detail.length > 0) {
      return problem.detail;
    }
  } catch {
    // no problem details: the status has to do
  }

  return `The candles could not be read (${response.status}).`;
}
