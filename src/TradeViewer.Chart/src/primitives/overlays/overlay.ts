// The shapes of overlays and scenarios in the vocabulary of EWTS's drawings (docs/knowledge/ewts-interface.md,
// "Drawings"; Contracts/Overlays). They are read-only (OV3): drawn, hidden by group and pointed at, never edited.
// Times are Unix seconds (UTC) here; the records Blazor passes have ISO times.

export type OverlayShapeType = "line" | "label" | "level" | "zone" | "marker" | "verticalLine";

export interface OverlayPoint {
  readonly time: number;
  readonly price: number;
}

export interface OverlayShape {
  readonly type: string;
  readonly role: string;
  readonly points?: readonly OverlayPoint[];
  readonly time?: number;
  readonly price?: number;
  readonly text?: string;
  readonly side?: "above" | "below";
  readonly from?: number;
  /** Undefined: open to the right. */
  readonly to?: number;
  readonly lower?: number;
  readonly upper?: number;
  /** Shown when the user points at the shape. */
  readonly detail?: string;
  readonly style?: "solid" | "dotted" | "dashed";
  readonly weight?: "normal" | "strong";
  /** What the shape stands for where an editor can select it (a named point, e.g. trade.exits#1). */
  readonly key?: string;
}

export interface OverlayGroup {
  /** Stable for the same thing: the hidden groups of a chart stay hidden (OV2). */
  readonly key: string;
  readonly title: string;
  readonly kind: string;
  readonly timeframe?: string;
  /** An expert scenario's level (context, setup, trigger), SC21. */
  readonly level?: string;
  readonly shapes: readonly OverlayShape[];
}

/** The record of a group as the Api writes it (camelCase, ISO times, null for missing fields). */
export interface OverlayGroupRecord {
  readonly key: string;
  readonly title: string;
  readonly kind: string;
  readonly timeframe?: string | null;
  readonly level?: string | null;
  readonly shapes: readonly Record<string, unknown>[];
}

/**
 * The groups of the records. A shape whose type is unknown or whose fields cannot be read is dropped: new types may
 * be added by EWTS and a client ignores what it does not know (ewts-interface.md).
 */
export function parseGroups(records: readonly OverlayGroupRecord[] | null | undefined): OverlayGroup[] {
  return (records ?? []).map((record) => ({
    key: record.key,
    title: record.title,
    kind: record.kind,
    timeframe: record.timeframe ?? undefined,
    level: record.level ?? undefined,
    shapes: record.shapes.map(parseShape).filter((shape): shape is OverlayShape => shape !== undefined),
  }));
}

export function parseShape(record: Record<string, unknown>): OverlayShape | undefined {
  const type = text(record["type"]);
  const role = text(record["role"]) ?? "neutral";
  const shape = {
    type: type ?? "",
    role,
    time: seconds(record["time"]),
    price: number(record["price"]),
    text: text(record["text"]),
    side: record["side"] === "below" ? ("below" as const) : record["side"] === "above" ? ("above" as const) : undefined,
    from: seconds(record["from"]),
    to: seconds(record["to"]),
    lower: number(record["lower"]),
    upper: number(record["upper"]),
    detail: text(record["detail"]),
    style: record["style"] === "dotted" || record["style"] === "dashed" ? (record["style"] as "dotted" | "dashed") : undefined,
    weight: record["weight"] === "strong" ? ("strong" as const) : undefined,
    points: Array.isArray(record["points"]) ? parsePoints(record["points"]) : undefined,
    key: text(record["key"]),
  };

  switch (type) {
    case "line":
      return shape.points !== undefined && shape.points.length >= 2 ? shape : undefined;
    case "label":
    case "marker":
      return shape.time !== undefined && shape.price !== undefined ? shape : undefined;
    case "level":
      return shape.price !== undefined && shape.from !== undefined ? shape : undefined;
    case "zone":
      return shape.lower !== undefined && shape.upper !== undefined && shape.from !== undefined ? shape : undefined;
    case "verticalLine":
      return shape.time !== undefined ? shape : undefined;
    default:
      return undefined;
  }
}

function parsePoints(values: unknown[]): OverlayPoint[] | undefined {
  const points: OverlayPoint[] = [];
  for (const value of values) {
    const record = value as Record<string, unknown> | null;
    const time = seconds(record?.["time"]);
    const price = number(record?.["price"]);
    if (time === undefined || price === undefined) {
      return undefined;
    }

    points.push({ time, price });
  }

  return points;
}

function seconds(value: unknown): number | undefined {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }

  if (typeof value !== "string") {
    return undefined;
  }

  const milliseconds = Date.parse(value);
  return Number.isFinite(milliseconds) ? Math.floor(milliseconds / 1000) : undefined;
}

function number(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

function text(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}
