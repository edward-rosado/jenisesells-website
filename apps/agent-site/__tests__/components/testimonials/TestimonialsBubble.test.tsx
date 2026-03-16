/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { TestimonialsBubble } from "@/components/sections/testimonials/TestimonialsBubble";
import type { TestimonialItem } from "@/lib/types";

const ITEMS: TestimonialItem[] = [
  { reviewer: "Alice M.", text: "Wonderful experience!", rating: 5, source: "Zillow" },
  { reviewer: "Bob K.", text: "Very professional.", rating: 4, source: "Google" },
];

describe("TestimonialsBubble", () => {
  it("renders the default title", () => {
    render(<TestimonialsBubble items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2, name: "What My Clients Say" })).toBeInTheDocument();
  });

  it("renders a custom title", () => {
    render(<TestimonialsBubble items={ITEMS} title="Kind Words" />);
    expect(screen.getByRole("heading", { level: 2, name: "Kind Words" })).toBeInTheDocument();
  });

  it("uses id=testimonials for anchor linking", () => {
    const { container } = render(<TestimonialsBubble items={ITEMS} />);
    expect(container.querySelector("#testimonials")).toBeInTheDocument();
  });

  it("renders all testimonial texts", () => {
    render(<TestimonialsBubble items={ITEMS} />);
    expect(screen.getByText("Wonderful experience!")).toBeInTheDocument();
    expect(screen.getByText("Very professional.")).toBeInTheDocument();
  });

  it("renders reviewer names", () => {
    render(<TestimonialsBubble items={ITEMS} />);
    expect(screen.getByText(/Alice M\./)).toBeInTheDocument();
    expect(screen.getByText(/Bob K\./)).toBeInTheDocument();
  });

  it("renders star ratings with aria labels", () => {
    render(<TestimonialsBubble items={ITEMS} />);
    expect(screen.getByLabelText("5 out of 5 stars")).toBeInTheDocument();
    expect(screen.getByLabelText("4 out of 5 stars")).toBeInTheDocument();
  });

  it("renders source attribution", () => {
    render(<TestimonialsBubble items={ITEMS} />);
    expect(screen.getByText(/via Zillow/)).toBeInTheDocument();
    expect(screen.getByText(/via Google/)).toBeInTheDocument();
  });

  it("includes FTC disclaimer text", () => {
    render(<TestimonialsBubble items={ITEMS} />);
    expect(screen.getByText(/No compensation was provided/)).toBeInTheDocument();
  });

  it("uses warm background", () => {
    const { container } = render(<TestimonialsBubble items={ITEMS} />);
    const section = container.querySelector("#testimonials") as HTMLElement;
    expect(section?.style.background).toBe("rgb(255, 248, 240)");
  });

  it("renders avatar circles for reviewers", () => {
    const { container } = render(<TestimonialsBubble items={ITEMS} />);
    expect(screen.getByText("A")).toBeInTheDocument();
    expect(screen.getByText("B")).toBeInTheDocument();
  });
});
