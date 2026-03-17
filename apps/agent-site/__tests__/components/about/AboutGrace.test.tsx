/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutGrace } from "@/components/sections/about/AboutGrace";
import { AGENT, AGENT_MINIMAL } from "../fixtures";
import type { AboutData } from "@/lib/types";

const DATA_WITH_CREDENTIALS: AboutData = {
  bio: "Isabelle Fontaine has represented Fairfield County's finest properties for 25 years.",
  credentials: ["Top 1% CT Agents", "$850M Career Volume", "CREN Member"],
};

const DATA_ARRAY_BIO: AboutData = {
  bio: [
    "First paragraph about Isabelle.",
    "Second paragraph about her expertise.",
  ],
  credentials: ["ABR", "CRS"],
};

const DATA_NO_CREDENTIALS: AboutData = {
  bio: "Simple bio without credentials.",
  credentials: [],
};

describe("AboutGrace", () => {
  it("renders section with id=about", () => {
    const { container } = render(<AboutGrace agent={AGENT} data={DATA_WITH_CREDENTIALS} />);
    expect(container.querySelector("section#about")).toBeInTheDocument();
  });

  it("renders default heading using agent name when title is absent", () => {
    render(<AboutGrace agent={AGENT} data={DATA_WITH_CREDENTIALS} />);
    expect(screen.getByRole("heading", { level: 2, name: "About Jane Smith" })).toBeInTheDocument();
  });

  it("renders custom title when provided in data", () => {
    const dataWithTitle: AboutData = { ...DATA_WITH_CREDENTIALS, title: "Meet Isabelle" };
    render(<AboutGrace agent={AGENT} data={dataWithTitle} />);
    expect(screen.getByRole("heading", { level: 2, name: "Meet Isabelle" })).toBeInTheDocument();
  });

  it("renders bio text", () => {
    render(<AboutGrace agent={AGENT} data={DATA_WITH_CREDENTIALS} />);
    expect(screen.getByText(/Isabelle Fontaine has represented/)).toBeInTheDocument();
  });

  it("renders multiple bio paragraphs when bio is an array", () => {
    render(<AboutGrace agent={AGENT} data={DATA_ARRAY_BIO} />);
    expect(screen.getByText("First paragraph about Isabelle.")).toBeInTheDocument();
    expect(screen.getByText("Second paragraph about her expertise.")).toBeInTheDocument();
  });

  it("renders credentials as comma-separated text (not pills)", () => {
    render(<AboutGrace agent={AGENT} data={DATA_WITH_CREDENTIALS} />);
    // credentials appear as a single text node with commas
    const credEl = screen.getByText(/Top 1% CT Agents.*\$850M Career Volume.*CREN Member/);
    expect(credEl).toBeInTheDocument();
  });

  it("does not render credentials section when credentials is empty", () => {
    render(<AboutGrace agent={AGENT} data={DATA_NO_CREDENTIALS} />);
    expect(screen.queryByText(/Top 1%/)).not.toBeInTheDocument();
  });

  it("renders agent headshot when headshot_url is provided", () => {
    const agentWithHeadshot = {
      ...AGENT,
      identity: { ...AGENT.identity, headshot_url: "/agents/isabelle/headshot.jpg" },
    };
    render(<AboutGrace agent={agentWithHeadshot} data={DATA_WITH_CREDENTIALS} />);
    const img = screen.getByRole("img");
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("alt", "Jane Smith");
  });

  it("does not render headshot when headshot_url is absent", () => {
    render(<AboutGrace agent={AGENT} data={DATA_WITH_CREDENTIALS} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("headshot container has thin accent border", () => {
    const agentWithHeadshot = {
      ...AGENT,
      identity: { ...AGENT.identity, headshot_url: "/agents/isabelle/headshot.jpg" },
    };
    const { container } = render(<AboutGrace agent={agentWithHeadshot} data={DATA_WITH_CREDENTIALS} />);
    const photoWrapper = container.querySelector("[data-photo-wrapper]");
    expect(photoWrapper).toBeInTheDocument();
    expect((photoWrapper as HTMLElement).style.border).toContain("color-accent");
  });

  it("renders minimal agent correctly", () => {
    render(<AboutGrace agent={AGENT_MINIMAL} data={DATA_NO_CREDENTIALS} />);
    expect(screen.getByRole("heading", { level: 2, name: "About Bob Jones" })).toBeInTheDocument();
  });
});
