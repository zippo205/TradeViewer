import {
  HistogramSeries,
  LineSeries,
  PriceScaleMode,
  type HistogramData,
  type IChartApi,
  type ISeriesApi,
  type LineData,
  type LineWidth,
  type UTCTimestamp,
  type WhitespaceData,
} from "lightweight-charts";
import type { SeriesKey } from "../data/candleClient";
import { formatPrice, type PriceFormat } from "../format";
import { BandFill } from "./bandFill";
import {
  HISTOGRAM_ABOVE,
  HISTOGRAM_BELOW,
  histogramColour,
  linesOf,
  onPrice,
  styleOf,
  valuesKey,
  withOpacity,
  type IndicatorInstance,
} from "./indicator";
import { fetchIndicator, type IndicatorPage } from "./indicatorClient";
import { IndicatorValues } from "./indicatorData";
import { IndicatorLegend, type IndicatorAction, type LegendValue } from "./legend";

/** The page sizes of the candles (data/seriesData), so an indicator page has the times of a candle page. */
export interface PageSizes {
  readonly first: number;
  readonly older: number;
  /** A refresh asks for fewer values: only the newest change; a gap is filled by reading again. */
  readonly refresh: number;
}

/** What the set needs to know of the chart it belongs to. */
export interface IndicatorContext {
  readonly key: () => SeriesKey | undefined;
  /** The open time of the oldest candle in memory: the values must reach back to it. */
  readonly oldestBarTime: () => number | undefined;
  readonly priceFormat: () => PriceFormat;
  readonly fetcher: typeof fetch;
  /** Called for every request, for the figures of the performance targets. */
  readonly counted: () => void;
}

/** The fill of Bollinger Bands (TradingView: #2196F3 at 95 % transparency) and of the RSI zone (its line colour at 90 %). */
const BB_FILL = "rgba(33, 150, 243, 0.05)";
const RSI_FILL_OPACITY = 0.1;

/** A new oscillator pane gets this share of the height of the price pane (proposal 5). */
const OSCILLATOR_SHARE = 0.25;

/** The order of the values in a legend line, as TradingView shows them (MACD: histogram first). */
const LEGEND_ORDER: Record<string, readonly string[]> = { macd: ["histogram", "macd", "signal"] };

interface Entry {
  instance: IndicatorInstance;
  key: string;
  values: IndicatorValues;
  series: Map<string, ISeriesApi<"Line"> | ISeriesApi<"Histogram">>;
  fill: BandFill | undefined;
  legend: IndicatorLegend;
  /** Raised when what is fetched changes: an answer for an older generation is dropped. */
  generation: number;
}

/**
 * The indicators of one chart (IN1): one series per line, the price indicators in the price pane, every RSI, MACD
 * and ATR in a pane of its own below, in the order they were added; band fills; a legend line per indicator.
 * The values are fetched by the set itself, page by page with the candles, and never pass through Blazor.
 */
export class IndicatorSet {
  private entries: Entry[] = [];
  private readonly priceLegends: HTMLDivElement;
  private readonly paneObserver: ResizeObserver | undefined;
  private observedPanes = new Set<HTMLElement>();
  private crosshairTime: number | undefined;
  /** True while the candles of a new series are on their way: values that arrive are kept, drawn on release. */
  private holding = false;
  private onAction: (id: string, action: IndicatorAction) => void = () => {};

  constructor(
    private readonly chart: IChartApi,
    private readonly container: HTMLElement,
    private readonly context: IndicatorContext,
    private readonly pages: PageSizes,
  ) {
    this.priceLegends = document.createElement("div");
    this.priceLegends.className = "tv-indicator-legends";
    container.append(this.priceLegends);

    // The library has no event for pane sizes; every layout change resizes the pane elements (step 1 of the plan).
    this.paneObserver = typeof ResizeObserver === "function" ? new ResizeObserver(() => this.placeLegends()) : undefined;
  }

  get count(): number {
    return this.entries.length;
  }

  /** The instances shown, in their order. */
  instances(): readonly IndicatorInstance[] {
    return this.entries.map((entry) => entry.instance);
  }

  /** The values held for an indicator, for checks in the browser. */
  valuesOf(id: string): IndicatorValues | undefined {
    return this.entries.find((entry) => entry.instance.id === id)?.values;
  }

  onActions(listener: (id: string, action: IndicatorAction) => void): void {
    this.onAction = listener;
  }

