import { describe, it, expect } from "vitest";
import { LEGAL_EFFECTIVE_DATE, STATE_NAMES, getStateName } from "../src/constants";

describe("legal constants", () => {
  it("exports LEGAL_EFFECTIVE_DATE as a non-empty string", () => {
    expect(LEGAL_EFFECTIVE_DATE).toBeTruthy();
    expect(typeof LEGAL_EFFECTIVE_DATE).toBe("string");
  });

  it("STATE_NAMES maps all 50 states plus DC", () => {
    expect(Object.keys(STATE_NAMES)).toHaveLength(51);
  });

  it("STATE_NAMES maps NJ to New Jersey", () => {
    expect(STATE_NAMES["NJ"]).toBe("New Jersey");
  });

  it("STATE_NAMES maps TX to Texas", () => {
    expect(STATE_NAMES["TX"]).toBe("Texas");
  });

  it("getStateName returns full name for known abbreviation", () => {
    expect(getStateName("NJ")).toBe("New Jersey");
  });

  it("getStateName returns abbreviation for unknown code", () => {
    expect(getStateName("XX")).toBe("XX");
  });
});
