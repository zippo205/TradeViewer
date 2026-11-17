import { describe, expect, it } from "vitest";
import { patternLabels, withSides } from "./wavePatterns";

const anchors = (...prices: number[]) => prices.map((price, index) => ({ time: index * 3600, price }));

describe("patternLabels", () => {
  it("labels an impulse 1 to 5 on the points after the start, highs above and lows below", () => {
    const labels = patternLabels(anchors(100, 120, 110, 140, 130, 150), ["1", "2", "3", "4", "5"], "minor");

    expect(labels.map((label) => `${label.point}${label.mark.label}${label.mark.side[0]}`)).toEqual(["11a", "22b", "33a", "44b", "55a"]);
    expect(labels.every((label) => label.mark.degree === "minor")).toBe(true);
  });

  it("labels a correction down: A below, B above, C below", () => {
    const labels = patternLabels(anchors(150, 130, 140, 120), ["A", "B", "C"], "intermediate");

    expect(labels.map((label) => label.mark.label + label.mark.side[0])).toEqual(["Ab", "Ba", "Cb"]);
  });

  it("labels only the points placed so far, as the preview of placing shows them", () => {
    expect(patternLabels(anchors(100, 120), ["W", "X", "Y", "X", "Z"], "minute").map((label) => label.mark.label)).toEqual(["W"]);
  });
});

describe("withSides", () => {
  it("puts a label on the other side when its point moved from a high to a low", () => {
    const labels = patternLabels(anchors(100, 120, 110), ["1", "2"], "minor");
    const moved = withSides(anchors(100, 90, 110), labels);

    expect(moved.map((label) => label.mark.label + label.mark.side[0])).toEqual(["1b", "2a"]);
  });
});
