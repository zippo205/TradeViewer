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
  type Logical,
  type LogicalRange as ChartLogicalRange,
  type MouseEventParams,
  type UTCTimestamp,
} from "lightweight-charts";
import type { Bar } from "./data/bars";
import { CandleRequestError, fetchCandles, type SeriesKey } from "./data/candleClient";
import { fetchGaps, gapText, placeGaps, type Gap } from "./data/gaps";
import { FIRST_PAGE_SIZE, OLDER_PAGE_SIZE, REFRESH_PAGE_SIZE, SeriesData, shouldLoadOlder, type SeriesWindow } from "./data/seriesData";
import { GAP_ID_PREFIX, GapMarkers } from "./gapMarkers";
import { parseGroups, type OverlayGroupRecord } from "./primitives/overlays/overlay";
import { OverlayLayer } from "./primitives/overlays/overlayLayer";
import { ScenarioHandles } from "./primitives/scenarioHandles";
import { RangeBand } from "./primitives/rangeBand";
import { RangeSelection, type RangeEvents } from "./tools/rangeSelection";
import { OverlaySelection, type OverlaySelectionEvents } from "./tools/overlaySelection";
import { ScenarioPointInteraction, type ScenarioHandle, type ScenarioPointEvents, type ScenarioTool } from "./tools/scenarioPoints";
import type { IndicatorInstance } from "./indicators/indicator";
import { IndicatorSet } from "./indicators/indicatorSet";
import type { IndicatorAction } from "./indicators/legend";
import type { Anchor, Drawing, DrawingStyle, ToolCode } from "./primitives/drawing";
import { DrawingLayer } from "./primitives/drawingLayer";
import type { Point } from "./tools/geometry";
import { DrawingInteraction, type DrawingEvents, type DrawingOptions } from "./tools/interaction";
import { PointerInput } from "./tools/pointerInput";
import { extremeOf, finestOf, madeAt, type Extreme } from "./tools/exactTime";
import { TimeMapping, timeframeSeconds } from "./tools/timeMapping";
import { formatPrice, formatTimeUtc, formatVolume, isIntraday, priceFormatOf, type PriceFormat } from "./format";
import { captureView, latestRange, restoreView, type ViewState } from "./range";
import { themeOf, type ChartTheme } from "./theme";

/** What Blazor tells the pane about the series to show. */
export interface SeriesRequest extends SeriesKey {
  /** The tick size of the asset: prices are shown with its precision (CH1). */
  readonly tickSize: number;
  /** The timeframes of the asset with candles; the smallest gives anchors their exact times (step 9). */
  readonly timeframes?: readonly string[];
  /** Only the candles of this time window (the span of a scenario, SC2); without it the whole history. */
  readonly window?: SeriesWindow | null;
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
  /** Milliseconds from the last setSeries call until the values of all its indicators were painted (NF1). */
  readonly lastIndicatorsMs: number | undefined;
  /** Candles in memory. */
  readonly bars: number;
  /** Requests for candles since the pane was created. */
  readonly requests: number;
  /** Indicators on the chart and the requests for their values since the pane was created (IN1). */
  readonly indicators: number;
  readonly indicatorRequests: number;
  /** The visible part in bar indices, or undefined without candles. */
  readonly visibleFrom: number | undefined;
  readonly visibleTo: number | undefined;
  /** Open time of the oldest and the newest candle in memory (Unix seconds). */
  readonly firstTime: number | undefined;
  readonly lastTime: number | undefined;
  /** True: the last candle is the current one, still in progress. */
  readonly partial: boolean;
  /** Drawings on the chart, those created and changed by the user, the selected one and the tool in use. */
  readonly drawings: number;
  readonly drawingsCreated: number;
  readonly drawingChanges: number;
  readonly selectedDrawing: string | undefined;
  readonly tool: ToolCode | undefined;
  /** Overlay shapes drawn at the last paint (hidden groups and shapes off the candles not counted). */
  readonly overlayShapes: number;
  /** Handles of named points, the point tool in use, the field it places next, the selected point, points placed or let go. */
  readonly scenarioHandles: number;
  readonly scenarioTool: string | undefined;
  readonly scenarioNext: string | undefined;
  readonly selectedPoint: string | undefined;
  readonly pointsPlaced: number;
  readonly rangeTool: boolean;
  readonly selectedOverlay: { readonly group: string; readonly key?: string } | undefined;
  readonly rangeBand: { from: number; to: number } | undefined;
}

