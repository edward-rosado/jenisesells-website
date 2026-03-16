/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutMinimal } from "@/components/sections/about/AboutMinimal";
import { AGENT } from "../fixtures";
import type { AboutData } from "@/lib/types";

const ABOUT_DATA: AboutData = {
  bio: "A modern approach to real estate.",
  credentials: ["Licensed REALTOR", "Certified Negotiation Expert"],
};

describe("AboutMinimal", () => {
  it("renders the agent name in heading", () => {
    render(<AboutMinimal agent={AGENT} data={ABOUT_DATA} />);
    expect(screen.getByRole("heading", { level: 2, name: /About/ })).toBeInTheDocument();
  });

  it("renders the bio text", () => {
    render(<AboutMinimal agent={AGENT} data={ABOUT_DATA} />);
    expect(screen.getByText("A modern approach to real estate.")).toBeInTheDocument();
  });

  it("handles array bio", () => {
    const arrayBio: AboutData = { bio: ["First paragraph.", "Second paragraph."], credentials: [] };
    render(<AboutMinimal agent={AGENT} data={arrayBio} />);
    expect(screen.getByText("First paragraph.")).toBeInTheDocument();
    expect(screen.getByText("Second paragraph.")).toBeInTheDocument();
  });

  it("uses id=about for anchor linking", () => {
    const { container } = render(<AboutMinimal agent={AGENT} data={ABOUT_DATA} />);
    expect(container.querySelector("#about")).toBeInTheDocument();
  });

  it("renders agent photo when available", () => {
    render(<AboutMinimal agent={AGENT} data={ABOUT_DATA} />);
    if (AGENT.identity.headshot_url) {
      expect(screen.getByRole("img")).toBeInTheDocument();
    }
  });

  it("renders credentials as inline text", () => {
    render(<AboutMinimal agent={AGENT} data={ABOUT_DATA} />);
    expect(screen.getByText(/Licensed REALTOR/)).toBeInTheDocument();
    expect(screen.getByText(/Certified Negotiation Expert/)).toBeInTheDocument();
  });

  it("uses flexWrap for responsive layout", () => {
    const { container } = render(<AboutMinimal agent={AGENT} data={ABOUT_DATA} />);
    const flexContainer = container.querySelector("[style*='flex-wrap: wrap']");
    expect(flexContainer).toBeInTheDocument();
  });

  it("uses clean minimal style — no gradient background", () => {
    const { container } = render(<AboutMinimal agent={AGENT} data={ABOUT_DATA} />);
    const section = container.querySelector("#about");
    expect(section?.style.background).not.toContain("gradient");
  });
});
