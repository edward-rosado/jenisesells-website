import { describe, it, expect } from "vitest";
import { getStateBounds, STATE_BOUNDING_BOXES } from "./stateBounds";

describe("getStateBounds", () => {
  it("returns bounding box for known state (NJ)", () => {
    const bounds = getStateBounds("NJ");
    expect(bounds).toEqual({
      north: 41.3574,
      south: 38.9285,
      east: -73.8938,
      west: -75.5598,
    });
  });

  it("returns bounding box for another known state (NY)", () => {
    const bounds = getStateBounds("NY");
    expect(bounds).toBeDefined();
    expect(bounds!.north).toBeGreaterThan(bounds!.south);
  });

  it("returns undefined for unknown state code", () => {
    expect(getStateBounds("ZZ")).toBeUndefined();
  });

  it("returns undefined for empty string", () => {
    expect(getStateBounds("")).toBeUndefined();
  });

  it("returns undefined for lowercase state code (case-sensitive)", () => {
    expect(getStateBounds("nj")).toBeUndefined();
  });
});

describe("STATE_BOUNDING_BOXES", () => {
  it("has valid bounds for all entries (north > south, east > west)", () => {
    for (const [state, bounds] of Object.entries(STATE_BOUNDING_BOXES)) {
      expect(bounds.north, `${state}: north > south`).toBeGreaterThan(bounds.south);
      expect(bounds.east, `${state}: east > west`).toBeGreaterThan(bounds.west);
    }
  });
});
