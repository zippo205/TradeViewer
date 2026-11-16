import {
  CandlestickSeries,
  CrosshairMode,
  HistogramSeries,
  PriceScaleMode,
  createChart,
  type CandlestickData,
  type HistogramData,
  type IChartApi,
  type ISeriesApi,
  type LogicalRange as ChartLogicalRange,
  type MouseEventParams,
  type UTCTimestamp,
} from "lightweight-charts";
import type { Bar } from "./data/bars";
import { CandleRequestError, fetchCandles, type SeriesKey } from "./data/candleClient";
import { fetchGaps, gapText, placeGaps, type Gap } from "./data/gaps";
import { FIRST_PAGE_SIZE, OLDER_PAGE_SIZE, SeriesData, shouldLoadOlder } from "./data/seriesData";
import { GAP_ID_PREFIX, GapMarkers } from "./gapMarkers";
import { formatPrice, formatTimeUtc, formatVolume, isIntraday, priceFormatOf, type PriceFormat } from "./format";
import { captureView, latestRange, restoreView, type ViewState } from "./range";
import { themeOf, type ChartTheme } from "./theme";

/** What Blazor tells the pane about the series to show. */
export interface SeriesRequest extends SeriesKey {
  /** The tick size of the asset: prices are shown with its precision (CH1). */
  readonly tickSize: number;
}

export interface PaneOptions {
  /** Logarithmic price scale instead of the linear one (CH1). */
  readonly logScale?: boolean;
  /** Dark colours; follows the system setting when not given. */
  readonly dark?: boolean;
}

/** Figures for the performance targets, read by measurements (NF1). */
export interface PaneStats {
  /** Milliseconds from the last setSeries call until its candles were painted. */
  readonly lastDrawMs: number | undefined;
  /** Candles in memory. */
  readonly bars: number;
  /** Requests for candles since the pane was created. */
  readonly requests: number;
  /** The visible part in bar indices, or undefined without candles. */
  readonly visibleFrom: number | undefined;
  readonly visibleTo: number | undefined;
  /** Open time of the oldest and the newest candle in memory (Unix seconds). */
  readonly firstTime: number | undefined;
  readonly lastTime: number | undefined;
  /** True: the last candle is the current one, still in progress. */
  readonly partial: boolean;
}

/**
 * One chart: candles and volume of an asset in a timeframe, scales, crosshair legend, paging while
 * scrolling back, last-close line and the jump to the latest candle (CH1 to CH5). It fetches its
 * candles itself; zoom, pan and crosshair need no request (NF2).
 */
export class ChartPane {
  private readonly chart: IChartApi;
  private readonly candles: ISeriesApi<"Candlestick">;
  private readonly volume: ISeriesApi<"Histogram">;
  private readonly legend: HTMLDivElement;
  private readonly message: HTMLDivElement;
  private readonly latestButton: HTMLButtonElement;
  private readonly tooltip: HTMLDivElement;
  private readonly gapMarkers = new GapMarkers();
  private gaps: Gap[] = [];
  private readonly fetcher: typeof fetch;

  private data = new SeriesData();
  private request: SeriesRequest | undefined;
  private priceFormat: PriceFormat = priceFormatOf(0.01);
  private theme: ChartTheme;
  private options: PaneOptions = {};
  private generation = 0;
  private loadingOlder = false;
  private lastDrawMs: number | undefined;
  private requests = 0;
  private disposed = false;

