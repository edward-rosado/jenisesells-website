/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutCompact } from "@/components/sections/about/AboutCompact";
import { AGENT } from "../fixtures";

const DATA = {
  bio: "Kai Nakamura is a top-producing NYC agent with 200+ deals.",
  credentials: ["Licensed NY Salesperson", "200+ NYC Deals", "Top 15% Brooklyn Volume"],
  title: "About Kai",
};

describe("AboutCompact", () => {
  it("renders section with id=about", () => {
    const { container } = render(<AboutCompact agent={AGENT} data={DATA} />);
    expect(container.querySelector("section#about")).toBeInTheDocument();
  });

  it("renders the heading", () => {
    render(<AboutCompact agent={AGENT} data={DATA} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders the bio text", () => {
    render(<AboutCompact agent={AGENT} data={DATA} />);
    expect(screen.getByText("Kai Nakamura is a top-producing NYC agent with 200+ deals.")).toBeInTheDocument();
  });

  it("renders all credential badges", () => {
    render(<AboutCompact agent={AGENT} data={DATA} />);
    expect(screen.getByText("Licensed NY Salesperson")).toBeInTheDocument();
    expect(screen.getByText("200+ NYC Deals")).toBeInTheDocument();
    expect(screen.getByText("Top 15% Brooklyn Volume")).toBeInTheDocument();
  });

  it("renders agent photo when headshot_url is provided", () => {
    const agentWithPhoto = {
      ...AGENT,
      identity: { ...AGENT.identity, headshot_url: "/agents/kai/headshot.jpg" },
    };
    render(<AboutCompact agent={agentWithPhoto} data={DATA} />);
    const img = screen.getByRole("img");
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("alt", AGENT.identity.name);
  });

  it("omits agent photo when headshot_url is not provided", () => {
    render(<AboutCompact agent={AGENT} data={DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("renders photo wrapper with circular style (borderRadius 50%)", () => {
    const agentWithPhoto = {
      ...AGENT,
      identity: { ...AGENT.identity, headshot_url: "/agents/kai/headshot.jpg" },
    };
    const { container } = render(<AboutCompact agent={agentWithPhoto} data={DATA} />);
    const photoWrapper = container.querySelector("[data-photo-wrapper]") as HTMLElement;
    expect(photoWrapper).toBeInTheDocument();
    expect(photoWrapper.style.borderRadius).toBe("50%");
  });

  it("renders horizontal layout (flex row)", () => {
    const { container } = render(<AboutCompact agent={AGENT} data={DATA} />);
    const layout = container.querySelector("[data-about-layout]") as HTMLElement;
    expect(layout).toBeInTheDocument();
    expect(layout.style.display).toBe("flex");
  });

  it("uses default title fallback when title is absent", () => {
    render(<AboutCompact agent={AGENT} data={{ bio: "Some bio.", credentials: [] }} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });
});
