// A surface with simple linear scales for the tests of the interaction: bar i at x = 10 i, price p at
// y = 1000 - p, 100 candles of 1h from T0, each with open 500, high 520, low 480, close 510.
import type { Anchor, Drawing } from "../primitives/drawing";
import type { PaneSize } from "../primitives/shape";
import type { Point } from "./geometry";
import type { DrawingSurface } from "./interaction";
import { snapPrice } from "./magnet";

export const T0 = 1_763_164_800;
export const HOUR = 3600;
export const BARS = 100;
export const BAR = { time: 0, open: 500, high: 520, low: 480, close: 510, volume: 1 };

export class FakeSurface implements DrawingSurface {
  redraws = 0;

  /** The anchor of bar i at a price. */
  static anchor(index: number, price: number): Anchor {
    return { time: T0 + index * HOUR, price };
  }

  /** The point of bar i at a price. */
  static point(index: number, price: number): Point {
    return { x: index * 10, y: 1000 - price };
  }

  pointsOf(drawing: Drawing): Point[] | undefined {
    return drawing.anchors.map((anchor) => FakeSurface.point(this.indexOf(anchor.time)!, anchor.price ?? 0));
  }

  anchorAt(point: Point, magnet: boolean): Anchor | undefined {
    const index = Math.round(point.x / 10);
    const price = magnet && index >= 0 && index < BARS ? snapPrice(BAR, point.y, (value) => 1000 - value) : this.priceAt(point.y);
    return price === undefined ? undefined : { time: this.timeOf(index), price };
  }

  barsBetween(fromX: number, toX: number): number {
    return Math.round((toX - fromX) / 10);
  }

  indexOf(time: number): number {
    return Math.floor((time - T0) / HOUR);
  }

  timeOf(index: number): number {
    return T0 + Math.round(index) * HOUR;
  }

  priceAt(y: number): number {
    return 1000 - y;
  }

  paneSize(): PaneSize {
    return { width: 1000, height: 600 };
  }

  redraw(): void {
    this.redraws++;
  }
}