  /**
   * Shows these indicators, replacing what is shown. Unchanged ones keep their values; a changed parameter fetches
   * again, a changed style or "hidden" only redraws.
   */
  set(instances: readonly IndicatorInstance[]): void {
    const wanted = new Map(instances.map((instance) => [instance.id, instance]));

    for (const entry of this.entries.filter((candidate) => !wanted.has(candidate.instance.id))) {
      this.removeEntry(entry);
    }

    const kept = new Map(this.entries.filter((entry) => wanted.has(entry.instance.id)).map((entry) => [entry.instance.id, entry]));
    const next: Entry[] = [];
    for (const instance of instances) {
      let entry = kept.get(instance.id);
      if (entry !== undefined && entry.instance.type !== instance.type) {
        this.removeEntry(entry); // another kind under the same id: its series do not fit
        entry = undefined;
      }

      if (entry === undefined) {
        // The fetch is answered after this method has put the entry into the set.
        entry = this.addEntry(instance);
        next.push(entry);
        void this.cover(entry);
        continue;
      }

      const key = valuesKey(instance);
      const refetch = key !== entry.key;
      entry.instance = instance;
      entry.key = key;
      entry.legend.update(instance);
      this.applyStyles(entry);
      next.push(entry);
      if (refetch) {
        entry.generation++;
        entry.values = new IndicatorValues(linesOf(instance.type));
        this.draw(entry);
        void this.cover(entry);
      }
    }

    this.entries = next;
    this.arrangeLegends();
    this.showValues(this.crosshairTime);
  }

  /**
   * Another asset or timeframe: the values held belong to the old one. Fetches the newest page of each, in parallel
   * with the candles; what arrives is drawn only on release(), so the values of the new series never
   * stand beside the candles of the old one.
   */
  load(): void {
    this.holding = true;
    for (const entry of this.entries) {
      entry.generation++;
      entry.values = new IndicatorValues(linesOf(entry.instance.type));
      void this.fetchLatest(entry, this.pages.first);
    }
  }

  /** The candles of the new series are drawn: the values held so far too, and from now on as they arrive. */
  release(): void {
    this.holding = false;
    for (const entry of this.entries) {
      this.draw(entry);
    }
  }

  /** New candles may exist (MD2): the newest values again; a gap since the last refresh reads all again. */
  async refresh(): Promise<void> {
    await Promise.all(this.entries.map((entry) => this.fetchLatest(entry, this.pages.refresh)));
  }

  /** The chart loaded the candles before this time (CH4): the values of the same page. */
  async loadOlder(to: number): Promise<void> {
    await Promise.all(
      this.entries.map(async (entry) => {
        const key = this.context.key();
        const generation = entry.generation;
        if (key === undefined) {
          return;
        }

        try {
          this.context.counted();
          const page = await fetchIndicator(key, entry.instance, to, this.pages.older, this.context.fetcher);
          if (generation === entry.generation && this.entries.includes(entry) && entry.values.addOlder(page) > 0) {
            this.draw(entry);
          }
        } catch {
          // The candles are there without the values; the next refresh or a reload tries again.
        }
      }),
    );
  }

  /** The values at the crosshair (undefined: the latest) in every legend line. */
  showValues(time: number | undefined): void {
    this.crosshairTime = time;
    const format = this.context.priceFormat();
    for (const entry of this.entries) {
      const at = time === undefined ? undefined : entry.values.at(time);
      const latest = entry.values.newestTime;
      const values = at ?? (latest === undefined ? undefined : entry.values.at(latest));
      entry.legend.show(entry.instance.hidden || values === undefined ? [] : this.legendValues(entry, values, time ?? latest!, format));
    }
  }

  /** The price format of a new asset; the values are drawn with it. */
  applyPriceFormat(): void {
    for (const entry of this.entries) {
      this.applyStyles(entry);
    }
  }

  /**
   * The logarithmic price scale is for the price pane (CH1): an oscillator pane stays linear, as in TradingView, also
   * when the chart's price scale mode changes. MACD goes below zero, which a logarithmic scale cannot show.
   */
  scalesChanged(): void {
    for (const entry of this.entries) {
      this.keepLinear(entry.series.values());
    }
  }

  dispose(): void {
    this.paneObserver?.disconnect();
    for (const entry of [...this.entries]) {
      this.removeEntry(entry);
    }

    this.priceLegends.remove();
  }

