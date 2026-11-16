import type { Bar } from "./bars";
import type { SeriesKey } from "./candleClient";
import { formatTimeUtc } from "../format";

/** A time span without candles that the source confirmed (MD4): from inclusive, to exclusive, Unix seconds (UTC). */
export interface Gap {
  readonly from: number;
  readonly to: number;
  readonly reason: string;
}

/** Where the icon of a gap sits: between the last candle before the gap and the first one after it. */
export interface GapMarker {
  readonly gap: Gap;
  /** Open time of the last candle before the gap. */
  readonly beforeTime: number;
  /** Open time of the first candle after the gap, or undefined when the gap reaches to the newest candle. */
  readonly afterTime: number | undefined;
}

export function gapsAddress(key: SeriesKey): string {
  return "api/gaps/" + [key.source, key.symbol, key.timeframe].map(encodeURIComponent).join("/");
}

/**
 * Reads the gaps of a series. The markers are an addition to the chart: when they cannot be read, the
 * chart shows its candles without them.
 */
export async function fetchGaps(key: SeriesKey, fetcher: typeof fetch = fetch): Promise<Gap[]> {
  try {
    const response = await fetcher(gapsAddress(key));
    return response.ok ? ((await response.json()) as Gap[]) : [];
  } catch {
    return [];
  }
}

/**
 * Places the gaps on the candles in memory. A gap older than the oldest candle here has no place yet; it
 * gets one when the user scrolls back and older candles are loaded.
 */
export function placeGaps(bars: readonly Bar[], gaps: readonly Gap[]): GapMarker[] {
  const markers: GapMarker[] = [];
  for (const gap of gaps) {
    const before = lastIndexBefore(bars, gap.from);
    if (before < 0) {
      continue;
    }

    const after = bars[before + 1];
    markers.push({ gap, beforeTime: bars[before]!.time, afterTime: after?.time });
  }

  return markers;
}

/** What pointing at the icon says: from when to when candles are missing, and why. */
export function gapText(gap: Gap): string {
  const sameDay = formatTimeUtc(gap.from, false) === formatTimeUtc(gap.to, false);
  const to = sameDay ? formatTimeUtc(gap.to, true).slice(11) : formatTimeUtc(gap.to, true);
  const reason =
    gap.reason === "missingUpstream" ? "The source has no candles for this time." : `Reason given by the Downloader: ${gap.reason}.`;

  return `No candles from ${formatTimeUtc(gap.from, true)} to ${to} UTC. ${reason}`;
}

/** Index of the last bar that opens before the time, or -1. */
function lastIndexBefore(bars: readonly Bar[], time: number): number {
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

  return low - 1;
}
