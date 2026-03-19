// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useScrollReveal } from "@/hooks/useScrollReveal";
import { useRef } from "react";

// Mock IntersectionObserver
let observerCallback: IntersectionObserverCallback;
let observerOptions: IntersectionObserverInit | undefined;
let disconnectSpy: ReturnType<typeof vi.fn>;

beforeEach(() => {
  disconnectSpy = vi.fn();
  vi.stubGlobal("IntersectionObserver", class {
    constructor(cb: IntersectionObserverCallback, opts?: IntersectionObserverInit) {
      observerCallback = cb;
      observerOptions = opts;
    }
    observe = vi.fn();
    unobserve = vi.fn();
    disconnect = disconnectSpy;
  });
  vi.stubGlobal("matchMedia", vi.fn().mockImplementation(function (query: string) {
    return {
      matches: false,
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    };
  }));
});

afterEach(() => { vi.restoreAllMocks(); });

// Helper: render hook with a real ref attached to a div
function renderScrollReveal(options?: Parameters<typeof useScrollReveal>[1]) {
  const div = document.createElement("div");
  return renderHook(() => {
    const ref = useRef<HTMLDivElement>(div);
    const isVisible = useScrollReveal(ref, options);
    return { isVisible, ref };
  });
}

describe("useScrollReveal", () => {
  it("returns false initially", () => {
    const { result } = renderScrollReveal();
    expect(result.current.isVisible).toBe(false);
  });

  it("returns true after element intersects", () => {
    const { result } = renderScrollReveal();
    act(() => {
      observerCallback(
        [{ isIntersecting: true }] as IntersectionObserverEntry[],
        {} as IntersectionObserver,
      );
    });
    expect(result.current.isVisible).toBe(true);
  });

  it("uses default threshold of 0.15", () => {
    renderScrollReveal();
    expect(observerOptions?.threshold).toBe(0.15);
  });

  it("accepts custom threshold", () => {
    renderScrollReveal({ threshold: 0.5 });
    expect(observerOptions?.threshold).toBe(0.5);
  });

  it("disconnects observer after first trigger when once=true (default)", () => {
    renderScrollReveal();
    act(() => {
      observerCallback(
        [{ isIntersecting: true }] as IntersectionObserverEntry[],
        {} as IntersectionObserver,
      );
    });
    expect(disconnectSpy).toHaveBeenCalled();
  });

  it("does not disconnect when once=false", () => {
    renderScrollReveal({ once: false });
    act(() => {
      observerCallback(
        [{ isIntersecting: true }] as IntersectionObserverEntry[],
        {} as IntersectionObserver,
      );
    });
    expect(disconnectSpy).not.toHaveBeenCalled();
  });

  it("disconnects on unmount", () => {
    const { unmount } = renderScrollReveal();
    unmount();
    expect(disconnectSpy).toHaveBeenCalled();
  });
});

describe("useScrollReveal with reduced motion", () => {
  beforeEach(() => {
    vi.stubGlobal("matchMedia", vi.fn().mockImplementation(function (query: string) {
      return {
        matches: query === "(prefers-reduced-motion: reduce)",
        media: query,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      };
    }));
  });

  it("returns true immediately when reduced motion is enabled", () => {
    const { result } = renderScrollReveal();
    expect(result.current.isVisible).toBe(true);
  });
});
