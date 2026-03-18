/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { renderHook } from "@testing-library/react";
import { useFocusTrap } from "../../lib/use-focus-trap";

describe("useFocusTrap", () => {
  it("returns a ref", () => {
    const { result } = renderHook(() => useFocusTrap(true));
    expect(result.current).toBeDefined();
    expect(result.current.current).toBeNull();
  });

  it("does not trap when disabled", () => {
    const container = document.createElement("div");
    const button = document.createElement("button");
    container.appendChild(button);
    document.body.appendChild(container);

    const { result } = renderHook(() => useFocusTrap(false));
    expect(result.current).toBeDefined();
    document.body.removeChild(container);
  });
});
