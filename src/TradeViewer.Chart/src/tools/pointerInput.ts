import type { IChartApi } from "lightweight-charts";
import type { Point } from "./geometry";
import type { DrawingInteraction } from "./interaction";

/**
 * Takes the pointer before the chart does. A press that the interaction takes (placing, a drawing, a handle)
 * never reaches the library, so the chart does not pan; any other press goes to the chart as before (NF2).
 * While a drawing is placed or dragged, the moves and the release anywhere in the window belong to it, and
 * Escape cancels. Only the module works meanwhile: no call to Blazor until the result is reported (NF3).
 */
export class PointerInput {
  private readonly onDown = (event: MouseEvent) => this.press(event);
  private readonly onMove = (event: MouseEvent) => this.move(event);
  private readonly onUp = (event: MouseEvent) => this.release(event);
  private readonly onKey = (event: KeyboardEvent) => this.key(event);

  constructor(
    private readonly element: HTMLElement,
    private readonly chart: IChartApi,
    private readonly interaction: DrawingInteraction,
  ) {
    // The capture phase on an element around the chart: the library listens on its own elements inside it.
    element.addEventListener("mousedown", this.onDown, { capture: true });
    window.addEventListener("mousemove", this.onMove, { capture: true });
    window.addEventListener("mouseup", this.onUp, { capture: true });
    window.addEventListener("keydown", this.onKey, { capture: true });
  }

  dispose(): void {
    this.element.removeEventListener("mousedown", this.onDown, { capture: true });
    window.removeEventListener("mousemove", this.onMove, { capture: true });
    window.removeEventListener("mouseup", this.onUp, { capture: true });
    window.removeEventListener("keydown", this.onKey, { capture: true });
  }

  private press(event: MouseEvent): void {
    if (event.button !== 0) {
      return;
    }

    const point = this.local(event);
    if (point.x < 0 || point.x > this.chart.timeScale().width()) {
      return; // on the price scale: that belongs to the chart
    }

    if (this.interaction.pointerDown(point)) {
      event.stopPropagation();
      event.preventDefault();
    }
  }

  private move(event: MouseEvent): void {
    if (this.interaction.isBusy()) {
      this.interaction.pointerMove(this.local(event));
    }
  }

  private release(event: MouseEvent): void {
    if (event.button === 0 && this.interaction.isBusy()) {
      this.interaction.pointerUp();
    }
  }

  private key(event: KeyboardEvent): void {
    if (event.key === "Escape" && this.interaction.cancel()) {
      event.stopPropagation();
      event.preventDefault();
    }
  }

  /** The pointer in pixels of the pane: the pane starts at the top left of the element (no left scale). */
  private local(event: MouseEvent): Point {
    const box = this.element.getBoundingClientRect();
    return { x: event.clientX - box.left, y: event.clientY - box.top };
  }
}
