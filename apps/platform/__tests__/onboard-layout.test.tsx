import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import OnboardLayout from "../app/onboard/layout";

describe("OnboardLayout", () => {
  it("renders children", () => {
    render(<OnboardLayout><p>hello</p></OnboardLayout>);
    expect(screen.getByText("hello")).toBeInTheDocument();
  });

  it("exports metadata with correct title", async () => {
    const { metadata } = await import("../app/onboard/layout");
    expect(metadata.title).toBe("Onboard Your Business | Real Estate Star");
  });

  it("exports metadata with correct description", async () => {
    const { metadata } = await import("../app/onboard/layout");
    expect(metadata.description).toContain("AI-powered");
  });
});
