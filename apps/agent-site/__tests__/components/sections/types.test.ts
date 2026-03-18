/**
 * @vitest-environment node
 */
import { describe, it, expect } from "vitest";
import { clampRating, FTC_DISCLAIMER, getDisplayName } from "@/components/sections/types";
import { ACCOUNT, ACCOUNT_BROKER_ONLY, ACCOUNT_BROKERAGE_ONLY, AGENT_PROP } from "../fixtures";

describe("clampRating", () => {
  it("returns the rating when within 0-5", () => {
    expect(clampRating(3)).toBe(3);
    expect(clampRating(0)).toBe(0);
    expect(clampRating(5)).toBe(5);
  });

  it("clamps ratings above 5 to 5", () => {
    expect(clampRating(10)).toBe(5);
    expect(clampRating(100000)).toBe(5);
  });

  it("clamps negative ratings to 0", () => {
    expect(clampRating(-1)).toBe(0);
    expect(clampRating(-100)).toBe(0);
  });

  it("floors fractional ratings", () => {
    expect(clampRating(3.7)).toBe(3);
    expect(clampRating(4.9)).toBe(4);
  });

  it("handles NaN as 0", () => {
    expect(clampRating(NaN)).toBe(0);
  });

  it("handles 0 as 0", () => {
    expect(clampRating(0)).toBe(0);
  });
});

describe("getDisplayName", () => {
  it("returns agent name from AccountConfig when agent is present", () => {
    expect(getDisplayName(ACCOUNT)).toBe("Jane Smith");
  });

  it("returns broker name when AccountConfig has broker but no agent", () => {
    expect(getDisplayName(ACCOUNT_BROKER_ONLY)).toBe("Sam Broker");
  });

  it("returns brokerage name when AccountConfig has neither agent nor broker", () => {
    expect(getDisplayName(ACCOUNT_BROKERAGE_ONLY)).toBe("Brokerage LLC");
  });

  it("returns name directly from AgentConfig", () => {
    expect(getDisplayName(AGENT_PROP)).toBe("Explicit Agent");
  });
});

describe("FTC_DISCLAIMER", () => {
  it("contains required FTC language", () => {
    expect(FTC_DISCLAIMER).toContain("No compensation was provided");
    expect(FTC_DISCLAIMER).toContain("Individual results may vary");
  });
});