  constructor(
    private readonly container: HTMLElement,
    options: PaneOptions = {},
    fetcher: typeof fetch = (input, init) => fetch(input, init),
    private readonly onDisposed: () => void = () => {},
  ) {
    this.fetcher = fetcher;
    this.options = options;
    this.theme = themeOf(options.dark ?? prefersDark());

    container.classList.add("tv-chart");
    const canvasHost = document.createElement("div");
    canvasHost.className = "tv-chart-canvas";
    container.append(canvasHost);

    this.chart = createChart(canvasHost, {
      autoSize: true,
      layout: {
        background: { color: this.theme.background },
        textColor: this.theme.text,
        // The licence of Lightweight Charts asks for this notice (decision 0001).
        attributionLogo: true,
      },
      grid: { vertLines: { color: this.theme.grid }, horzLines: { color: this.theme.grid } },
      crosshair: { mode: CrosshairMode.Normal },
      rightPriceScale: { borderColor: this.theme.border, mode: scaleMode(options) },
      timeScale: { borderColor: this.theme.border, timeVisible: true, secondsVisible: false },
      // Times are UTC everywhere (MD7): the library shows Unix seconds as UTC, and so does this formatter.
      localization: { timeFormatter: (time: number) => formatTimeUtc(time, isIntraday(this.request?.timeframe ?? "1d")) },
    });

    this.candles = this.chart.addSeries(CandlestickSeries, {
      upColor: this.theme.up,
      downColor: this.theme.down,
      wickUpColor: this.theme.up,
      wickDownColor: this.theme.down,
      borderVisible: false,
      // The line and label of the last close, coloured by the direction of the last candle (CH5).
      priceLineVisible: true,
      lastValueVisible: true,
    });

    // Volume like in TradingView: in the lower part of the candle pane, on its own hidden scale (CH2).
    this.volume = this.chart.addSeries(HistogramSeries, {
      priceScaleId: "",
      priceFormat: { type: "volume" },
      priceLineVisible: false,
      lastValueVisible: false,
    });
    this.volume.priceScale().applyOptions({ scaleMargins: { top: 0.8, bottom: 0 } });
    this.candles.priceScale().applyOptions({ scaleMargins: { top: 0.08, bottom: 0.22 } });

    this.legend = overlay(container, "tv-chart-legend");
    this.message = overlay(container, "tv-chart-message");
    this.tooltip = overlay(container, "tv-chart-tooltip");
    this.tooltip.hidden = true;

    // Gaps the source confirmed show as icons at the bottom of the chart (MD4).
    this.candles.attachPrimitive(this.gapMarkers);
    this.latestButton = document.createElement("button");
    this.latestButton.type = "button";
    this.latestButton.className = "tv-chart-latest";
    this.latestButton.title = "Jump to the latest candle";
    this.latestButton.textContent = "»";
    this.latestButton.hidden = true;
    this.latestButton.addEventListener("click", () => this.scrollToLatest());
    container.append(this.latestButton);

    this.chart.subscribeCrosshairMove((event) => {
      this.showLegend(event);
      this.showGapTooltip(event);
    });
    this.chart.timeScale().subscribeVisibleLogicalRangeChange((range) => this.onRangeChanged(range));
  }

  /**
   * Shows another asset or timeframe. The number of visible candles is kept; the view stays at the
   * latest candles or, scrolled back within the same asset, at the same time (CH3).
   */
  async setSeries(request: SeriesRequest): Promise<void> {
    const started = performance.now();
    const generation = ++this.generation;
    const previous = this.request;
    const view = this.currentView();
    const assetChanged = previous === undefined || previous.source !== request.source || previous.symbol !== request.symbol;

    this.request = request;
    this.loadingOlder = false;
    this.showMessage(this.data.bars.length === 0 ? "Loading..." : undefined);

    let data: SeriesData;
    try {
      data = new SeriesData();
      data.setLatest(await this.fetchPage(request, undefined, FIRST_PAGE_SIZE));
    } catch (error) {
      if (generation === this.generation) {
        this.showError(error);
      }

      return;
    }

    if (generation !== this.generation || this.disposed) {
      return; // the user has moved on to another series meanwhile
    }

    this.data = data;
    this.gaps = [];
    this.tooltip.hidden = true;
    void this.loadGaps(request, generation);
    this.priceFormat = priceFormatOf(request.tickSize);
    this.candles.applyOptions({ priceFormat: { type: "price", ...this.priceFormat } });
    // A new series starts with the price scale fitted to its candles, whatever the user did before (CH1).
    this.candles.priceScale().applyOptions({ autoScale: true });

    if (data.bars.length === 0) {
      this.draw();
      this.showMessage(`No candles for ${request.symbol} in ${request.timeframe}.`);
      return;
    }

    this.draw();
    this.chart.timeScale().setVisibleLogicalRange(restoreView(view, data.bars, assetChanged));
    this.showMessage(undefined);
    this.showLegend(undefined);

    requestAnimationFrame(() => {
      if (generation === this.generation) {
        this.lastDrawMs = performance.now() - started;
      }
    });
  }

