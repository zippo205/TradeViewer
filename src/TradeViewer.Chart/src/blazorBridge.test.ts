import { describe, expect, it } from "vitest";
import { pageKeyOf } from "./blazorBridge";

const key = (name: string, modifiers: { ctrl?: boolean; meta?: boolean; shift?: boolean; alt?: boolean } = {}) => ({
  key: name,
  ctrlKey: modifiers.ctrl ?? false,
  metaKey: modifiers.meta ?? false,
  shiftKey: modifiers.shift ?? false,
  altKey: modifiers.alt ?? false,
});

describe("pageKeyOf", () => {
  it("knows delete, undo and redo, also with the command key of a Mac", () => {
    expect(pageKeyOf(key("Delete"), false)).toBe("delete");
    expect(pageKeyOf(key("Backspace"), false)).toBe("delete");
    expect(pageKeyOf(key("z", { ctrl: true }), false)).toBe("undo");
    expect(pageKeyOf(key("Z", { meta: true }), false)).toBe("undo");
    expect(pageKeyOf(key("y", { ctrl: true }), false)).toBe("redo");
    expect(pageKeyOf(key("z", { ctrl: true, shift: true }), false)).toBe("redo");
  });

  it("leaves keys typed into a field to the field, and other keys to the page", () => {
    expect(pageKeyOf(key("Backspace"), true)).toBeUndefined();
    expect(pageKeyOf(key("z", { ctrl: true }), true)).toBeUndefined();
    expect(pageKeyOf(key("z"), false)).toBeUndefined();
    expect(pageKeyOf(key("Delete", { ctrl: true }), false)).toBeUndefined();
    expect(pageKeyOf(key("z", { ctrl: true, alt: true }), false)).toBeUndefined();
  });
});
