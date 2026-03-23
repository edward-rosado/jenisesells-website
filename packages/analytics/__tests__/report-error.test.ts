import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { reportError, setErrorReporter } from "../src/report-error";
import type { ErrorReporter } from "../src/report-error";

describe("reportError", () => {
  let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
    // Reset reporter back to the default (console.error) after each test
    setErrorReporter((error) => console.error(error));
  });

  it("uses console.error as the default reporter", () => {
    const error = new Error("something went wrong");
    reportError(error);

    expect(consoleErrorSpy).toHaveBeenCalledOnce();
    expect(consoleErrorSpy).toHaveBeenCalledWith(error);
  });

  it("default reporter ignores optional context (does not forward it to console.error)", () => {
    reportError(new Error("oops"), { page: "home" });

    // The default reporter only passes the error, not the context
    expect(consoleErrorSpy).toHaveBeenCalledOnce();
  });

  it("uses the injected reporter after setErrorReporter is called", () => {
    const customReporter: ErrorReporter = vi.fn();
    setErrorReporter(customReporter);

    const error = new Error("custom reporter error");
    reportError(error, { source: "form" });

    expect(customReporter).toHaveBeenCalledOnce();
    expect(customReporter).toHaveBeenCalledWith(error, { source: "form" });
    expect(consoleErrorSpy).not.toHaveBeenCalled();
  });

  it("forwards context to the injected reporter", () => {
    const captured: { error: unknown; context?: Record<string, string> }[] = [];
    setErrorReporter((error, context) => {
      captured.push({ error, context });
    });

    reportError("string error", { component: "LeadForm", step: "submit" });

    expect(captured).toHaveLength(1);
    expect(captured[0].error).toBe("string error");
    expect(captured[0].context).toEqual({ component: "LeadForm", step: "submit" });
  });

  it("works with non-Error values (strings, objects)", () => {
    const customReporter: ErrorReporter = vi.fn();
    setErrorReporter(customReporter);

    reportError("plain string error");
    reportError({ code: 404, message: "not found" });

    expect(customReporter).toHaveBeenCalledTimes(2);
    expect(customReporter).toHaveBeenNthCalledWith(1, "plain string error", undefined);
    expect(customReporter).toHaveBeenNthCalledWith(
      2,
      { code: 404, message: "not found" },
      undefined
    );
  });

  it("supports calling reportError without a context argument", () => {
    const customReporter: ErrorReporter = vi.fn();
    setErrorReporter(customReporter);

    reportError(new Error("no context"));

    expect(customReporter).toHaveBeenCalledWith(new Error("no context"), undefined);
  });
});
