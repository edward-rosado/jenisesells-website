/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
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
});
