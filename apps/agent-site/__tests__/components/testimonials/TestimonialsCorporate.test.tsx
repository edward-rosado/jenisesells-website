/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { TestimonialsCorporate } from "@/components/sections/testimonials/TestimonialsCorporate";
import type { TestimonialItem } from "@/lib/types";

const ITEMS: TestimonialItem[] = [
  {
    text: "Robert negotiated an exceptional deal on our portfolio acquisition.",
    reviewer: "John Smith, CEO",
    rating: 5,
    source: "Acme Corp",
  },
  {
    text: "Unparalleled market knowledge and transaction expertise.",
    reviewer: "Lisa Chen, CFO",
    rating: 5,
    source: "Nexus Holdings",
  },
  {
    text: "Delivered results on a complex mixed-use deal.",
    reviewer: "Mark Davis, Managing Director",
    rating: 4,
  },
];

describe("TestimonialsCorporate", () => {
  it("renders the default section heading", () => {
    render(<TestimonialsCorporate items={ITEMS} />);
    expect(
      screen.getByRole("heading", { level: 2, name: "Client Testimonials" })
    ).toBeInTheDocument();
  });

  it("renders a custom heading when title is provided", () => {
    render(<TestimonialsCorporate items={ITEMS} title="What Our Clients Say" />);
    expect(
      screen.getByRole("heading", { level: 2, name: "What Our Clients Say" })
    ).toBeInTheDocument();
  });

  it("renders all quote texts", () => {
    render(<TestimonialsCorporate items={ITEMS} />);
    expect(
      screen.getByText("Robert negotiated an exceptional deal on our portfolio acquisition.")
    ).toBeInTheDocument();
    expect(
      screen.getByText("Unparalleled market knowledge and transaction expertise.")
    ).toBeInTheDocument();
    expect(
      screen.getByText("Delivered results on a complex mixed-use deal.")
    ).toBeInTheDocument();
  });

  it("renders reviewer names", () => {
    render(<TestimonialsCorporate items={ITEMS} />);
    expect(screen.getByText(/John Smith, CEO/)).toBeInTheDocument();
    expect(screen.getByText(/Lisa Chen, CFO/)).toBeInTheDocument();
    expect(screen.getByText(/Mark Davis, Managing Director/)).toBeInTheDocument();
  });

  it("does NOT render star ratings", () => {
    render(<TestimonialsCorporate items={ITEMS} />);
    expect(screen.queryByText(/★/)).not.toBeInTheDocument();
    expect(screen.queryByText(/☆/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/out of 5 stars/)).not.toBeInTheDocument();
  });

  it("renders source attribution alongside reviewer when source is provided", () => {
    render(<TestimonialsCorporate items={ITEMS} />);
    // Reviewer and source are in adjacent elements; verify both are present near each other
    expect(screen.getByText(/John Smith, CEO/)).toBeInTheDocument();
    expect(screen.getByText(/Acme Corp/)).toBeInTheDocument();
    expect(screen.getByText(/Lisa Chen, CFO/)).toBeInTheDocument();
    expect(screen.getByText(/Nexus Holdings/)).toBeInTheDocument();
  });

  it("renders reviewer without source when source is not provided", () => {
    render(<TestimonialsCorporate items={ITEMS} />);
    expect(screen.getByText(/Mark Davis, Managing Director/)).toBeInTheDocument();
  });

  it("uses id=testimonials for anchor linking", () => {
    const { container } = render(<TestimonialsCorporate items={ITEMS} />);
    expect(container.querySelector("#testimonials")).toBeInTheDocument();
  });

  it("includes FTC disclaimer text", () => {
    render(<TestimonialsCorporate items={ITEMS} />);
    expect(screen.getByText(/No compensation was provided/)).toBeInTheDocument();
  });

  it("renders clean bordered cards (no star rating area)", () => {
    const { container } = render(<TestimonialsCorporate items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(3);
    articles.forEach((article) => {
      expect((article as HTMLElement).style.border).toContain("1px solid");
    });
  });
});