  /** New candles may exist (CandlesUpdated): reads the newest page again; zoom and position stay. */
  async refresh(): Promise<void> {
    const request = this.request;
    if (request === undefined) {
      return;
    }

    const generation = this.generation;
    let page;
    try {
      page = await this.fetchPage(request, undefined, FIRST_PAGE_SIZE);
    } catch {
      return; // the chart keeps what it shows; the next event or a reload tries again
    }

    if (generation !== this.generation || this.disposed) {
      return;
    }

    const before = this.data.bars.length;
    const result = this.data.setLatest(page);
    if (result.firstChanged < 0) {
      return;
    }

    if (this.data.bars.length === 0) {
      this.draw();
      this.showMessage(`No candles for ${request.symbol} in ${request.timeframe}.`);
      return;
    }

    // Only the end changed: the library adds and replaces the last candles without moving the view.
    if (!result.reset && before > 0 && result.firstChanged >= before - 1 && this.data.bars.length >= before) {
      for (let index = result.firstChanged; index < this.data.bars.length; index++) {
        const bar = this.data.bars[index]!;
        const partial = this.data.partial && index === this.data.bars.length - 1;
        this.candles.update(this.candleOf(bar, partial));
        this.volume.update(this.volumeOf(bar, partial));
      }

      this.gapMarkers.set(placeGaps(this.data.bars, this.gaps));
    } else {
      const view = this.currentView();
      this.draw();
      if (before === 0 || result.reset) {
        this.chart.timeScale().setVisibleLogicalRange(restoreView(view, this.data.bars, false));
      }
    }

    this.showMessage(undefined);
    this.showLegend(undefined);
  }

  setOptions(options: PaneOptions): void {
    this.options = { ...this.options, ...options };
    this.theme = themeOf(this.options.dark ?? prefersDark());

    this.chart.applyOptions({
      layout: { background: { color: this.theme.background }, textColor: this.theme.text },
      grid: { vertLines: { color: this.theme.grid }, horzLines: { color: this.theme.grid } },
      rightPriceScale: { borderColor: this.theme.border, mode: scaleMode(this.options) },
      timeScale: { borderColor: this.theme.border },
    });
    this.candles.applyOptions({
      upColor: this.theme.up,
      downColor: this.theme.down,
      wickUpColor: this.theme.up,
      wickDownColor: this.theme.down,
    });

    if (this.data.bars.length > 0) {
      this.draw(); // the colours of volume and of the current candle are part of the data
    }
  }

  /** Back to the latest candle, with the same number of candles in view and the usual space to the right (CH5). */
  scrollToLatest(): void {
    const range = this.chart.timeScale().getVisibleLogicalRange();
    if (range === null || this.data.bars.length === 0) {
      return;
    }

    this.chart.timeScale().setVisibleLogicalRange(latestRange(range.to - range.from, this.data.bars.length));
  }

  stats(): PaneStats {
    const range = this.chart.timeScale().getVisibleLogicalRange();
    const bars = this.data.bars;
    return {
      lastDrawMs: this.lastDrawMs,
      bars: bars.length,
      requests: this.requests,
      visibleFrom: range?.from,
      visibleTo: range?.to,
      firstTime: bars[0]?.time,
      lastTime: bars[bars.length - 1]?.time,
      partial: this.data.partial,
    };
  }

  dispose(): void {
    this.disposed = true;
    this.generation++;
    this.onDisposed();
    this.chart.remove();
    this.container.replaceChildren();
    this.container.classList.remove("tv-chart");
  }

