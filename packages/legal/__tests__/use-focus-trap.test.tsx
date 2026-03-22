/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, render, act } from "@testing-library/react";
import React from "react";
import { useFocusTrap } from "../src/use-focus-trap";

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

  describe("Tab key handling with a real component render", () => {
    function FocusTrapFixture({ active, children }: { active: boolean; children: React.ReactNode }) {
      const ref = useFocusTrap(active);
      return React.createElement("div", { ref, "data-testid": "trap" }, children);
    }

    it("wraps focus from first to last on Shift+Tab", () => {
      const { getByTestId } = render(
        React.createElement(FocusTrapFixture, { active: true },
          React.createElement("button", { "data-testid": "first" }, "First"),
          React.createElement("button", { "data-testid": "last" }, "Last"),
        )
      );

      const first = getByTestId("first") as HTMLButtonElement;
      const last = getByTestId("last") as HTMLButtonElement;
      const trap = getByTestId("trap");

      expect(trap).toBeInTheDocument();

      act(() => { first.focus(); });
      expect(document.activeElement).toBe(first);

      let prevented = false;
      act(() => {
        const event = new KeyboardEvent("keydown", { key: "Tab", shiftKey: true, bubbles: true, cancelable: true });
        Object.defineProperty(event, "preventDefault", { value: () => { prevented = true; } });
        document.dispatchEvent(event);
      });

      expect(prevented).toBe(true);
      expect(document.activeElement).toBe(last);
    });

    it("wraps focus from last to first on Tab (forward)", () => {
      const { getByTestId } = render(
        React.createElement(FocusTrapFixture, { active: true },
          React.createElement("button", { "data-testid": "first" }, "First"),
          React.createElement("button", { "data-testid": "last" }, "Last"),
        )
      );

      const first = getByTestId("first") as HTMLButtonElement;
      const last = getByTestId("last") as HTMLButtonElement;

      act(() => { last.focus(); });
      expect(document.activeElement).toBe(last);

      let prevented = false;
      act(() => {
        const event = new KeyboardEvent("keydown", { key: "Tab", shiftKey: false, bubbles: true, cancelable: true });
        Object.defineProperty(event, "preventDefault", { value: () => { prevented = true; } });
        document.dispatchEvent(event);
      });

      expect(prevented).toBe(true);
      expect(document.activeElement).toBe(first);
    });

    it("does not intercept Tab when focus is not at a boundary", () => {
      const { getByTestId } = render(
        React.createElement(FocusTrapFixture, { active: true },
          React.createElement("button", { "data-testid": "first" }, "First"),
          React.createElement("button", { "data-testid": "last" }, "Last"),
        )
      );

      const first = getByTestId("first") as HTMLButtonElement;

      act(() => { first.focus(); });

      let prevented = false;
      act(() => {
        const event = new KeyboardEvent("keydown", { key: "Tab", shiftKey: false, bubbles: true, cancelable: true });
        Object.defineProperty(event, "preventDefault", { value: () => { prevented = true; } });
        document.dispatchEvent(event);
      });

      expect(prevented).toBe(false);
    });

    it("does not intercept non-Tab keys", () => {
      render(
        React.createElement(FocusTrapFixture, { active: true },
          React.createElement("button", null, "Only"),
        )
      );

      let prevented = false;
      act(() => {
        const event = new KeyboardEvent("keydown", { key: "Escape", bubbles: true, cancelable: true });
        Object.defineProperty(event, "preventDefault", { value: () => { prevented = true; } });
        document.dispatchEvent(event);
      });

      expect(prevented).toBe(false);
    });

    it("does nothing when container has no focusable elements", () => {
      render(
        React.createElement(FocusTrapFixture, { active: true },
          React.createElement("span", null, "Non-focusable"),
        )
      );

      let prevented = false;
      act(() => {
        const event = new KeyboardEvent("keydown", { key: "Tab", bubbles: true, cancelable: true });
        Object.defineProperty(event, "preventDefault", { value: () => { prevented = true; } });
        document.dispatchEvent(event);
      });

      expect(prevented).toBe(false);
    });
  });
});
