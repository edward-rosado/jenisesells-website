import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { FeatureCards } from "@/features/landing/FeatureCards";

describe("FeatureCards", () => {
  it("renders all 8 feature cards", () => {
    const { container } = render(<FeatureCards />);
    const cards = container.querySelectorAll("[data-testid='feature-card']");
    expect(cards).toHaveLength(8);
  });

  it("renders 4 live features", () => {
    render(<FeatureCards />);
    const liveFeatures = [
      "Professional Website",
      "CMA Automation",
      "Google Drive Integration",
      "Lead Capture",
    ];
    for (const name of liveFeatures) {
      expect(screen.getByText(name)).toBeInTheDocument();
    }
  });

  it("renders 4 coming soon features with badges", () => {
    render(<FeatureCards />);
    const comingSoonBadges = screen.getAllByText("Coming Soon");
    expect(comingSoonBadges).toHaveLength(4);
  });

  it("renders coming soon feature names", () => {
    render(<FeatureCards />);
    const comingSoonFeatures = [
      "Auto-Replies",
      "Contract Drafting & DocuSign",
      "Photographer Scheduling",
      "MLS Listing Automation",
    ];
    for (const name of comingSoonFeatures) {
      expect(screen.getByText(name)).toBeInTheDocument();
    }
  });

  it("renders a section heading", () => {
    render(<FeatureCards />);
    expect(
      screen.getByRole("heading", { name: /everything you need/i })
    ).toBeInTheDocument();
  });

  it("each card has a description", () => {
    const { container } = render(<FeatureCards />);
    const descriptions = container.querySelectorAll("[data-testid='feature-description']");
    expect(descriptions).toHaveLength(8);
    for (const desc of descriptions) {
      expect(desc.textContent).toBeTruthy();
    }
  });
});
