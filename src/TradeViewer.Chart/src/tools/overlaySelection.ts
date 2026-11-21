import type { Point } from "./geometry";
import type { PointerTarget } from "./pointerInput";

// Selecting a form of an expert scenario by clicking it (decision 0013): a line or label selects its group, a shape
// with a key (a marker of the trade) selects that point. Web offers Delete for what is selected.

/** What the selection needs of the overlay layer. */
export interface OverlaySelectionLayer {
  shapeAt(point: Point): { readonly group: string; readonly key?: string } | undefined;
  select(group: string | undefined, key?: string): void;
  selected(): { readonly group: string; readonly key?: string } | undefined;
}

export interface OverlaySelectionEvents {
  /** A shape of a selectable group was clicked (group, and the key of the shape if it has one), or undefined: none. */
  overlayClicked(group: string | undefined, key: string | undefined): void;
}

export class OverlaySelection implements PointerTarget {
  constructor(
    private readonly layer: OverlaySelectionLayer,
    private readonly events: Partial<OverlaySelectionEvents>,
  ) {}

  /** A click on a shape selects it and is not passed on (no pan); any other click clears the selection and is passed on. */
  pointerDown(point: Point): boolean {
    const hit = this.layer.shapeAt(point);
    const before = this.layer.selected();
    if (hit === undefined) {
      if (before !== undefined) {
        this.layer.select(undefined);
        this.events.overlayClicked?.(undefined, undefined);
      }

      return false;
    }

    this.layer.select(hit.group, hit.key);
    this.events.overlayClicked?.(hit.group, hit.key);
    return true;
  }

  pointerMove(): boolean {
    return false;
  }

  pointerUp(): boolean {
    return false;
  }

  isBusy(): boolean {
    return false;
  }

  /** Escape clears the selection. */
  cancel(): boolean {
    if (this.layer.selected() === undefined) {
      return false;
    }

    this.layer.select(undefined);
    this.events.overlayClicked?.(undefined, undefined);
    return true;
  }

  finish(): boolean {
    return false;
  }
}
