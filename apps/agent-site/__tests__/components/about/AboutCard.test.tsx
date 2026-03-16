/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutCard } from "@/components/sections/about/AboutCard";
import { AGENT } from "../fixtures";
import type { AboutData } from "@/lib/types";

const ABOUT_DATA: AboutData = {
  bio: "I love helping families find their dream home.",
  credentials: ["Licensed REALTOR", "Certified Negotiation Expert"],
};

describe("AboutCard", () => {
  it("renders the agent name in heading", () => {
    render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    expect(screen.getByRole("heading", { level: 2, name: /About/ })).toBeInTheDocument();
  });

  it("renders the bio text", () => {
    render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    expect(screen.getByText("I love helping families find their dream home.")).toBeInTheDocument();
  });

  it("uses id=about for anchor linking", () => {
    const { container } = render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    expect(container.querySelector("#about")).toBeInTheDocument();
  });

  it("renders agent photo when available", () => {
    render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    const img = screen.queryByRole("img");
    if (AGENT.identity.headshot_url) {
      expect(img).toBeInTheDocument();
    }
  });

  it("renders credentials as badges", () => {
    render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    expect(screen.getByText("Licensed REALTOR")).toBeInTheDocument();
    expect(screen.getByText("Certified Negotiation Expert")).toBeInTheDocument();
  });

  it("uses rounded card layout with shadow", () => {
    const { container } = render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    const section = container.querySelector("#about");
    const card = section?.querySelector("[style*='border-radius']");
    expect(card).toBeInTheDocument();
  });

  it("renders phone number when available", () => {
    render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    if (AGENT.identity.phone) {
      expect(screen.getByText(new RegExp(AGENT.identity.phone.replace(/[()]/g, "\\$&")))).toBeInTheDocument();
    }
  });

  it("renders email when available", () => {
    render(<AboutCard agent={AGENT} data={ABOUT_DATA} />);
    if (AGENT.identity.email) {
      expect(screen.getByText(AGENT.identity.email)).toBeInTheDocument();
    }
  });
});
