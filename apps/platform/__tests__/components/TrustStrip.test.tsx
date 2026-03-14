import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { TrustStrip } from "@/components/landing/TrustStrip";

describe("TrustStrip", () => {
  it("renders trust indicators", () => {
    render(<TrustStrip />);
    expect(screen.getByText(/14 Days Free/i)).toBeInTheDocument();
    expect(screen.getByText(/\$14\.99\/mo After/i)).toBeInTheDocument();
    expect(screen.getByText(/Live in Minutes/i)).toBeInTheDocument();
  });

  it("renders a section element", () => {
    const { container } = render(<TrustStrip />);
    expect(container.querySelector("section")).toBeInTheDocument();
  });

  it("renders 3 trust items", () => {
    const { container } = render(<TrustStrip />);
    const items = container.querySelectorAll("[data-testid='trust-item']");
    expect(items).toHaveLength(3);
  });
});