/**
 * One chart: candles and volume of an asset in a timeframe, scales, crosshair legend, paging while
 * scrolling back, last-close line and the jump to the latest candle (CH1 to CH5). It fetches its
 * candles itself; zoom, pan and crosshair need no request (NF2).
 */
/** The smallest bar spacing of Lightweight Charts by default, in pixels. */
const MIN_BAR_SPACING = 0.5;

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
  private readonly overlayLayer = new OverlayLayer(
    () => this.timeMapping(),
    () => this.theme.background,
  );
  private readonly drawingLayer = new DrawingLayer(
    () => this.timeMapping(),
    () => this.data.bars,
  );
  private readonly drawingInteraction: DrawingInteraction;
  private readonly scenarioHandles: ScenarioHandles;
  private readonly scenarioPoints: ScenarioPointInteraction;
  private scenarioListener: Partial<ScenarioPointEvents> = {};
  private pointsPlaced = 0;
  private readonly rangeBand: RangeBand;
  private readonly rangeSelection: RangeSelection;
  private rangeListener: Partial<RangeEvents> = {};
  private readonly overlaySelection: OverlaySelection;
  private overlayListener: Partial<OverlaySelectionEvents> = {};
  private seriesLoaded: Promise<void> = Promise.resolve();
  private readonly pointerInput: PointerInput;
  private drawingListener: Partial<DrawingEvents> = {};
  private drawingsCreated = 0;
  private mappingCache: { bars: readonly Bar[]; timeframe: string; mapping: TimeMapping | undefined } | undefined;
  private drawingChanges = 0;
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
  private indicatorRequests = 0;
  private readonly indicatorSet: IndicatorSet;
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

    // The indicators (IN1); their values are fetched with the candles, page by page.
    this.indicatorSet = new IndicatorSet(
      this.chart,
      container,
      {
        key: () => this.request,
        oldestBarTime: () => this.data.oldestTime,
        priceFormat: () => this.priceFormat,
        fetcher: this.fetcher,
        counted: () => this.indicatorRequests++,
      },
      { first: FIRST_PAGE_SIZE, older: OLDER_PAGE_SIZE, refresh: REFRESH_PAGE_SIZE },
    );

    this.legend = overlay(container, "tv-chart-legend");
    this.message = overlay(container, "tv-chart-message");
    this.tooltip = overlay(container, "tv-chart-tooltip");
    this.tooltip.hidden = true;

    // Gaps the source confirmed show as icons at the bottom of the chart (MD4).
    this.candles.attachPrimitive(this.gapMarkers);

    // Overlays and scenarios (OV1-OV6, SC8): read-only shapes below the user's drawings.
    this.candles.attachPrimitive(this.overlayLayer);

    // The drawings of the asset (DR3), on every timeframe; the pointer is taken before the chart gets it.
    this.candles.attachPrimitive(this.drawingLayer);
    this.drawingInteraction = new DrawingInteraction(
      this.drawingLayer,
      {
        drawingCreated: (drawing) => {
          this.drawingsCreated++;
          this.drawingListener.drawingCreated?.(drawing);
        },
        drawingChanged: (drawing) => {
          this.drawingChanges++;
          this.drawingListener.drawingChanged?.(drawing);
        },
        selectionChanged: (id) => {
          if (id !== undefined) {
            this.scenarioPoints.select(undefined); // one thing is selected at a time: a drawing or a point
          }

          this.drawingListener.selectionChanged?.(id);
        },
        toolChanged: (tool) => this.drawingListener.toolChanged?.(tool),
      },
      undefined,
      (drawing, changed) => this.exactTimes(drawing, changed),
    );
    this.drawingLayer.bind(this.drawingInteraction);

    // The named points of an expert scenario (SC10): handles on top, taken before the drawings.
    const pointSurface = {
      pointOf: (anchor: Anchor) => this.drawingLayer.pointOf(anchor),
      anchorAt: (point: Point, magnet: boolean) => this.drawingLayer.anchorAt(point, magnet),
      redraw: () => this.scenarioHandles.redraw(),
    };
    this.scenarioHandles = new ScenarioHandles(pointSurface, () => this.theme.background);
    this.scenarioPoints = new ScenarioPointInteraction(pointSurface, {
      pointPlaced: (field, index, time, price, kind) => {
        this.pointsPlaced++;
        this.scenarioListener.pointPlaced?.(field, index, time, price, kind);
      },
      pointSelected: (key) => {
        if (key !== undefined) {
          this.drawingInteraction.select(undefined);
        }

        this.scenarioListener.pointSelected?.(key);
      },
      scenarioToolChanged: (code) => this.scenarioListener.scenarioToolChanged?.(code),
    });
    this.scenarioHandles.bind(this.scenarioPoints);
    this.candles.attachPrimitive(this.scenarioHandles);

    // The range tool of the chart page (SC22): a band while dragging, the range reported when let go.
    const rangeSurface = {
      anchorAt: (point: Point, magnet: boolean) => this.drawingLayer.anchorAt(point, magnet),
      xOf: (time: number) => {
        const index = this.drawingLayer.indexOf(time);
        const x = index === undefined ? null : this.chart.timeScale().logicalToCoordinate(index as Logical);
        return x === null ? undefined : x;
      },
      redraw: () => this.rangeBand.redraw(),
    };
    this.rangeBand = new RangeBand(rangeSurface, () => this.chart.timeScale().options().barSpacing);
    this.rangeSelection = new RangeSelection(rangeSurface, {
      rangeSelected: (from, to) => this.rangeListener.rangeSelected?.(from, to),
      rangeToolChanged: (on) => this.rangeListener.rangeToolChanged?.(on),
    });
    this.rangeBand.bind(this.rangeSelection);
    this.candles.attachPrimitive(this.rangeBand);
    // A form of an expert scenario is selected by clicking it (decision 0013); a handle on top of it wins.
    this.overlaySelection = new OverlaySelection(this.overlayLayer, {
      overlayClicked: (group, key) => this.overlayListener.overlayClicked?.(group, key),
    });
    this.pointerInput = new PointerInput(canvasHost, this.chart, [this.rangeSelection, this.scenarioPoints, this.overlaySelection, this.drawingInteraction]);
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
      this.indicatorSet.showValues(event.time === undefined ? undefined : (event.time as number));
      this.showTooltip(event);
    });
    this.chart.timeScale().subscribeVisibleLogicalRangeChange((range) => this.onRangeChanged(range));
  }

  /**
   * Shows another asset or timeframe. The number of visible candles is kept; the view stays at the
   * latest candles or, scrolled back within the same asset, at the same time (CH3).
   */
  setSeries(request: SeriesRequest): Promise<void> {
    this.seriesLoaded = this.loadSeries(request);
    return this.seriesLoaded;
  }

  /**
   * Positions the chart on a time span (SC2), with a small margin on both sides: waits for the series being loaded,
   * loads older pages until the start of the span is in memory (or the history ends), then sets the view. Another
   * setSeries meanwhile wins: then nothing is changed.
   */
  async showTimeRange(from: number, to: number, margin: boolean = true): Promise<void> {
    await this.seriesLoaded;
    const generation = this.generation;
    for (let attempt = 0; attempt < 100 && generation === this.generation && this.data.hasMore && (this.data.oldestTime ?? 0) > from; attempt++) {
      if (this.loadingOlder) {
        await new Promise((resolve) => setTimeout(resolve, 30));
        continue;
      }

      const oldest = this.data.oldestTime;
      await this.loadOlder();
      if (this.data.oldestTime === oldest) {
        break; // the page failed or brought nothing: show what is there
      }
    }

    const mapping = this.timeMapping();
    const first = mapping?.indexOf(from);
    const last = mapping?.indexOf(to);
    if (generation !== this.generation || this.disposed || first === undefined || last === undefined) {
      return;
    }

    const bars = margin ? Math.max(2, Math.round((last - first) * 0.05)) : 0;
    // The library does not zoom out below its smallest bar spacing (0.5 px); a long span of a small timeframe (a year
    // of 4h candles) needs less, so it is lowered as far as the span needs, never below 0.05 px.
    const width = this.chart.timeScale().width();
    const spacing = width / Math.max(1, last - first + 2 * bars + 1);
    if (spacing < MIN_BAR_SPACING) {
      this.chart.timeScale().applyOptions({ minBarSpacing: Math.max(0.05, spacing * 0.95) });
    }

    this.chart.timeScale().setVisibleLogicalRange({ from: first - bars, to: last + bars });
  }

  /** The times of the first and the last candle in view (Unix seconds), or undefined without candles. */
  visibleTimeRange(): { from: number; to: number } | undefined {
    const range = this.chart.timeScale().getVisibleLogicalRange();
    const mapping = this.timeMapping();
    const from = range === null ? undefined : mapping?.timeOf(Math.ceil(range.from));
    const to = range === null ? undefined : mapping?.timeOf(Math.floor(range.to));
    return from === undefined || to === undefined ? undefined : { from, to };
  }

  private async loadSeries(request: SeriesRequest): Promise<void> {
    const started = performance.now();
    const generation = ++this.generation;
    const previous = this.request;
    const view = this.currentView();
    const assetChanged = previous === undefined || previous.source !== request.source || previous.symbol !== request.symbol;

    this.request = request;
    this.loadingOlder = false;
    this.showMessage(this.data.bars.length === 0 ? "Loading..." : undefined);
    this.indicatorSet.load(); // in parallel with the candles, drawn with them

    let data: SeriesData;
    try {
      data = new SeriesData(request.window ?? undefined);
      data.setLatest(await this.fetchPage(request, request.window?.to, FIRST_PAGE_SIZE));
    } catch (error) {
      if (generation === this.generation) {
        this.showError(error);
        this.indicatorSet.release();
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
    this.indicatorSet.applyPriceFormat();
    // A new series starts with the price scale fitted to its candles, whatever the user did before (CH1).
    this.candles.priceScale().applyOptions({ autoScale: true });

    if (data.bars.length === 0) {
      this.draw();
      this.indicatorSet.release();
      this.showMessage(`No candles for ${request.symbol} in ${request.timeframe}.`);
      return;
    }

    this.draw();
    this.indicatorSet.release();
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
    void this.indicatorSet.refresh();
    let page;
    try {
      page = await this.fetchPage(request, request.window?.to, FIRST_PAGE_SIZE);
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
    this.indicatorSet.scalesChanged(); // the mode is for the price pane only
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

  /**
   * Shows read-only shapes (OV1-OV6, SC8) as the Api gives them, placed by time and price on every timeframe; the
   * groups whose keys are given are hidden (OV2, SC2, SC21). An empty list removes them.
   */
  setOverlays(groups: readonly OverlayGroupRecord[] | null | undefined, hiddenKeys?: readonly string[] | null): void {
    this.overlayLayer.set(parseGroups(groups), hiddenKeys ?? []);
  }

  /** Shows these drawings of the asset (DR3), placed by time and price on the candles of the timeframe shown. */
  setDrawings(drawings: readonly Drawing[]): void {
    this.drawingInteraction.setDrawings(drawings);
  }

  /** The drawings as the chart has them now, with what the user has changed. */
  drawings(): readonly Drawing[] {
    return this.drawingInteraction.all();
  }

  /** Chooses the tool to place next (undefined: the pointer); a style given replaces the default of the tool. */
  setTool(tool: ToolCode | null | undefined, style?: DrawingStyle | null, extras?: Record<string, unknown> | null): void {
    if (tool !== null && tool !== undefined && this.scenarioPoints.tool() !== undefined) {
      this.scenarioPoints.setTool(undefined); // one tool at a time
    }

    if (tool !== null && tool !== undefined) {
      this.rangeSelection.setActive(false);
    }

    this.drawingInteraction.setTool(tool ?? undefined, style ?? undefined, extras ?? {});
  }

  /**
   * The named points of an expert scenario (SC10): those of the level shown are editable and get a handle; the
   * others are drawn by the overlay only. Called with the points the server snapped; an empty list removes them.
   */
  setScenarioPoints(points: readonly ScenarioHandle[] | null | undefined): void {
    this.scenarioPoints.setPoints(points ?? []);
  }

  /** A point tool ("1-2", "i-v", an exit, ...), or the pointer with null. Choosing one puts the drawing tool away. */
  setScenarioTool(tool: ScenarioTool | null | undefined): void {
    if (tool !== null && tool !== undefined) {
      this.drawingInteraction.setTool(undefined);
      this.rangeSelection.setActive(false);
    }

    this.scenarioPoints.setTool(tool ?? undefined);
  }

  /** The range tool of the chart page (SC22), on or off. Choosing it puts the other tools away. */
  setRangeTool(on: boolean | null | undefined): void {
    if (on === true) {
      this.drawingInteraction.setTool(undefined);
      this.scenarioPoints.setTool(undefined);
    }

    this.rangeSelection.setActive(on === true);
  }

  onRangeEvents(listener: Partial<RangeEvents>): void {
    this.rangeListener = listener;
  }

  /** The groups whose shapes can be selected by a click (the forms of the level shown); an empty list: none. */
  setOverlaySelectable(keys: readonly string[] | null | undefined): void {
    this.overlayLayer.setSelectable(keys ?? []);
  }

  /** Selects a group, or one shape of it by its key, or nothing. */
  selectOverlay(group: string | null | undefined, key?: string | null): void {
    this.overlayLayer.select(group ?? undefined, key ?? undefined);
  }

  onOverlayEvents(listener: Partial<OverlaySelectionEvents>): void {
    this.overlayListener = listener;
  }

  /** Selects a point by its key ("<field>" or "<field>#<index>"), or none. */
  selectScenarioPoint(key: string | null | undefined): void {
    this.scenarioPoints.select(key ?? undefined);
  }

  onScenarioPointEvents(listener: Partial<ScenarioPointEvents>): void {
    this.scenarioListener = listener;
  }

  /** The magnet and "all drawings hidden" (DR2, DR6). */
  setDrawingOptions(options: Partial<DrawingOptions>): void {
    this.drawingInteraction.setOptions(options);
  }

  selectDrawing(id: string | null | undefined): void {
    this.drawingInteraction.select(id ?? undefined);
  }

  /**
   * Shows these indicators of the asset (IN1), on every timeframe, calculated from the candles shown. Unchanged ones
   * keep their values, a changed parameter fetches again, a changed style only redraws.
   */
  setIndicators(instances: readonly IndicatorInstance[] | null | undefined): void {
    this.indicatorSet.set(instances ?? []);
  }

  /** The indicators as the chart shows them. */
  indicators(): readonly IndicatorInstance[] {
    return this.indicatorSet.instances();
  }

  /** The values the chart holds of an indicator, for checks in the browser; changes nothing. */
  indicatorValues(id: string): { t: readonly number[]; lines: Record<string, readonly (number | null)[]> } | undefined {
    const values = this.indicatorSet.valuesOf(id);
    const instance = this.indicatorSet.instances().find((candidate) => candidate.id === id);
    if (values === undefined || instance === undefined) {
      return undefined;
    }

    const lines: Record<string, readonly (number | null)[]> = {};
    for (const line of ["value", "basis", "upper", "lower", "macd", "signal", "histogram"]) {
      if (values.line(line).length > 0) {
        lines[line] = values.line(line);
      }
    }

    return { t: values.t, lines };
  }

  /** Who hears the actions of the legend lines (hide, settings, remove); Web in step 6 of the plan. */
  onIndicatorActions(listener: (id: string, action: IndicatorAction) => void): void {
    this.indicatorSet.onActions(listener);
  }

  /** Who hears what the user does with drawings; Web in step 6 of the plan. */
  onDrawingEvents(listener: Partial<DrawingEvents>): void {
    this.drawingListener = listener;
  }

  /** Where a time and price are drawn, in pixels of the pane, for checks in the browser; changes nothing. */
  pointOf(time: number, price: number): Point | undefined {
    return this.drawingLayer.pointOf({ time, price });
  }

  /** Where the anchors of a drawing are drawn, in pixels of the pane, for checks in the browser. */
  drawingPoints(id: string): (Point | undefined)[] {
    const drawing = this.drawingInteraction.all().find((candidate) => candidate.id === id);
    return drawing?.anchors.map((anchor) => this.drawingLayer.pointOf(anchor)) ?? [];
  }

  stats(): PaneStats {
    const range = this.chart.timeScale().getVisibleLogicalRange();
    const bars = this.data.bars;
    return {
      lastDrawMs: this.lastDrawMs,
      lastIndicatorsMs: this.indicatorSet.lastLoadMs,
      bars: bars.length,
      requests: this.requests,
      indicators: this.indicatorSet.count,
      indicatorRequests: this.indicatorRequests,
      visibleFrom: range?.from,
      visibleTo: range?.to,
      firstTime: bars[0]?.time,
      lastTime: bars[bars.length - 1]?.time,
      partial: this.data.partial,
      drawings: this.drawingInteraction.all().length,
      drawingsCreated: this.drawingsCreated,
      drawingChanges: this.drawingChanges,
      selectedDrawing: this.drawingInteraction.selected(),
      tool: this.drawingInteraction.tool(),
      overlayShapes: this.overlayLayer.drawnCount(),
      scenarioHandles: this.scenarioPoints.visible().length,
      scenarioTool: this.scenarioPoints.tool(),
      scenarioNext: this.scenarioPoints.nextField(),
      selectedPoint: this.scenarioPoints.selected(),
      pointsPlaced: this.pointsPlaced,
      rangeTool: this.rangeSelection.isActive(),
      selectedOverlay: this.overlayLayer.selected(),
      rangeBand: this.rangeSelection.band(),
    };
  }

  dispose(): void {
    this.disposed = true;
    this.generation++;
    this.onDisposed();
    this.pointerInput.dispose();
    this.indicatorSet.dispose();
    this.chart.remove();
    this.container.replaceChildren();
    this.container.classList.remove("tv-chart");
  }

  private async fetchPage(request: SeriesKey, to: number | undefined, limit: number) {
    this.requests++;
    return fetchCandles(request, to, limit, this.fetcher);
  }

  /**
   * Exact anchor times (step 9 of the plan): an anchor on the high or low of its candle, and a wave label, gets
   * the time of the candle of the smallest timeframe that made that price, looked up in one request per anchor.
   * Undefined when there is nothing to look up: the timeframe shown is the smallest, or no anchor is on an
   * extreme. A failed request leaves the times as they are.
   */
  private exactTimes(drawing: Drawing, changed: readonly number[]): Promise<Drawing> | undefined {
    const request = this.request;
    const step = request === undefined ? undefined : timeframeSeconds(request.timeframe);
    const finest = request?.timeframes === undefined ? undefined : finestOf(request.timeframes);
    const mapping = this.timeMapping();
    if (request === undefined || step === undefined || finest === undefined || finest.seconds >= step || mapping === undefined) {
      return undefined;
    }

    const lookups: { index: number; bar: Bar; extreme: Extreme; price: number }[] = [];
    for (const index of changed) {
      const anchor = drawing.anchors[index];
      const barIndex = anchor === undefined ? undefined : mapping.indexOf(anchor.time);
      const bar = barIndex === undefined ? undefined : this.data.bars[barIndex];
      if (anchor === undefined || bar === undefined || anchor.time !== bar.time) {
        continue; // beside the candles, or exact already
      }

      if (drawing.tool === "waveLabel") {
        const above = (drawing["wave"] as { side?: string } | undefined)?.side === "above";
        lookups.push({ index, bar, extreme: above ? "high" : "low", price: above ? bar.high : bar.low });
        continue;
      }

      const extreme = anchor.price === null ? undefined : extremeOf(bar, anchor.price);
      if (extreme !== undefined) {
        lookups.push({ index, bar, extreme, price: anchor.price! });
      }
    }

    if (lookups.length === 0) {
      return undefined;
    }

    const key = { source: request.source, symbol: request.symbol, timeframe: finest.code };
    const perCandle = Math.min(Math.ceil(step / finest.seconds), 5000);
    return (async () => {
      const anchors = [...drawing.anchors];
      for (const lookup of lookups) {
        this.requests++;
        const page = await fetchCandles(key, lookup.bar.time + step, perCandle, this.fetcher);
        const time = madeAt(
          page.bars.filter((candle) => candle.time >= lookup.bar.time),
          lookup.extreme,
          lookup.price,
        );
        if (time !== undefined) {
          anchors[lookup.index] = { ...anchors[lookup.index]!, time };
        }
      }

      return { ...drawing, anchors };
    })();
  }

  /** The time mapping of the candles in memory; built again only when they or the timeframe change. */
  private timeMapping(): TimeMapping | undefined {
    const bars = this.data.bars;
    const timeframe = this.request?.timeframe ?? "";
    const cache = this.mappingCache;
    if (cache !== undefined && cache.bars === bars && cache.timeframe === timeframe) {
      return cache.mapping;
    }

    const step = timeframeSeconds(timeframe);
    const mapping = step === undefined || bars.length === 0 ? undefined : new TimeMapping(bars.map((bar) => bar.time), step);
    this.mappingCache = { bars, timeframe, mapping };
    return mapping;
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
    void this.indicatorSet.loadOlder(oldest); // the same page of every indicator, in parallel
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

  /**
   * Pointing at a gap icon shows from when to when candles are missing, and why (MD4); pointing at an overlay shape
   * shows its detail under the title of its group (SC4).
   */
  private showTooltip(event: MouseEventParams): void {
    const id = typeof event.hoveredObjectId === "string" ? event.hoveredObjectId : "";
    const marker = id.startsWith(GAP_ID_PREFIX) ? this.gapMarkers.get(Number(id.slice(GAP_ID_PREFIX.length))) : undefined;
    const overlay = marker === undefined ? this.overlayLayer.hovered(id) : undefined;
    const text = marker !== undefined ? gapText(marker.gap) : overlay !== undefined ? overlay.group.title + "\n" + (overlay.shape.detail ?? overlay.shape.text ?? "") : undefined;
    if (text === undefined || event.point === undefined) {
      this.tooltip.hidden = true;
      return;
    }

    this.tooltip.textContent = text;
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
