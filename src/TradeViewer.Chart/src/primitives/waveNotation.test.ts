import { describe, expect, it } from "vitest";
import { labelBox } from "./waveLabelDrawing";
import { WAVE_DEGREES, waveMarkOf, waveText, type WaveDegree } from "./waveNotation";

/** The label written out as in the table of the plan, e.g. ((1)) for a circle. */
function written(degree: WaveDegree, ...labels: string[]): string {
  return labels
    .map((label) => {
      const text = waveText(degree, label)!;
      return text.enclosure === "circle" ? `((${text.text}))` : text.enclosure === "parentheses" ? `(${text.text})` : text.text;
    })
    .join(" ");
}

describe("waveText", () => {
  it.each<[WaveDegree, string, string]>([
    ["grandSupercycle", "((I)) ((II)) ((III)) ((IV)) ((V))", "((a)) ((b)) ((c))"],
    ["supercycle", "(I) (II) (III) (IV) (V)", "(a) (b) (c)"],
    ["cycle", "I II III IV V", "a b c"],
    ["primary", "((1)) ((2)) ((3)) ((4)) ((5))", "((A)) ((B)) ((C))"],
    ["intermediate", "(1) (2) (3) (4) (5)", "(A) (B) (C)"],
    ["minor", "1 2 3 4 5", "A B C"],
    ["minute", "((i)) ((ii)) ((iii)) ((iv)) ((v))", "((a)) ((b)) ((c))"],
    ["minuette", "(i) (ii) (iii) (iv) (v)", "(a) (b) (c)"],
    ["subminuette", "i ii iii iv v", "a b c"],
  ])("%s writes the table of the plan (the same as WaveNotation in the Domain)", (degree, motive, corrective) => {
    expect(written(degree, "1", "2", "3", "4", "5")).toBe(motive);
    expect(written(degree, "A", "B", "C")).toBe(corrective);
  });

  it("writes W, X, Y, D and E in the form of the letters of the degree", () => {
    expect(written("minute", "W", "X", "Y", "D", "E")).toBe("((w)) ((x)) ((y)) ((d)) ((e))");
    expect(written("intermediate", "W", "Y")).toBe("(W) (Y)");
  });

  it("knows nine degrees and refuses what is not in the table", () => {
    expect(WAVE_DEGREES).toHaveLength(9);
    expect(waveText("minor", "6")).toBeUndefined();
    expect(waveText("huge" as WaveDegree, "1")).toBeUndefined();
  });
});

describe("waveMarkOf", () => {
  it("reads a valid mark of a record and nothing else", () => {
    expect(waveMarkOf({ degree: "minor", label: "C", side: "below" })).toEqual({ degree: "minor", label: "C", side: "below" });
    expect(waveMarkOf({ degree: "minor", label: "C", side: "left" })).toBeUndefined();
    expect(waveMarkOf(null)).toBeUndefined();
    expect(waveMarkOf("1")).toBeUndefined();
  });
});

describe("labelBox", () => {
  it("lies above the high or below the low it stands for, and a circle is at least as wide as high", () => {
    const above = labelBox({ x: 100, y: 200 }, { degree: "minor", label: "1", side: "above" })!;
    const below = labelBox({ x: 100, y: 200 }, { degree: "primary", label: "1", side: "below" })!;

    expect(above.to.y).toBeLessThan(200);
    expect(below.from.y).toBeGreaterThan(200);
    expect(below.to.x - below.from.x).toBeGreaterThanOrEqual(below.to.y - below.from.y);
  });
});