  private addEntry(instance: IndicatorInstance): Entry {
    const own = !onPrice(instance.type);
    const pane = own ? this.chart.panes().length : 0;
    const series = new Map<string, ISeriesApi<"Line"> | ISeriesApi<"Histogram">>();

    // The histogram first, so the lines are drawn over it.
    const lines = instance.type === "macd" ? ["histogram", "macd", "signal"] : linesOf(instance.type);
    for (const line of lines) {
      const options = { priceLineVisible: false, lastValueVisible: true, crosshairMarkerVisible: false };
      series.set(
        line,
        line === "histogram" ? this.chart.addSeries(HistogramSeries, options, pane) : this.chart.addSeries(LineSeries, options, pane),
      );
    }

    if (own) {
      const panes = this.chart.panes();
      panes[pane]?.setStretchFactor((panes[0]?.getStretchFactor() ?? 1) * OSCILLATOR_SHARE);
    }

    this.keepLinear(series.values());

    const entry: Entry = {
      instance,
      key: valuesKey(instance),
      values: new IndicatorValues(linesOf(instance.type)),
      series,
      fill: undefined,
      legend: new IndicatorLegend(instance, (id, action) => this.onAction(id, action)),
      generation: 0,
    };

    if (instance.type === "bb") {
      entry.fill = new BandFill({ times: () => entry.values.t, upper: () => entry.values.line("upper"), lower: () => entry.values.line("lower") }, BB_FILL);
      series.get("upper")!.attachPrimitive(entry.fill);
    } else if (instance.type === "rsi") {
      const rsi = series.get("value")!;
      entry.fill = new BandFill({ times: () => entry.values.t, upper: () => 70, lower: () => 30 }, BB_FILL);
      rsi.attachPrimitive(entry.fill);
      for (const [price, style] of [
        [70, 2],
        [50, 3],
        [30, 2],
      ] as const) {
        rsi.createPriceLine({ price, color: "#787B86", lineWidth: 1, lineStyle: style, axisLabelVisible: false, title: "" });
      }
    }

    this.applyStyles(entry);
    this.draw(entry);
    return entry;
  }

  private keepLinear(series: Iterable<ISeriesApi<"Line"> | ISeriesApi<"Histogram">>): void {
    for (const one of series) {
      if (one.getPane().paneIndex() > 0) {
        one.priceScale().applyOptions({ mode: PriceScaleMode.Normal });
      }
    }
  }

  private removeEntry(entry: Entry): void {
    entry.generation++;
    for (const series of entry.series.values()) {
      this.chart.removeSeries(series); // the last series of a pane takes the pane with it
    }

    entry.legend.element.remove();
    this.entries = this.entries.filter((candidate) => candidate !== entry);
    this.arrangeLegends();
  }

  private applyStyles(entry: Entry): void {
    const instance = entry.instance;
    const format = this.context.priceFormat();
    const priceFormat = instance.type === "rsi" ? { type: "price" as const, precision: 2, minMove: 0.01 } : { type: "price" as const, ...format };

    for (const [line, series] of entry.series) {
      const style = styleOf(instance, line);
      if (line === "histogram") {
        series.applyOptions({ visible: !instance.hidden, priceFormat });
      } else {
        (series as ISeriesApi<"Line">).applyOptions({
          color: style.color,
          lineWidth: Math.min(Math.max(style.width, 1), 4) as LineWidth,
          visible: !instance.hidden,
          priceFormat,
        });
      }
    }

    if (instance.type === "rsi") {
      entry.fill?.set(withOpacity(styleOf(instance, "value").color, RSI_FILL_OPACITY), !instance.hidden);
    } else {
      entry.fill?.set(BB_FILL, !instance.hidden);
    }

    if (instance.type === "macd") {
      this.draw(entry); // the colours of the columns are part of the data
    }
  }

  /** Gives the library the values held. */
  private draw(entry: Entry): void {
    if (this.holding) {
      return;
    }

    const times = entry.values.t;
    for (const [line, series] of entry.series) {
      const values = entry.values.line(line);
      if (line === "histogram") {
        const above = styleOf(entry.instance, HISTOGRAM_ABOVE).color;
        const below = styleOf(entry.instance, HISTOGRAM_BELOW).color;
        const data: (HistogramData<UTCTimestamp> | WhitespaceData<UTCTimestamp>)[] = times.map((time, index) => {
          const value = values[index];
          return value === null || value === undefined
            ? { time: time as UTCTimestamp }
            : { time: time as UTCTimestamp, value, color: histogramColour(value, values[index - 1], above, below) };
        });
        (series as ISeriesApi<"Histogram">).setData(data);
      } else {
        const data: (LineData<UTCTimestamp> | WhitespaceData<UTCTimestamp>)[] = times.map((time, index) => {
          const value = values[index];
          return value === null || value === undefined ? { time: time as UTCTimestamp } : { time: time as UTCTimestamp, value };
        });
        (series as ISeriesApi<"Line">).setData(data);
      }
    }

    if (this.crosshairTime === undefined) {
      this.showValues(undefined);
    }
  }

