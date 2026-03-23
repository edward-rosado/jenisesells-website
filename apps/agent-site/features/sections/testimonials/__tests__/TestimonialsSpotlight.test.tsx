// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, fireEvent, act } from "@testing-library/react";
import { TestimonialsSpotlight } from "@/features/sections/testimonials/TestimonialsSpotlight";
import type { TestimonialItem } from "@/features/config/types";

vi.mock("@/features/shared/useReducedMotion", () => ({
  useReducedMotion: vi.fn(() => false),
}));
import { useReducedMotion } from "@/features/shared/useReducedMotion";
const mockUseReducedMotion = useReducedMotion as unknown as ReturnType<typeof vi.fn>;

const ITEMS: TestimonialItem[] = [
  { text: "Amazing service!", reviewer: "Alice B.", rating: 5, source: "Zillow" },
  { text: "Would recommend.", reviewer: "Tom C.", rating: 4 },
  { text: "Smooth process.", reviewer: "Sara D.", rating: 3 },
];

describe("TestimonialsSpotlight", () => {
  beforeEach(() => { vi.useFakeTimers(); });
  afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks(); });

  it("renders nothing when items is empty", () => {
    const { container } = render(<TestimonialsSpotlight items={[]} />);
    expect(container.querySelector("section")).toBeNull();
  });

  it("renders single item without dots or arrows", () => {
    const { getByText, container } = render(<TestimonialsSpotlight items={[ITEMS[0]]} />);
    expect(getByText("Amazing service!")).toBeTruthy();
    expect(container.querySelectorAll("[role='tab']")).toHaveLength(0);
  });

  it("renders first testimonial by default", () => {
    const { getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    expect(getByText("Amazing service!")).toBeTruthy();
    expect(getByText("Alice B.")).toBeTruthy();
  });

  it("renders dot indicators for multiple items", () => {
    const { container } = render(<TestimonialsSpotlight items={ITEMS} />);
    const dots = container.querySelectorAll("[role='tab']");
    expect(dots).toHaveLength(3);
  });

  it("navigates to specific review when dot is clicked", () => {
    const { container, getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    const dots = container.querySelectorAll("[role='tab']");
    fireEvent.click(dots[1]);
    expect(getByText("Would recommend.")).toBeTruthy();
  });

  it("auto-rotates after 5 seconds", () => {
    const { getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    expect(getByText("Amazing service!")).toBeTruthy();
    act(() => { vi.advanceTimersByTime(5000); });
    expect(getByText("Would recommend.")).toBeTruthy();
  });

  it("does not auto-rotate when reduced motion is enabled", () => {
    mockUseReducedMotion.mockReturnValue(true);
    const { getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    act(() => { vi.advanceTimersByTime(15000); });
    expect(getByText("Amazing service!")).toBeTruthy(); // still on first
  });

  it("has aria-live='polite' on review container", () => {
    const { container } = render(<TestimonialsSpotlight items={ITEMS} />);
    expect(container.querySelector("[aria-live='polite']")).toBeTruthy();
  });

  it("renders FTC disclaimer", () => {
    const { getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    expect(getByText(/Real reviews from real clients/)).toBeTruthy();
  });

  it("renders star rating", () => {
    const { container } = render(<TestimonialsSpotlight items={ITEMS} />);
    // Should have 5 star elements (filled based on rating)
    const stars = container.querySelectorAll("[data-star]");
    expect(stars.length).toBeGreaterThanOrEqual(5);
  });

  it("renders title when provided", () => {
    const { getByText } = render(<TestimonialsSpotlight items={ITEMS} title="What Clients Say" />);
    expect(getByText("What Clients Say")).toBeTruthy();
  });

  it("renders source when available", () => {
    const { getAllByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    expect(getAllByText(/Zillow/).length).toBeGreaterThanOrEqual(1);
  });

  it("navigates with arrow keys on tablist", () => {
    const { container, getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    const tablist = container.querySelector("[role='tablist']") as HTMLElement;
    fireEvent.keyDown(tablist, { key: "ArrowRight" });
    expect(getByText("Would recommend.")).toBeTruthy();
    fireEvent.keyDown(tablist, { key: "ArrowLeft" });
    expect(getByText("Amazing service!")).toBeTruthy();
  });

  it("pauses auto-rotation on focus within section", () => {
    const { container, getByText } = render(<TestimonialsSpotlight items={ITEMS} />);
    const section = container.querySelector("section") as HTMLElement;
    fireEvent.focus(section);
    act(() => { vi.advanceTimersByTime(15000); });
    expect(getByText("Amazing service!")).toBeTruthy(); // still on first
  });

  it("stays paused on blur when relatedTarget is inside section", () => {
    mockUseReducedMotion.mockReturnValue(false);
    const { container, getByText } = render(<TestimonialsSpotlight items={ITEMS} />);

    // Auto-rotate to second item
    act(() => { vi.advanceTimersByTime(5000); });
    expect(getByText("Would recommend.")).toBeTruthy();

    const section = container.querySelector("section") as HTMLElement;
    const propsKey = Object.keys(section).find(k => k.startsWith("__reactProps"))!;
    const props = (section as unknown as Record<string, Record<string, (...args: unknown[]) => void>>)[propsKey];

    // Pause via onFocus
    act(() => { props.onFocus(); });
    act(() => { vi.advanceTimersByTime(10000); });
    expect(getByText("Would recommend.")).toBeTruthy(); // paused

    // Blur with relatedTarget INSIDE the section — should stay paused
    const innerTab = container.querySelector("[role='tab']") as HTMLElement;
    act(() => { props.onBlur({ relatedTarget: innerTab }); });
    act(() => { vi.advanceTimersByTime(10000); });
    expect(getByText("Would recommend.")).toBeTruthy(); // still paused
  });

  it("unpauses on blur when relatedTarget is outside section", () => {
    mockUseReducedMotion.mockReturnValue(false);
    const { container, getByText } = render(<TestimonialsSpotlight items={ITEMS} />);

    // Auto-rotate to second item
    act(() => { vi.advanceTimersByTime(5000); });
    expect(getByText("Would recommend.")).toBeTruthy();

    // Now access React internals to invoke focus/blur directly
    const section = container.querySelector("section") as HTMLElement;
    const propsKey = Object.keys(section).find(k => k.startsWith("__reactProps"))!;
    const props = (section as unknown as Record<string, Record<string, (...args: unknown[]) => void>>)[propsKey];

    // Pause via direct onFocus call
    act(() => { props.onFocus(); });
    act(() => { vi.advanceTimersByTime(10000); });
    expect(getByText("Would recommend.")).toBeTruthy(); // still paused

    // Unpause via onBlur with relatedTarget outside section
    const outsideEl = document.createElement("button");
    document.body.appendChild(outsideEl);
    act(() => { props.onBlur({ relatedTarget: outsideEl }); });
    act(() => { vi.advanceTimersByTime(5000); });
    expect(getByText("Smooth process.")).toBeTruthy();
    document.body.removeChild(outsideEl);
  });
});
