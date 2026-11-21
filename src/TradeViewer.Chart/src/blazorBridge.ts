// What Blazor cannot do from C# alone: hand JavaScript callbacks to the pane, listen to keys on the whole page,
// and ask before the window is closed. The target is a DotNetObjectReference of ChartHost.
import type { ChartPane } from "./chartPane";

/** A DotNetObjectReference as Blazor passes it to JavaScript. */
export interface DotNetTarget {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

/** Disposes what a bind call set up. */
export interface Binding {
  dispose(): void;
}

/** Reports the drawing events of the pane to ChartHost: DrawingCreated, DrawingChanged, SelectionChanged, ToolChanged. */
export function bindDrawingEvents(pane: ChartPane, target: DotNetTarget): void {
  pane.onDrawingEvents({
    drawingCreated: (drawing) => void target.invokeMethodAsync("DrawingCreated", drawing),
    drawingChanged: (drawing) => void target.invokeMethodAsync("DrawingChanged", drawing),
    selectionChanged: (id) => void target.invokeMethodAsync("SelectionChanged", id ?? null),
    toolChanged: (tool) => void target.invokeMethodAsync("ToolChanged", tool ?? null),
  });
}

/** Reports the events of the named points of a scenario to ChartHost (SC10): PointPlaced, PointSelected, ScenarioToolChanged. */
export function bindScenarioEvents(pane: ChartPane, target: DotNetTarget): void {
  pane.onScenarioPointEvents({
    pointPlaced: (field, index, time, price, kind) => void target.invokeMethodAsync("PointPlaced", field, index ?? null, time, price, kind ?? null),
    pointSelected: (key) => void target.invokeMethodAsync("PointSelected", key ?? null),
    scenarioToolChanged: (code) => void target.invokeMethodAsync("ScenarioToolChanged", code ?? null),
  });
}

/** Reports the range tool of the chart page to ChartHost (SC22): RangeSelected(from, to), RangeToolChanged(on). */
export function bindRangeEvents(pane: ChartPane, target: DotNetTarget): void {
  pane.onRangeEvents({
    rangeSelected: (from, to) => void target.invokeMethodAsync("RangeSelected", from, to),
    rangeToolChanged: (on) => void target.invokeMethodAsync("RangeToolChanged", on),
  });
}

/** Reports the actions of the indicator legends to ChartHost: IndicatorAction(id, "hide" | "settings" | "remove"). */
export function bindIndicatorActions(pane: ChartPane, target: DotNetTarget): void {
  pane.onIndicatorActions((id, action) => void target.invokeMethodAsync("IndicatorAction", id, action));
}

/** The keys of the chart page (DR2): delete, undo, redo. */
export type PageKey = "delete" | "undo" | "redo";

/** Which key of the chart page an event is, if any. Keys typed into a field belong to the field. */
export function pageKeyOf(event: Pick<KeyboardEvent, "key" | "ctrlKey" | "metaKey" | "shiftKey" | "altKey">, typing: boolean): PageKey | undefined {
  if (typing || event.altKey) {
    return undefined;
  }

  const command = event.ctrlKey || event.metaKey;
  const key = event.key.toLowerCase();
  if (!command && (event.key === "Delete" || event.key === "Backspace")) {
    return "delete";
  }

  if (command && key === "z") {
    return event.shiftKey ? "redo" : "undo";
  }

  if (command && key === "y") {
    return "redo";
  }

  return undefined;
}

/** Reports the keys of the chart page to ChartHost (Key). */
export function bindPageKeys(target: DotNetTarget): Binding {
  const listener = (event: KeyboardEvent) => {
    const key = pageKeyOf(event, isTyping(event.target));
    if (key !== undefined) {
      event.preventDefault();
      void target.invokeMethodAsync("Key", key);
    }
  };

  document.addEventListener("keydown", listener);
  return { dispose: () => document.removeEventListener("keydown", listener) };
}

let warnOnLeave = false;
let leaveListener: ((event: BeforeUnloadEvent) => void) | undefined;

/** While a change is not saved, closing or reloading the window asks first (priority 1). */
export function warnWhenLeaving(on: boolean): void {
  warnOnLeave = on;
  if (leaveListener === undefined) {
    leaveListener = (event) => {
      if (warnOnLeave) {
        event.preventDefault();
        event.returnValue = "";
      }
    };
    window.addEventListener("beforeunload", leaveListener);
  }
}

function isTyping(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  return target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName);
}
