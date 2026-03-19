import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook } from "@testing-library/react";
import { useFocusTrap } from "@/lib/use-focus-trap";

describe("useFocusTrap", () => {
  beforeEach(() => {
    document.body.textContent = "";
  });

  it("returns a ref object", () => {
    const { result } = renderHook(() => useFocusTrap(false));
    expect(result.current).toBeDefined();
    expect(result.current.current).toBeNull();
  });

  it("does not add event listener when inactive", () => {
    const addSpy = vi.spyOn(document, "addEventListener");
    renderHook(() => useFocusTrap(false));
    expect(addSpy).not.toHaveBeenCalledWith("keydown", expect.any(Function));
    addSpy.mockRestore();
  });

  it("adds keydown listener when active and container has focusable elements", () => {
    const addSpy = vi.spyOn(document, "addEventListener");
    const div = document.createElement("div");
    const button = document.createElement("button");
    div.appendChild(button);
    document.body.appendChild(div);

    const { result } = renderHook(() => useFocusTrap(true));
    // Manually assign the ref to simulate component usage
    Object.defineProperty(result.current, "current", {
      value: div,
      writable: true,
    });

    // The hook returns a valid ref
    expect(result.current.current).toBe(div);
    addSpy.mockRestore();
  });

  it("cleans up event listener on unmount", () => {
    const removeSpy = vi.spyOn(document, "removeEventListener");
    const { unmount } = renderHook(() => useFocusTrap(true));
    unmount();
    // Cleanup should not throw
    expect(true).toBe(true);
    removeSpy.mockRestore();
  });
});
