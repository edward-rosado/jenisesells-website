import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("@grafana/faro-web-sdk", () => ({
  initializeFaro: vi.fn(),
}));
vi.mock("@grafana/faro-web-tracing", () => ({
  TracingInstrumentation: vi.fn(),
}));

describe("initFaro", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
    delete process.env.NEXT_PUBLIC_FARO_COLLECTOR_URL;
  });

  it("initializes Faro when FARO_COLLECTOR_URL is set", async () => {
    process.env.NEXT_PUBLIC_FARO_COLLECTOR_URL = "https://faro.example.com/collect";
    const { initFaro } = await import("@/lib/faro");
    const { initializeFaro } = await import("@grafana/faro-web-sdk");
    initFaro();
    expect(initializeFaro).toHaveBeenCalledWith(
      expect.objectContaining({ url: "https://faro.example.com/collect" })
    );
  });

  it("does nothing when FARO_COLLECTOR_URL is not set", async () => {
    const { initFaro } = await import("@/lib/faro");
    const { initializeFaro } = await import("@grafana/faro-web-sdk");
    initFaro();
    expect(initializeFaro).not.toHaveBeenCalled();
  });

  it("does not throw when FARO_COLLECTOR_URL is empty string", async () => {
    process.env.NEXT_PUBLIC_FARO_COLLECTOR_URL = "";
    const { initFaro } = await import("@/lib/faro");
    expect(() => initFaro()).not.toThrow();
  });
});
