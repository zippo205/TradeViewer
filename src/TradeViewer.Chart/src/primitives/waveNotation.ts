// The notation of the wave labels (DR5), the same table as WaveNotation in the Domain: each degree writes the
// labels 1-5 and A-E, W, X, Y in its own form. Double brackets, such as ((1)), are drawn as a circle.

export type WaveDegree =
  | "grandSupercycle"
  | "supercycle"
  | "cycle"
  | "primary"
  | "intermediate"
  | "minor"
  | "minute"
  | "minuette"
  | "subminuette";

export type WaveSide = "above" | "below";

/** A wave label as it is stored: degree, label and side, not the drawn text. */
export interface WaveMark {
  readonly degree: WaveDegree;
  readonly label: string;
  readonly side: WaveSide;
}

export type Enclosure = "none" | "parentheses" | "circle";

export interface WaveText {
  readonly text: string;
  readonly enclosure: Enclosure;
}

/** Largest first. */
export const WAVE_DEGREES: readonly WaveDegree[] = [
  "grandSupercycle",
  "supercycle",
  "cycle",
  "primary",
  "intermediate",
  "minor",
  "minute",
  "minuette",
  "subminuette",
];

export const WAVE_LABELS: readonly string[] = ["1", "2", "3", "4", "5", "A", "B", "C", "D", "E", "W", "X", "Y"];

const UPPER_ROMAN = ["I", "II", "III", "IV", "V"];
const LOWER_ROMAN = ["i", "ii", "iii", "iv", "v"];

/** The label as it is drawn in its degree; undefined for an unknown degree or label. */
export function waveText(degree: WaveDegree, label: string): WaveText | undefined {
  const index = WAVE_DEGREES.indexOf(degree);
  if (index < 0 || !WAVE_LABELS.includes(label)) {
    return undefined;
  }

  const group = Math.floor(index / 3); // 0: Grand Supercycle to Cycle, 1: Primary to Minor, 2: Minute to Subminuette
  const enclosure: Enclosure = index % 3 === 0 ? "circle" : index % 3 === 1 ? "parentheses" : "none";
  const number = "12345".indexOf(label);
  let text: string;
  if (number >= 0) {
    text = group === 0 ? UPPER_ROMAN[number]! : group === 1 ? label : LOWER_ROMAN[number]!;
  } else {
    text = group === 1 ? label : label.toLowerCase();
  }

  return { text, enclosure };
}

/** The wave mark of a drawing record, if it has a valid one. */
export function waveMarkOf(value: unknown): WaveMark | undefined {
  if (typeof value !== "object" || value === null) {
    return undefined;
  }

  const mark = value as Partial<WaveMark>;
  return WAVE_DEGREES.includes(mark.degree as WaveDegree) &&
    typeof mark.label === "string" &&
    WAVE_LABELS.includes(mark.label) &&
    (mark.side === "above" || mark.side === "below")
    ? (mark as WaveMark)
    : undefined;
}
