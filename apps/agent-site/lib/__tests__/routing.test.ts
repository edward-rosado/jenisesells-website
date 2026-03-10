import { describe, it, expect } from "vitest";
import { extractAgentId } from "../routing";

describe("extractAgentId", () => {
  it("should extract agent-id from subdomain", () => {
    expect(extractAgentId("jenise-buckalew.realestatestar.com")).toBe("jenise-buckalew");
  });

  it("should return null for bare domain", () => {
    expect(extractAgentId("realestatestar.com")).toBeNull();
  });

  it("should return null for www subdomain", () => {
    expect(extractAgentId("www.realestatestar.com")).toBeNull();
  });

  it("should handle localhost with port for dev", () => {
    expect(extractAgentId("jenise-buckalew.localhost:3000")).toBe("jenise-buckalew");
  });

  it("should return null for plain localhost", () => {
    expect(extractAgentId("localhost:3000")).toBeNull();
  });
});
