// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useParallax } from "@/hooks/useParallax";
import { useRef } from "react";

let rafCallback: FrameRequestCallback | null = null;

beforeEach(() => {
  rafCallback = null;
  vi.stubGlobal("requestAnimationFrame", vi.fn((cb: FrameRequestCallback) => { rafCallback = cb; return 1; }));
  vi.stubGlobal("cancelAnimationFrame", vi.fn());
  vi.stubGlobal("matchMedia", vi.fn((query: string) => ({
    matches: false,
    media: query,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
  })));
});

afterEach(() => { vi.restoreAllMocks(); });

function renderParallax(options?: Parameters<typeof useParallax>[2]) {
  const container = document.createElement("div");
  const bg = document.createElement("div");
  container.getBoundingClientRect = () => ({
    top: 0, bottom: 600, left: 0, right: 800, width: 800, height: 600, x: 0, y: 0, toJSON: () => "",
  });
  Object.defineProperty(window, "innerHeight", { value: 800, writable: true });

  return renderHook(() => {
    const containerRef = useRef<HTMLDivElement>(container);
    const bgRef = useRef<HTMLDivElement>(bg);
    useParallax(containerRef, bgRef, options);
    return { container, bg };
  });
}

describe("useParallax", () => {
  it("attaches scroll listener on mount", () => {
    const addSpy = vi.spyOn(window, "addEventListener");
    renderParallax();
    expect(addSpy).toHaveBeenCalledWith("scroll", expect.any(Function), expect.anything());
  });

  it("removes scroll listener on unmount", () => {
    const removeSpy = vi.spyOn(window, "removeEventListener");
    const { unmount } = renderParallax();
    unmount();
    expect(removeSpy).toHaveBeenCalledWith("scroll", expect.any(Function));
  });

  it("applies transform to background element on scroll", () => {
    const { result } = renderParallax();
    window.dispatchEvent(new Event("scroll"));
    if (rafCallback) act(() => { rafCallback!(0); });
    expect(result.current.bg.style.transform).toBeTruthy();
  });

  it("does not attach listener when reduced motion is enabled", () => {
    vi.stubGlobal("matchMedia", vi.fn((query: string) => ({
      matches: query === "(prefers-reduced-motion: reduce)",
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })));
    const addSpy = vi.spyOn(window, "addEventListener");
    renderParallax();
    const scrollCalls = addSpy.mock.calls.filter(([event]) => event === "scroll");
    expect(scrollCalls).toHaveLength(0);
  });
});
