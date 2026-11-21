// Entry point of the chart module, imported by Blazor (ChartHost). It exposes the calls of the
// interop contract (architecture, chapter 10); the calls for overlays are added by their epic.
import { ChartPane, type PaneOptions } from "./chartPane";

export { moduleInfo } from "./moduleInfo";
export type { ModuleInfo } from "./moduleInfo";
export type { PaneOptions, PaneStats, SeriesRequest } from "./chartPane";
export type { Anchor, Drawing, DrawingStyle, LineStyle, ToolCode } from "./primitives/drawing";
export type { DrawingEvents, DrawingOptions } from "./tools/interaction";
export type { IndicatorInstance, IndicatorStyle, IndicatorType } from "./indicators/indicator";
export type { IndicatorAction } from "./indicators/legend";

const openPanes = new Set<ChartPane>();

/** Creates a chart in the element. Blazor keeps the returned pane and calls setSeries, setOptions, refresh and dispose on it. */
export function createPane(element: HTMLElement, options?: PaneOptions): ChartPane {
  const pane = new ChartPane(element, options ?? {}, undefined, () => openPanes.delete(pane));
  openPanes.add(pane);
  return pane;
}

/** The charts of this window, for measurements of the performance targets (NF1, NF2): `panes()[0].stats()`. */
export function panes(): ChartPane[] {
  return [...openPanes];
}

export { bindDrawingEvents, bindIndicatorActions, bindPageKeys, bindRangeEvents, bindScenarioEvents, warnWhenLeaving } from "./blazorBridge";
