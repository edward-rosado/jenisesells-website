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
});
