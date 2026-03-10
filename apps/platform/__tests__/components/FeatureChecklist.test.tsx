import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { FeatureChecklist } from "../../components/chat/FeatureChecklist";

describe("FeatureChecklist", () => {
  it("renders the heading", () => {
    render(<FeatureChecklist />);
    expect(
      screen.getByText(/everything included with real estate star/i)
    ).toBeInTheDocument();
  });

  it("renders all features", () => {
    render(<FeatureChecklist />);
    expect(screen.getByText("Automated CMA reports")).toBeInTheDocument();
    expect(screen.getByText("Lead response within 60 seconds")).toBeInTheDocument();
    expect(screen.getByText("Professional agent website")).toBeInTheDocument();
  });
});
