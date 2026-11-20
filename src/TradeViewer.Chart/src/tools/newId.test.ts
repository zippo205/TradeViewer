import { describe, expect, it } from "vitest";
import { newId, type RandomSource } from "./newId";

const uuidV4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

describe("newId", () => {
  it("uses randomUUID where the browser offers it", () => {
    const source: RandomSource = { randomUUID: () => "from-random-uuid", getRandomValues: (a) => a };
    expect(newId(source)).toBe("from-random-uuid");
  });

  it("builds a version 4 UUID from getRandomValues outside a secure context (plain HTTP)", () => {
    const source: RandomSource = { getRandomValues: (a) => crypto.getRandomValues(a) };
    const ids = new Set(Array.from({ length: 50 }, () => newId(source)));
    expect(ids.size).toBe(50);
    for (const id of ids) expect(id).toMatch(uuidV4);
  });

  it("sets the version and variant bits whatever the random bytes are", () => {
    const allOnes: RandomSource = { getRandomValues: (a) => { a.fill(0xff); return a; } };
    const allZeros: RandomSource = { getRandomValues: (a) => { a.fill(0); return a; } };
    expect(newId(allOnes)).toBe("ffffffff-ffff-4fff-bfff-ffffffffffff");
    expect(newId(allZeros)).toBe("00000000-0000-4000-8000-000000000000");
  });
});
