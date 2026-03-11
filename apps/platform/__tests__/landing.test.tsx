import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import LandingPage from "@/app/page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

describe("Landing Page", () => {
  it('displays the headline "Stop paying monthly."', () => {
    render(<LandingPage />);
    expect(
      screen.getByRole("heading", { name: /stop paying monthly\./i })
    ).toBeInTheDocument();
  });

  it('displays the price "$900. Everything."', () => {
    render(<LandingPage />);
    expect(screen.getByText(/\$900\. everything\./i)).toBeInTheDocument();
  });

  it("renders a URL input for the agent profile", () => {
    render(<LandingPage />);
    expect(
      screen.getByPlaceholderText(/paste your zillow or realtor\.com/i)
    ).toBeInTheDocument();
  });

  it('renders a CTA button with text "Get Started Free"', () => {
    render(<LandingPage />);
    expect(
      screen.getByRole("button", { name: /get started free/i })
    ).toBeInTheDocument();
  });

  it("displays a trial disclaimer", () => {
    render(<LandingPage />);
    expect(
      screen.getByText(/7-day free trial\. no credit card\./i)
    ).toBeInTheDocument();
  });

  it("renders the FeatureCards section", () => {
    render(<LandingPage />);
    expect(
      screen.getByRole("heading", { name: /everything you need/i })
    ).toBeInTheDocument();
  });

  it("renders the ComparisonTable section", () => {
    render(<LandingPage />);
    expect(
      screen.getByRole("heading", { name: /why agents switch/i })
    ).toBeInTheDocument();
  });

  it("renders the TrustStrip section", () => {
    render(<LandingPage />);
    expect(screen.getByText(/no monthly fees/i)).toBeInTheDocument();
  });

  it("renders all 8 feature cards", () => {
    const { container } = render(<LandingPage />);
    const cards = container.querySelectorAll("[data-testid='feature-card']");
    expect(cards).toHaveLength(8);
  });

  it("renders the FinalCta section", () => {
    render(<LandingPage />);
    expect(
      screen.getByRole("heading", { name: /ready to get started/i })
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: /start your free trial/i })
    ).toBeInTheDocument();
  });

  it("has proper page structure with hero at top", () => {
    const { container } = render(<LandingPage />);
    const main = container.querySelector("main");
    expect(main).toBeInTheDocument();
    // Hero section should contain the headline
    const hero = container.querySelector("[data-testid='hero-section']");
    expect(hero).toBeInTheDocument();
  });

  it("has an accessible label for the profile URL input", () => {
    render(<LandingPage />);
    const input = screen.getByLabelText(/zillow or realtor\.com/i);
    expect(input).toBeInTheDocument();
    expect(input).toHaveAttribute("type", "url");
  });

  it("hero form has aria-label for screen readers", () => {
    const { container } = render(<LandingPage />);
    const form = container.querySelector("form[aria-label]");
    expect(form).toBeInTheDocument();
  });
});
