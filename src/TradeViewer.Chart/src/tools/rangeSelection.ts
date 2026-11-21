import type { Anchor } from "../primitives/drawing";
import type { Point } from "./geometry";
import type { PointerTarget } from "./pointerInput";

// The range tool of the chart page (SC22): the user drags across the chart, a band shows the candles chosen, and
// letting go reports the open times of the first and the last of them. Then the tool is put away. Only the module
// works while dragging (NF3); Blazor hears of it once, when the range is chosen.

/** What the range tool needs of the chart: pixels to a whole-bar anchor, the x of a candle, a new frame. */
export interface RangeSurface {
  anchorAt(point: Point, magnet: boolean): Anchor | undefined;
  xOf(time: number): number | undefined;
  redraw(): void;
}

/** What the range tool reports. */
export interface RangeEvents {
  /** The open times of the first and the last candle of the range, the first not after the last. */
  rangeSelected(from: number, to: number): void;
  /** The tool was chosen (true) or put away (false): after a range, with Escape, or by another tool. */
  rangeToolChanged(on: boolean): void;
}

export class RangeSelection implements PointerTarget {
  private active = false;
  private start: number | undefined;
  private end: number | undefined;

  constructor(
    private readonly surface: RangeSurface,
    private readonly events: Partial<RangeEvents>,
  ) {}

  /** Chooses the tool or puts it away; a band being dragged goes. */
  setActive(on: boolean): void {
    this.start = undefined;
    this.end = undefined;
    if (this.active !== on) {
      this.active = on;
      this.events.rangeToolChanged?.(on);
    }

    this.surface.redraw();
  }

  isActive(): boolean {
    return this.active;
  }

  /** The band to draw while dragging: the open times of its first and last candle. */
  band(): { from: number; to: number } | undefined {
    if (this.start === undefined || this.end === undefined) {
      return undefined;
    }

    return { from: Math.min(this.start, this.end), to: Math.max(this.start, this.end) };
  }

  /** While the tool is chosen every press is its own, so the chart does not pan. */
  isBusy(): boolean {
    return this.active;
  }

  cursorAt(): string | undefined {
    return this.active ? "col-resize" : undefined;
  }

  pointerDown(point: Point): boolean {
    if (!this.active) {
      return false;
    }

    const anchor = this.surface.anchorAt(point, false);
    if (anchor !== undefined) {
      this.start = anchor.time;
      this.end = anchor.time;
      this.surface.redraw();
    }

    return true;
  }

  pointerMove(point: Point): boolean {
    if (!this.active) {
      return false;
    }

    if (this.start !== undefined) {
      const anchor = this.surface.anchorAt(point, false);
      if (anchor !== undefined && anchor.time !== this.end) {
        this.end = anchor.time;
        this.surface.redraw();
      }
    }

    return true;
  }

  /** Letting go reports the range and puts the tool away. */
  pointerUp(): boolean {
    const band = this.band();
    if (!this.active || band === undefined) {
      return false;
    }

    this.setActive(false);
    this.events.rangeSelected?.(band.from, band.to);
    return true;
  }

  /** Escape: the band goes and the tool is put away. */
  cancel(): boolean {
    if (!this.active) {
      return false;
    }

    this.setActive(false);
    return true;
  }

  finish(): boolean {
    return false;
  }
}
