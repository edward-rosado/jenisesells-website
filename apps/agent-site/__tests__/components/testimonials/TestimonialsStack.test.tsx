/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { TestimonialsStack } from "@/components/sections/testimonials/TestimonialsStack";
import { FTC_DISCLAIMER } from "@/components/sections/types";
import type { TestimonialItem } from "@/lib/types";

const ITEMS: TestimonialItem[] = [
  { text: "Kai found us the perfect loft in two weeks!", reviewer: "Mia Tanaka", rating: 5, source: "Zillow" },
  { text: "Super easy process, no BS.", reviewer: "Derek Osei", rating: 5 },
  { text: "Negotiated $40k off asking. Incredible.", reviewer: "Priya Sharma", rating: 5, source: "Google" },
];

describe("TestimonialsStack", () => {
  it("renders section with id=testimonials", () => {
    const { container } = render(<TestimonialsStack items={ITEMS} />);
    expect(container.querySelector("section#testimonials")).toBeInTheDocument();
  });

  it("renders the default heading when no title provided", () => {
    render(<TestimonialsStack items={ITEMS} />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Client Reviews");
  });

  it("renders custom title when provided", () => {
    render(<TestimonialsStack items={ITEMS} title="What People Say" />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("What People Say");
  });

  it("renders all quote texts", () => {
    render(<TestimonialsStack items={ITEMS} />);
    expect(screen.getByText(/Kai found us the perfect loft/)).toBeInTheDocument();
    expect(screen.getByText(/Super easy process/)).toBeInTheDocument();
    expect(screen.getByText(/Negotiated \$40k off asking/)).toBeInTheDocument();
  });

  it("renders all reviewer names", () => {
    render(<TestimonialsStack items={ITEMS} />);
    expect(screen.getByText("Mia Tanaka")).toBeInTheDocument();
    expect(screen.getByText("Derek Osei")).toBeInTheDocument();
    expect(screen.getByText("Priya Sharma")).toBeInTheDocument();
  });

  it("renders initial avatar with first letter of reviewer name", () => {
    render(<TestimonialsStack items={ITEMS} />);
    // Avatars rendered as aria-hidden spans, use data attribute instead
    const { container } = render(<TestimonialsStack items={ITEMS} />);
    const avatarEls = container.querySelectorAll("[data-avatar-initial]");
    expect(avatarEls.length).toBe(3);
    expect(avatarEls[0].textContent).toBe("M"); // Mia
    expect(avatarEls[1].textContent).toBe("D"); // Derek
    expect(avatarEls[2].textContent).toBe("P"); // Priya
  });

  it("renders source pills for items with source", () => {
    render(<TestimonialsStack items={ITEMS} />);
    expect(screen.getAllByText("Zillow").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Google").length).toBeGreaterThan(0);
  });

  it("renders FTC disclaimer", () => {
    render(<TestimonialsStack items={ITEMS} />);
    expect(screen.getByText(FTC_DISCLAIMER)).toBeInTheDocument();
  });

  it("renders vertical stack layout (flex column)", () => {
    const { container } = render(<TestimonialsStack items={ITEMS} />);
    const stack = container.querySelector("[data-testimonials-stack]") as HTMLElement;
    expect(stack).toBeInTheDocument();
    expect(stack.style.flexDirection).toBe("column");
  });

  it("renders empty section gracefully when items is empty", () => {
    const { container } = render(<TestimonialsStack items={[]} />);
    expect(container.querySelector("section#testimonials")).toBeInTheDocument();
  });
});
