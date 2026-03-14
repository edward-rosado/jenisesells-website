import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { FinalCta } from "@/components/landing/FinalCta";

vi.mock("next/link", () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

describe("FinalCta", () => {
  it("renders a call-to-action heading", () => {
    render(<FinalCta />);
    expect(
      screen.getByRole("heading", { name: /ready to get started/i })
    ).toBeInTheDocument();
  });

  it("renders a CTA link to onboard", () => {
    render(<FinalCta />);
    const link = screen.getByRole("link", { name: /build your site free/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute("href", "/onboard");
  });

  it("renders a section element", () => {
    const { container } = render(<FinalCta />);
    expect(container.querySelector("section")).toBeInTheDocument();
  });

  it("renders a subheading with value proposition", () => {
    render(<FinalCta />);
    expect(screen.getByText(/14 days free\. \$14\.99\/mo\. Your business, automated\./i)).toBeInTheDocument();
  });
});