  /** The newest page; when it does not join the values held, all values back to the oldest candle again. */
  private async fetchLatest(entry: Entry, limit: number): Promise<void> {
    const key = this.context.key();
    const generation = entry.generation;
    if (key === undefined) {
      return;
    }

    let page: IndicatorPage;
    try {
      this.context.counted();
      page = await fetchIndicator(key, entry.instance, undefined, limit, this.context.fetcher);
    } catch {
      return; // what is shown stays; the next refresh tries again
    }

    if (generation !== entry.generation || !this.entries.includes(entry)) {
      return;
    }

    const joined = entry.values.setLatest(page);
    this.draw(entry);
    if (!joined) {
      await this.cover(entry);
    }
  }

  /** Fetches until the values reach back to the oldest candle in memory: a new indicator on a chart scrolled back. */
  private async cover(entry: Entry): Promise<void> {
    const key = this.context.key();
    const generation = entry.generation;
    if (key === undefined) {
      return;
    }

    try {
      if (entry.values.newestTime === undefined) {
        this.context.counted();
        const page = await fetchIndicator(key, entry.instance, undefined, this.pages.first, this.context.fetcher);
        if (generation !== entry.generation || !this.entries.includes(entry)) {
          return;
        }

        entry.values.setLatest(page);
        this.draw(entry);
        if (page.t.length < this.pages.first) {
          return; // the whole history
        }
      }

      for (;;) {
        const oldestBar = this.context.oldestBarTime();
        const oldest = entry.values.oldestTime;
        if (oldestBar === undefined || oldest === undefined || oldest <= oldestBar) {
          return;
        }

        this.context.counted();
        const page = await fetchIndicator(key, entry.instance, oldest, this.pages.older, this.context.fetcher);
        if (generation !== entry.generation || !this.entries.includes(entry)) {
          return;
        }

        const added = entry.values.addOlder(page);
        this.draw(entry);
        if (added === 0 || page.t.length < this.pages.older) {
          return;
        }
      }
    } catch {
      // The next refresh or scrolling back asks again.
    }
  }

  private legendValues(entry: Entry, values: Record<string, number | null>, time: number, format: PriceFormat): LegendValue[] {
    const order = LEGEND_ORDER[entry.instance.type] ?? linesOf(entry.instance.type);
    const text = (value: number) => (entry.instance.type === "rsi" ? value.toFixed(2) : formatPrice(value, format));

    return order.flatMap((line) => {
      const value = values[line];
      if (value === null || value === undefined) {
        return [{ text: "n/a", color: "var(--muted)" }];
      }

      let color = styleOf(entry.instance, line).color;
      if (line === "histogram") {
        const index = entry.values.t.indexOf(time);
        color = histogramColour(
          value,
          entry.values.line("histogram")[index - 1],
          styleOf(entry.instance, HISTOGRAM_ABOVE).color,
          styleOf(entry.instance, HISTOGRAM_BELOW).color,
        );
      }

      return [{ text: text(value), color }];
    });
  }

  /** Price indicators below the legend of the candles; each other one at the top of its own pane. */
  private arrangeLegends(): void {
    const onPriceLegends = this.entries.filter((entry) => onPrice(entry.instance.type)).map((entry) => entry.legend.element);
    this.priceLegends.replaceChildren(...onPriceLegends);
    for (const entry of this.entries.filter((candidate) => !onPrice(candidate.instance.type))) {
      if (entry.legend.element.parentElement !== this.container) {
        this.container.append(entry.legend.element);
      }
    }

    // The panes exist from the next frame on (step 1 of the plan).
    requestAnimationFrame(() => this.placeLegends());
  }

  private placeLegends(): void {
    const panes = this.chart.panes();
    for (const pane of panes) {
      const element = pane.getHTMLElement();
      if (element !== null && !this.observedPanes.has(element)) {
        this.observedPanes.add(element);
        this.paneObserver?.observe(element);
      }
    }

    for (const entry of this.entries) {
      if (onPrice(entry.instance.type)) {
        continue;
      }

      const first = entry.series.values().next().value;
      const index = first?.getPane().paneIndex();
      let top = 0;
      try {
        for (let pane = 0; pane < (index ?? 0); pane++) {
          top += this.chart.paneSize(pane).height + 1; // the separator is one pixel
        }
      } catch {
        requestAnimationFrame(() => this.placeLegends()); // not laid out yet
        return;
      }

      entry.legend.element.style.top = `${top + 6}px`;
    }
  }
}
