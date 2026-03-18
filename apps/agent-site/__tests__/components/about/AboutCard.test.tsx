/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutCard } from "@/components/sections/about/AboutCard";
import { ACCOUNT } from "../fixtures";
import type { AboutData } from "@/lib/types";

const ABOUT_DATA: AboutData = {
  bio: "I love helping families find their dream home.",
  credentials: ["Licensed REALTOR", "Certified Negotiation Expert"],
};

describe("AboutCard", () => {
  it("renders the agent name in heading", () => {
    render(<AboutCard agent={ACCOUNT} data={ABOUT_DATA} />);
    expect(screen.getByRole("heading", { level: 2, name: /About/ })).toBeInTheDocument();
  });

  it("renders the bio text", () => {
    render(<AboutCard agent={ACCOUNT} data={ABOUT_DATA} />);
    expect(screen.getByText("I love helping families find their dream home.")).toBeInTheDocument();
  });

  it("uses id=about for anchor linking", () => {
    const { container } = render(<AboutCard agent={ACCOUNT} data={ABOUT_DATA} />);
    expect(container.querySelector("#about")).toBeInTheDocument();
  });

  it("renders agent photo when headshot_url is set", () => {
    const agentWithPhoto = { ...ACCOUNT, agent: { ...ACCOUNT.agent!, headshot_url: "/photos/agent.jpg" } };
    render(<AboutCard agent={agentWithPhoto} data={ABOUT_DATA} />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Photo of Jane Smith");
  });

  it("renders credentials as badges", () => {
    render(<AboutCard agent={ACCOUNT} data={ABOUT_DATA} />);
    expect(screen.getByText("Licensed REALTOR")).toBeInTheDocument();
    expect(screen.getByText("Certified Negotiation Expert")).toBeInTheDocument();
  });

  it("uses rounded card layout with shadow", () => {
    const { container } = render(<AboutCard agent={ACCOUNT} data={ABOUT_DATA} />);
    const section = container.querySelector("#about");
    const card = section?.querySelector("[style*='border-radius']");
    expect(card).toBeInTheDocument();
  });

  it("renders phone number when available", () => {
    render(<AboutCard agent={ACCOUNT} data={ABOUT_DATA} />);
    if (ACCOUNT.agent?.phone) {
      expect(screen.getByText(new RegExp(ACCOUNT.agent.phone.replace(/[()]/g, "\\$&")))).toBeInTheDocument();
    }
  });

  it("renders email when available", () => {
    render(<AboutCard agent={ACCOUNT} data={ABOUT_DATA} />);
    if (ACCOUNT.agent?.email) {
      expect(screen.getByText(ACCOUNT.agent.email)).toBeInTheDocument();
    }
  });

  it("handles array bio with multiple paragraphs", () => {
    const arrayBio: AboutData = {
      bio: ["First paragraph.", "Second paragraph."],
      credentials: [],
    };
    render(<AboutCard agent={ACCOUNT} data={arrayBio} />);
    expect(screen.getByText("First paragraph.")).toBeInTheDocument();
    expect(screen.getByText("Second paragraph.")).toBeInTheDocument();
  });

  it("does not render photo when headshot_url is not set", () => {
    const agentNoPhoto = { ...ACCOUNT, agent: { ...ACCOUNT.agent!, headshot_url: undefined } };
    render(<AboutCard agent={agentNoPhoto} data={ABOUT_DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("does not render credentials section when empty", () => {
    const noCreds: AboutData = { bio: "Bio text", credentials: [] };
    render(<AboutCard agent={ACCOUNT} data={noCreds} />);
    expect(screen.queryByText("ABR")).not.toBeInTheDocument();
  });

  it("does not render phone when not set", () => {
    const agentNoPhone = { ...ACCOUNT, agent: { ...ACCOUNT.agent!, phone: "" } };
    render(<AboutCard agent={agentNoPhone} data={ABOUT_DATA} />);
    expect(screen.queryByText("555-123-4567")).not.toBeInTheDocument();
  });

  it("does not render email when not set", () => {
    const agentNoEmail = { ...ACCOUNT, agent: { ...ACCOUNT.agent!, email: "" } };
    render(<AboutCard agent={agentNoEmail} data={ABOUT_DATA} />);
    expect(screen.queryByText("jane@example.com")).not.toBeInTheDocument();
  });

  it("uses custom title from data.title when provided", () => {
    const dataWithTitle: AboutData = { ...ABOUT_DATA, title: "Meet Your Agent" };
    render(<AboutCard agent={ACCOUNT} data={dataWithTitle} />);
    expect(screen.getByRole("heading", { level: 2, name: "Meet Your Agent" })).toBeInTheDocument();
  });

  it("falls back to agent name when data.title is not provided", () => {
    render(<AboutCard agent={ACCOUNT} data={ABOUT_DATA} />);
    expect(screen.getByRole("heading", { level: 2, name: /About Jane Smith/ })).toBeInTheDocument();
  });
});