  private async fetchPage(request: SeriesKey, to: number | undefined, limit: number) {
    this.requests++;
    return fetchCandles(request, to, limit, this.fetcher);
  }

  private currentView(): ViewState | undefined {
    const range = this.chart.timeScale().getVisibleLogicalRange();
    return range === null ? undefined : captureView(range, this.data.bars);
  }

  /** Gives the library all candles in memory. */
  private draw(): void {
    const bars = this.data.bars;
    const last = bars.length - 1;

    const candles: CandlestickData<UTCTimestamp>[] = [];
    const volumes: HistogramData<UTCTimestamp>[] = [];
    for (let index = 0; index < bars.length; index++) {
      const partial = this.data.partial && index === last;
      candles.push(this.candleOf(bars[index]!, partial));
      volumes.push(this.volumeOf(bars[index]!, partial));
    }

    this.candles.setData(candles);
    this.volume.setData(volumes);
    this.gapMarkers.set(placeGaps(bars, this.gaps));
  }

  private candleOf(bar: Bar, partial: boolean): CandlestickData<UTCTimestamp> {
    const candle: CandlestickData<UTCTimestamp> = {
      time: bar.time as UTCTimestamp,
      open: bar.open,
      high: bar.high,
      low: bar.low,
      close: bar.close,
    };

    if (!partial) {
      return candle;
    }

    // The current candle is still in progress (MD8): drawn paler than the closed ones.
    const colour = bar.close >= bar.open ? this.theme.upPartial : this.theme.downPartial;
    return { ...candle, color: colour, wickColor: colour };
  }

  private volumeOf(bar: Bar, partial: boolean): HistogramData<UTCTimestamp> {
    const rising = bar.close >= bar.open;
    const colour = partial
      ? rising
        ? this.theme.upVolumePartial
        : this.theme.downVolumePartial
      : rising
        ? this.theme.upVolume
        : this.theme.downVolume;

    return { time: bar.time as UTCTimestamp, value: bar.volume, color: colour };
  }

  private onRangeChanged(range: ChartLogicalRange | null): void {
    if (range === null || this.request === undefined) {
      return;
    }

    // The button shows as soon as the latest candle is out of view (CH5).
    this.latestButton.hidden = this.data.bars.length === 0 || range.to >= this.data.bars.length - 1;

    if (shouldLoadOlder(range.from, this.data.hasMore, this.loadingOlder)) {
      void this.loadOlder();
    }
  }

  /** Loads the page before the oldest candle while the user scrolls back (CH4). */
  private async loadOlder(): Promise<void> {
    const request = this.request;
    const oldest = this.data.oldestTime;
    if (request === undefined || oldest === undefined) {
      return;
    }

    const generation = this.generation;
    this.loadingOlder = true;
    try {
      const page = await this.fetchPage(request, oldest, OLDER_PAGE_SIZE);
      if (generation !== this.generation || this.disposed) {
        return;
      }

      // The view is kept by its distance to the latest candle, so candles added in front do not move it.
      const range = this.chart.timeScale().getVisibleLogicalRange();
      const added = this.data.addOlder(page);
      if (added > 0) {
        this.draw();
        if (range !== null) {
          this.chart.timeScale().setVisibleLogicalRange({ from: range.from + added, to: range.to + added });
        }
      }
    } catch {
      // Scrolling on asks again; what is shown stays.
    } finally {
      if (generation === this.generation) {
        this.loadingOlder = false;

        // The user may have reached the edge while the page was on its way.
        const range = this.chart.timeScale().getVisibleLogicalRange();
        if (range !== null && shouldLoadOlder(range.from, this.data.hasMore, false)) {
          void this.loadOlder();
        }
      }
    }
  }

