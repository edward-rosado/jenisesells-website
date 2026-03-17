/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { TestimonialsBeach } from "@/components/sections/testimonials/TestimonialsBeach";
import type { TestimonialItem } from "@/lib/types";

const ITEMS: TestimonialItem[] = [
  { text: "Maya found us the perfect beach house!", reviewer: "Sarah K.", rating: 5, source: "Zillow" },
  { text: "She knew the OBX market inside out.", reviewer: "Tom W.", rating: 5, source: "Google" },
  { text: "Smooth transaction from start to close.", reviewer: "Linda P.", rating: 4 },
];

describe("TestimonialsBeach", () => {
  it("renders the default heading 'What Our Clients Say'", () => {
    render(<TestimonialsBeach items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "What Our Clients Say" })).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<TestimonialsBeach items={ITEMS} title="Client Stories" />);
    expect(screen.getByRole("heading", { level: 2, name: "Client Stories" })).toBeInTheDocument();
  });

  it("renders all quote texts", () => {
    render(<TestimonialsBeach items={ITEMS} />);
    expect(screen.getByText("Maya found us the perfect beach house!")).toBeInTheDocument();
    expect(screen.getByText("She knew the OBX market inside out.")).toBeInTheDocument();
    expect(screen.getByText("Smooth transaction from start to close.")).toBeInTheDocument();
  });

  it("renders all reviewer names", () => {
    render(<TestimonialsBeach items={ITEMS} />);
    expect(screen.getByText("Sarah K.")).toBeInTheDocument();
    expect(screen.getByText("Tom W.")).toBeInTheDocument();
    expect(screen.getByText("Linda P.")).toBeInTheDocument();
  });

  it("renders star ratings", () => {
    render(<TestimonialsBeach items={ITEMS} />);
    const starGroups = screen.getAllByRole("img", { name: /stars/ });
    expect(starGroups.length).toBe(3);
  });

  it("has id=testimonials for anchor linking", () => {
    const { container } = render(<TestimonialsBeach items={ITEMS} />);
    expect(container.querySelector("#testimonials")).toBeInTheDocument();
  });

  it("renders the FTC disclaimer", () => {
    render(<TestimonialsBeach items={ITEMS} />);
    expect(screen.getByText(/Real reviews from real clients/)).toBeInTheDocument();
  });

  it("cards have sandy background (#fefcf8)", () => {
    const { container } = render(<TestimonialsBeach items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(3);
    expect((articles[0] as HTMLElement).style.background).toMatch(/#fefcf8|rgb\(254, 252, 248\)/i);
  });

  it("renders dashed horizontal separator between cards", () => {
    const { container } = render(<TestimonialsBeach items={ITEMS} />);
    const separator = container.querySelector("hr");
    expect(separator).toBeInTheDocument();
    expect((separator as HTMLElement).style.borderStyle).toMatch(/dashed/);
  });

  it("star ratings have teal color", () => {
    const { container } = render(<TestimonialsBeach items={ITEMS} />);
    const starEl = container.querySelector("[role='img']");
    expect((starEl as HTMLElement).style.color).toMatch(/var\(--color-primary|#2c7a7b/);
  });
});
