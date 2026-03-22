// apps/agent-site/__tests__/components/testimonials/TestimonialsClean.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { TestimonialsClean } from "@/components/sections/testimonials/TestimonialsClean";
import type { TestimonialItem } from "@/features/config/types";

const ITEMS: TestimonialItem[] = [
  { reviewer: "Alice M.", text: "Wonderful experience!", rating: 5, source: "Zillow" },
  { reviewer: "Bob K.", text: "Very professional.", rating: 4, source: "Google" },
];

describe("TestimonialsClean", () => {
  it("renders the default title", () => {
    render(<TestimonialsClean items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "What My Clients Say" })).toBeInTheDocument();
  });

  it("renders a custom title", () => {
    render(<TestimonialsClean items={ITEMS} title="Client Stories" />);
    expect(screen.getByRole("heading", { level: 2, name: "Client Stories" })).toBeInTheDocument();
  });

  it("uses id=testimonials for anchor linking", () => {
    const { container } = render(<TestimonialsClean items={ITEMS} />);
    expect(container.querySelector("#testimonials")).toBeInTheDocument();
  });

  it("renders all testimonial items", () => {
    render(<TestimonialsClean items={ITEMS} />);
    expect(screen.getByText("Wonderful experience!")).toBeInTheDocument();
    expect(screen.getByText("Very professional.")).toBeInTheDocument();
  });

  it("renders reviewer names", () => {
    render(<TestimonialsClean items={ITEMS} />);
    expect(screen.getByText(/Alice M\./)).toBeInTheDocument();
    expect(screen.getByText(/Bob K\./)).toBeInTheDocument();
  });

  it("renders star ratings with aria labels", () => {
    render(<TestimonialsClean items={ITEMS} />);
    expect(screen.getByLabelText("5 out of 5 stars")).toBeInTheDocument();
    expect(screen.getByLabelText("4 out of 5 stars")).toBeInTheDocument();
  });

  it("renders source attribution", () => {
    render(<TestimonialsClean items={ITEMS} />);
    expect(screen.getByText(/via Zillow/)).toBeInTheDocument();
    expect(screen.getByText(/via Google/)).toBeInTheDocument();
  });

  it("includes FTC disclaimer text", () => {
    render(<TestimonialsClean items={ITEMS} />);
    expect(screen.getByText(/No compensation was provided/)).toBeInTheDocument();
  });

  it("applies hover lift on mouse enter", () => {
    const { container } = render(<TestimonialsClean items={ITEMS} />);
    const card = container.querySelectorAll("article")[0] as HTMLElement;
    fireEvent.mouseEnter(card);
    expect(card.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(card);
    expect(card.style.transform).toBe("none");
  });

  it("uses clean minimal styling — white background cards with thin borders", () => {
    const { container } = render(<TestimonialsClean items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(2);
    expect(articles[0].style.border).toContain("1px solid");
    expect(articles[0].style.background).toBe("white");
  });
});