  /** The values of the candle below the crosshair, or of the latest candle without one (CH1). */
  private showLegend(event: MouseEventParams | undefined): void {
    const bars = this.data.bars;
    if (bars.length === 0 || this.request === undefined) {
      this.legend.textContent = "";
      return;
    }

    let bar = bars[bars.length - 1]!;
    if (event?.time !== undefined) {
      const candle = event.seriesData.get(this.candles) as CandlestickData<UTCTimestamp> | undefined;
      const volume = event.seriesData.get(this.volume) as HistogramData<UTCTimestamp> | undefined;
      if (candle !== undefined) {
        bar = {
          time: candle.time,
          open: candle.open,
          high: candle.high,
          low: candle.low,
          close: candle.close,
          volume: volume?.value ?? 0,
        };
      }
    }

    const partial = this.data.partial && bar.time === bars[bars.length - 1]!.time;
    const change = bar.open === 0 ? 0 : ((bar.close - bar.open) / bar.open) * 100;
    const direction = bar.close >= bar.open ? "up" : "down";
    const price = (value: number) => formatPrice(value, this.priceFormat);

    this.legend.replaceChildren(
      span("tv-legend-time", formatTimeUtc(bar.time, isIntraday(this.request.timeframe)) + " UTC"),
      span(`tv-legend-${direction}`, `O ${price(bar.open)}  H ${price(bar.high)}  L ${price(bar.low)}  C ${price(bar.close)}`),
      span(`tv-legend-${direction}`, `${change >= 0 ? "+" : ""}${change.toFixed(2)}%`),
      span("tv-legend-volume", `Vol ${formatVolume(bar.volume)}`),
      ...(partial ? [span("tv-legend-partial", "in progress")] : []),
    );
  }

  /** Reads the gaps of the series after its candles; the chart does not wait for them. */
  private async loadGaps(request: SeriesRequest, generation: number): Promise<void> {
    const gaps = await fetchGaps(request, this.fetcher);
    if (generation !== this.generation || this.disposed) {
      return;
    }

    this.gaps = gaps;
    this.gapMarkers.set(placeGaps(this.data.bars, gaps));
  }

  /** Pointing at a gap icon shows from when to when candles are missing, and why (MD4). */
  private showGapTooltip(event: MouseEventParams): void {
    const id = typeof event.hoveredObjectId === "string" ? event.hoveredObjectId : "";
    const marker = id.startsWith(GAP_ID_PREFIX) ? this.gapMarkers.get(Number(id.slice(GAP_ID_PREFIX.length))) : undefined;
    if (marker === undefined || event.point === undefined) {
      this.tooltip.hidden = true;
      return;
    }

    this.tooltip.textContent = gapText(marker.gap);
    this.tooltip.hidden = false;

    // Above the icon, kept inside the chart.
    const width = this.tooltip.offsetWidth;
    const left = Math.min(Math.max(event.point.x - width / 2, 8), Math.max(this.container.clientWidth - width - 8, 8));
    this.tooltip.style.left = `${left}px`;
    this.tooltip.style.top = `${Math.max(event.point.y - this.tooltip.offsetHeight - 18, 8)}px`;
  }

  private showError(error: unknown): void {
    const text = error instanceof CandleRequestError ? error.message : "The candles could not be shown.";
    this.data = new SeriesData();
    this.gaps = [];
    this.tooltip.hidden = true;
    this.draw();
    this.legend.textContent = "";
    this.latestButton.hidden = true;
    this.showMessage(text);
  }

  private showMessage(text: string | undefined): void {
    this.message.textContent = text ?? "";
    this.message.hidden = text === undefined;
  }
}

function scaleMode(options: PaneOptions): PriceScaleMode {
  return options.logScale === true ? PriceScaleMode.Logarithmic : PriceScaleMode.Normal;
}

function prefersDark(): boolean {
  return typeof matchMedia === "function" && matchMedia("(prefers-color-scheme: dark)").matches;
}

function overlay(container: HTMLElement, className: string): HTMLDivElement {
  const element = document.createElement("div");
  element.className = className;
  container.append(element);
  return element;
}

function span(className: string, text: string): HTMLSpanElement {
  const element = document.createElement("span");
  element.className = className;
  element.textContent = text;
  return element;
}
