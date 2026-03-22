/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutMinimal } from "@/components/sections/about/AboutMinimal";
import { ACCOUNT } from "../fixtures";
import type { AboutData } from "@/features/config/types";

const ABOUT_DATA: AboutData = {
  bio: "A modern approach to real estate.",
  credentials: ["Licensed REALTOR", "Certified Negotiation Expert"],
};

describe("AboutMinimal", () => {
  it("renders the agent name in heading", () => {
    render(<AboutMinimal agent={ACCOUNT} data={ABOUT_DATA} />);
    expect(screen.getByRole("heading", { level: 2, name: /About/ })).toBeInTheDocument();
  });

  it("renders the bio text", () => {
    render(<AboutMinimal agent={ACCOUNT} data={ABOUT_DATA} />);
    expect(screen.getByText("A modern approach to real estate.")).toBeInTheDocument();
  });

  it("handles array bio", () => {
    const arrayBio: AboutData = { bio: ["First paragraph.", "Second paragraph."], credentials: [] };
    render(<AboutMinimal agent={ACCOUNT} data={arrayBio} />);
    expect(screen.getByText("First paragraph.")).toBeInTheDocument();
    expect(screen.getByText("Second paragraph.")).toBeInTheDocument();
  });

  it("uses id=about for anchor linking", () => {
    const { container } = render(<AboutMinimal agent={ACCOUNT} data={ABOUT_DATA} />);
    expect(container.querySelector("#about")).toBeInTheDocument();
  });

  it("renders agent photo when available", () => {
    const agentWithPhoto = { ...ACCOUNT, agent: { ...ACCOUNT.agent!, headshot_url: "/photos/agent.jpg" } };
    render(<AboutMinimal agent={agentWithPhoto} data={ABOUT_DATA} />);
    expect(screen.getByRole("img")).toBeInTheDocument();
  });

  it("renders credentials as inline text", () => {
    render(<AboutMinimal agent={ACCOUNT} data={ABOUT_DATA} />);
    expect(screen.getByText(/Licensed REALTOR/)).toBeInTheDocument();
    expect(screen.getByText(/Certified Negotiation Expert/)).toBeInTheDocument();
  });

  it("uses flexWrap for responsive layout", () => {
    const { container } = render(<AboutMinimal agent={ACCOUNT} data={ABOUT_DATA} />);
    const flexContainer = container.querySelector("[style*='flex-wrap: wrap']");
    expect(flexContainer).toBeInTheDocument();
  });

  it("uses clean minimal style — no gradient background", () => {
    const { container } = render(<AboutMinimal agent={ACCOUNT} data={ABOUT_DATA} />);
    const section = container.querySelector("#about");
    expect(section?.getAttribute("style")).not.toContain("gradient");
  });

  it("does not render photo when headshot_url is not set", () => {
    render(<AboutMinimal agent={ACCOUNT} data={ABOUT_DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("renders photo when headshot_url is set", () => {
    const agentWithPhoto = { ...ACCOUNT, agent: { ...ACCOUNT.agent!, headshot_url: "/photos/agent.jpg" } };
    render(<AboutMinimal agent={agentWithPhoto} data={ABOUT_DATA} />);
    expect(screen.getByRole("img")).toBeInTheDocument();
  });

  it("does not render credentials when empty", () => {
    const noCreds: AboutData = { bio: "Bio text", credentials: [] };
    render(<AboutMinimal agent={ACCOUNT} data={noCreds} />);
    expect(screen.queryByText(/Licensed REALTOR/)).not.toBeInTheDocument();
  });

  it("uses custom title from data.title", () => {
    const dataWithTitle: AboutData = { ...ABOUT_DATA, title: "Meet Your Agent" };
    render(<AboutMinimal agent={ACCOUNT} data={dataWithTitle} />);
    expect(screen.getByRole("heading", { level: 2, name: "Meet Your Agent" })).toBeInTheDocument();
  });
});
