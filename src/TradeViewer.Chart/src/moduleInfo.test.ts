import { describe, expect, it } from "vitest";
import packageJson from "../package.json";
import { moduleInfo } from "./moduleInfo";

describe("moduleInfo", () => {
  it("names the module and its version as in package.json", () => {
    expect(moduleInfo()).toEqual({ name: "tradeviewer-chart", version: packageJson.version });
  });
});
