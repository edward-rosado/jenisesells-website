import { describe, it, expect } from "vitest";
import { EventType } from "../src/event-types";

describe("EventType", () => {
  it("has PascalCase string values matching the backend FormEvent enum", () => {
    expect(EventType.Viewed).toBe("Viewed");
    expect(EventType.Started).toBe("Started");
    expect(EventType.Submitted).toBe("Submitted");
    expect(EventType.Succeeded).toBe("Succeeded");
    expect(EventType.Failed).toBe("Failed");
  });

  it("does NOT use legacy dot-notation lowercase format", () => {
    const values = Object.values(EventType);
    for (const value of values) {
      expect(value).not.toContain(".");
      expect(value).not.toMatch(/^[a-z]/); // must not start lowercase
    }
  });

  it("covers all five event lifecycle stages", () => {
    const values = Object.values(EventType);
    expect(values).toHaveLength(5);
    expect(values).toContain("Viewed");
    expect(values).toContain("Started");
    expect(values).toContain("Submitted");
    expect(values).toContain("Succeeded");
    expect(values).toContain("Failed");
  });

  it("enum keys match their string values (no key/value mismatch)", () => {
    for (const [key, value] of Object.entries(EventType)) {
      expect(key).toBe(value);
    }
  });
});
