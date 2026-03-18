/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import * as React from "react";
import { render, screen, fireEvent, act } from "@testing-library/react";
import { SoldCarousel } from "@/components/sections/sold/SoldCarousel";
import type { SoldHomeItem } from "@/lib/types";

// Mock matchMedia (not available in jsdom)
function mockMatchMedia(matches: boolean) {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
}

const ITEMS: SoldHomeItem[] = [
  { address: "432 Park Ave", city: "New York", state: "NY", price: "$12,000,000", image_url: "/sold/432-park-ave.jpg" },
  { address: "15 Central Park West", city: "New York", state: "NY", price: "$8,500,000", image_url: "/sold/15-central-park.jpg" },
  { address: "56 Leonard St", city: "New York", state: "NY", price: "$4,200,000" },
];

describe("SoldCarousel", () => {
  beforeEach(() => {
    mockMatchMedia(false); // not reduced motion by default
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("renders section with id=sold", () => {
    const { container } = render(<SoldCarousel items={ITEMS} />);
    expect(container.querySelector("section#sold")).toBeInTheDocument();
  });

  it("renders with role=region and aria-roledescription=carousel", () => {
    const { container } = render(<SoldCarousel items={ITEMS} />);
    const region = container.querySelector("[role='region']");
    expect(region).toBeInTheDocument();
    expect(region!.getAttribute("aria-roledescription")).toBe("carousel");
  });

  it("renders default heading 'Portfolio' when title is not provided", () => {
    render(<SoldCarousel items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "Portfolio" })).toBeInTheDocument();
  });

  it("renders custom heading when title is provided", () => {
    render(<SoldCarousel items={ITEMS} title="Recent Sales" />);
    expect(screen.getByRole("heading", { level: 2, name: "Recent Sales" })).toBeInTheDocument();
  });

  it("renders each slide as role=group with aria-roledescription=slide", () => {
    const { container } = render(<SoldCarousel items={ITEMS} />);
    const slides = container.querySelectorAll("[role='group']");
    expect(slides.length).toBe(3);
    slides.forEach((slide) => {
      expect(slide.getAttribute("aria-roledescription")).toBe("slide");
    });
  });

  it("renders all property prices", () => {
    render(<SoldCarousel items={ITEMS} />);
    expect(screen.getByText("$12,000,000")).toBeInTheDocument();
    expect(screen.getByText("$8,500,000")).toBeInTheDocument();
    expect(screen.getByText("$4,200,000")).toBeInTheDocument();
  });

  it("renders all property addresses", () => {
    render(<SoldCarousel items={ITEMS} />);
    expect(screen.getByText(/432 Park Ave/)).toBeInTheDocument();
    expect(screen.getByText(/15 Central Park West/)).toBeInTheDocument();
    expect(screen.getByText(/56 Leonard St/)).toBeInTheDocument();
  });

  it("renders dot indicators (role=tab) for each item", () => {
    render(<SoldCarousel items={ITEMS} />);
    const dots = screen.getAllByRole("tab");
    expect(dots).toHaveLength(3);
  });

  it("first dot is aria-selected=true initially", () => {
    render(<SoldCarousel items={ITEMS} />);
    const dots = screen.getAllByRole("tab");
    expect(dots[0]).toHaveAttribute("aria-selected", "true");
    expect(dots[1]).toHaveAttribute("aria-selected", "false");
    expect(dots[2]).toHaveAttribute("aria-selected", "false");
  });

  it("renders prev and next navigation buttons", () => {
    render(<SoldCarousel items={ITEMS} />);
    expect(screen.getByLabelText("Previous slide")).toBeInTheDocument();
    expect(screen.getByLabelText("Next slide")).toBeInTheDocument();
  });

  it("clicking Next advances to next slide", () => {
    render(<SoldCarousel items={ITEMS} />);
    const nextBtn = screen.getByLabelText("Next slide");
    fireEvent.click(nextBtn);
    const dots = screen.getAllByRole("tab");
    expect(dots[1]).toHaveAttribute("aria-selected", "true");
    expect(dots[0]).toHaveAttribute("aria-selected", "false");
  });

  it("clicking Prev at first slide wraps to last", () => {
    render(<SoldCarousel items={ITEMS} />);
    const prevBtn = screen.getByLabelText("Previous slide");
    fireEvent.click(prevBtn);
    const dots = screen.getAllByRole("tab");
    expect(dots[2]).toHaveAttribute("aria-selected", "true");
  });

  it("clicking a dot navigates to that slide", () => {
    render(<SoldCarousel items={ITEMS} />);
    const dots = screen.getAllByRole("tab");
    fireEvent.click(dots[2]);
    expect(dots[2]).toHaveAttribute("aria-selected", "true");
    expect(dots[0]).toHaveAttribute("aria-selected", "false");
  });

  it("ArrowRight keyboard event advances slide", () => {
    const { container } = render(<SoldCarousel items={ITEMS} />);
    const region = container.querySelector("[role='region']")!;
    fireEvent.keyDown(region, { key: "ArrowRight" });
    const dots = screen.getAllByRole("tab");
    expect(dots[1]).toHaveAttribute("aria-selected", "true");
  });

  it("ArrowLeft keyboard event goes to previous slide", () => {
    const { container } = render(<SoldCarousel items={ITEMS} />);
    const region = container.querySelector("[role='region']")!;
    // Advance first, then go back
    fireEvent.keyDown(region, { key: "ArrowRight" });
    fireEvent.keyDown(region, { key: "ArrowLeft" });
    const dots = screen.getAllByRole("tab");
    expect(dots[0]).toHaveAttribute("aria-selected", "true");
  });

  it("auto-advances after 5 seconds", () => {
    render(<SoldCarousel items={ITEMS} />);
    act(() => { vi.advanceTimersByTime(5000); });
    const dots = screen.getAllByRole("tab");
    expect(dots[1]).toHaveAttribute("aria-selected", "true");
  });

  it("wraps from last to first on Next click", () => {
    render(<SoldCarousel items={ITEMS} />);
    const nextBtn = screen.getByLabelText("Next slide");
    fireEvent.click(nextBtn);
    fireEvent.click(nextBtn);
    fireEvent.click(nextBtn);
    const dots = screen.getAllByRole("tab");
    expect(dots[0]).toHaveAttribute("aria-selected", "true");
  });

  it("renders image for items with image_url", () => {
    render(<SoldCarousel items={ITEMS} />);
    const images = screen.getAllByRole("img");
    expect(images.length).toBeGreaterThanOrEqual(2);
  });

  it("renders SOLD badge for each item", () => {
    render(<SoldCarousel items={ITEMS} />);
    const badges = screen.getAllByText("SOLD");
    expect(badges).toHaveLength(3);
  });

  it("renders subtitle when provided", () => {
    render(<SoldCarousel items={ITEMS} subtitle="Our finest closings" />);
    expect(screen.getByText("Our finest closings")).toBeInTheDocument();
  });

  it("pauses auto-advance on mouseEnter and resumes on mouseLeave", () => {
    render(<SoldCarousel items={ITEMS} />);
    const region = screen.getByRole("region");
    fireEvent.mouseEnter(region);
    act(() => { vi.advanceTimersByTime(5000); });
    // While paused, should still be on slide 0
    let dots = screen.getAllByRole("tab");
    expect(dots[0]).toHaveAttribute("aria-selected", "true");
    fireEvent.mouseLeave(region);
    act(() => { vi.advanceTimersByTime(5000); });
    // After resuming, should advance
    dots = screen.getAllByRole("tab");
    expect(dots[1]).toHaveAttribute("aria-selected", "true");
  });

  it("pauses auto-advance on focus and resumes on blur", () => {
    render(<SoldCarousel items={ITEMS} />);
    const region = screen.getByRole("region");
    fireEvent.focus(region);
    act(() => { vi.advanceTimersByTime(5000); });
    // While paused, should still be on slide 0
    let dots = screen.getAllByRole("tab");
    expect(dots[0]).toHaveAttribute("aria-selected", "true");
    fireEvent.blur(region);
    act(() => { vi.advanceTimersByTime(5000); });
    // After resuming, should advance
    dots = screen.getAllByRole("tab");
    expect(dots[1]).toHaveAttribute("aria-selected", "true");
  });

  it("renders reduced-motion vertical stack when prefers-reduced-motion is set", () => {
    mockMatchMedia(true);
    const { container } = render(<SoldCarousel items={ITEMS} title="Our Sales" subtitle="Top closings" />);
    // Should NOT render carousel region
    expect(container.querySelector("[role='region']")).not.toBeInTheDocument();
    // Should render all items as articles in a vertical stack
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(3);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Our Sales");
    expect(screen.getByText("Top closings")).toBeInTheDocument();
  });

  it("reduced-motion stack renders without subtitle when subtitle is absent", () => {
    mockMatchMedia(true);
    render(<SoldCarousel items={ITEMS} title="Portfolio" />);
    const heading = screen.getByRole("heading", { level: 2 });
    expect(heading).toHaveTextContent("Portfolio");
    // Subtitle element should not be present
    expect(screen.queryByText("Top closings")).not.toBeInTheDocument();
  });

  it("reduced-motion stack renders default title 'Portfolio' when title is absent", () => {
    mockMatchMedia(true);
    render(<SoldCarousel items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Portfolio");
  });

  it("renders item info panel as absolute overlay when image_url is present", () => {
    const itemsWithImg: SoldHomeItem[] = [
      { address: "100 Park Ave", city: "New York", state: "NY", price: "$5,000,000", image_url: "/sold/100-park.jpg" },
    ];
    const { container } = render(<SoldCarousel items={itemsWithImg} />);
    // The info div should be positioned absolutely over the image
    const infoDivs = container.querySelectorAll("[aria-label='Sold for $5,000,000']");
    expect(infoDivs.length).toBeGreaterThanOrEqual(1);
    // Find the slide's positioning wrapper
    const slides = container.querySelectorAll("[role='group']");
    const slideEl = slides[0] as HTMLElement;
    // The inner content div should have position:absolute when image_url is present
    const innerDiv = slideEl.querySelector("div > div:last-child") as HTMLElement;
    expect(innerDiv.style.position).toBe("absolute");
  });

  it("renders item info panel as relative block when image_url is absent", () => {
    const itemsNoImg: SoldHomeItem[] = [
      { address: "200 Elm St", city: "Newark", state: "NJ", price: "$300,000" },
    ];
    const { container } = render(<SoldCarousel items={itemsNoImg} />);
    const slides = container.querySelectorAll("[role='group']");
    const slideEl = slides[0] as HTMLElement;
    // The inner content div should have position:relative when no image_url
    const innerDiv = slideEl.querySelector("div > div:last-child") as HTMLElement;
    expect(innerDiv.style.position).toBe("relative");
  });

  it("unsubscribes matchMedia listener on unmount", () => {
    const removeEventListenerSpy = vi.fn();
    const addEventListenerSpy = vi.fn();
    Object.defineProperty(window, "matchMedia", {
      writable: true,
      value: vi.fn().mockImplementation((query: string) => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: addEventListenerSpy,
        removeEventListener: removeEventListenerSpy,
        dispatchEvent: vi.fn(),
      })),
    });
    const { unmount } = render(<SoldCarousel items={ITEMS} />);
    unmount();
    expect(removeEventListenerSpy).toHaveBeenCalledWith("change", expect.any(Function));
  });

  it("does not auto-advance when items.length is 1", () => {
    const singleItem: SoldHomeItem[] = [
      { address: "100 Park Ave", city: "New York", state: "NY", price: "$5,000,000" },
    ];
    render(<SoldCarousel items={singleItem} />);
    act(() => { vi.advanceTimersByTime(10000); });
    // Only one dot, always selected
    const dots = screen.getAllByRole("tab");
    expect(dots).toHaveLength(1);
    expect(dots[0]).toHaveAttribute("aria-selected", "true");
  });

  it("clears interval on unmount", () => {
    const clearIntervalSpy = vi.spyOn(globalThis, "clearInterval");
    const { unmount } = render(<SoldCarousel items={ITEMS} />);
    act(() => { vi.advanceTimersByTime(100); });
    unmount();
    expect(clearIntervalSpy).toHaveBeenCalled();
    clearIntervalSpy.mockRestore();
  });

  it("server snapshot returns false (useSyncExternalStore third argument)", () => {
    // renderToString exercises the server snapshot (third arg to useSyncExternalStore).
    // The server snapshot `() => false` means reducedMotion=false on SSR, so the
    // carousel markup (not the reduced-motion stack) should be rendered.
    const { renderToString } = require("react-dom/server");
    const html = renderToString(<SoldCarousel items={ITEMS} />);
    // Carousel region should be present (server snapshot returned false = no reduced motion)
    expect(html).toContain("aria-roledescription=\"carousel\"");
  });
});
