/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutCompact } from "@/components/sections/about/AboutCompact";
import { ACCOUNT, ACCOUNT_MINIMAL } from "../fixtures";

const DATA = {
  bio: "Kai Nakamura is a top-producing NYC agent with 200+ deals.",
  credentials: ["Licensed NY Salesperson", "200+ NYC Deals", "Top 15% Brooklyn Volume"],
  title: "About Kai",
};

describe("AboutCompact", () => {
  it("renders section with id=about", () => {
    const { container } = render(<AboutCompact agent={ACCOUNT} data={DATA} />);
    expect(container.querySelector("section#about")).toBeInTheDocument();
  });

  it("renders the heading", () => {
    render(<AboutCompact agent={ACCOUNT} data={DATA} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders the bio text", () => {
    render(<AboutCompact agent={ACCOUNT} data={DATA} />);
    expect(screen.getByText("Kai Nakamura is a top-producing NYC agent with 200+ deals.")).toBeInTheDocument();
  });

  it("renders all credential badges", () => {
    render(<AboutCompact agent={ACCOUNT} data={DATA} />);
    expect(screen.getByText("Licensed NY Salesperson")).toBeInTheDocument();
    expect(screen.getByText("200+ NYC Deals")).toBeInTheDocument();
    expect(screen.getByText("Top 15% Brooklyn Volume")).toBeInTheDocument();
  });

  it("renders agent photo when headshot_url is provided", () => {
    const agentWithPhoto = {
      ...ACCOUNT,
      agent: { ...ACCOUNT.agent!, headshot_url: "/agents/kai/headshot.jpg" },
    };
    render(<AboutCompact agent={agentWithPhoto} data={DATA} />);
    const img = screen.getByRole("img");
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("alt", ACCOUNT.agent!.name);
  });

  it("omits agent photo when headshot_url is not provided", () => {
    render(<AboutCompact agent={ACCOUNT} data={DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("renders photo wrapper with circular style (borderRadius 50%)", () => {
    const agentWithPhoto = {
      ...ACCOUNT,
      agent: { ...ACCOUNT.agent!, headshot_url: "/agents/kai/headshot.jpg" },
    };
    const { container } = render(<AboutCompact agent={agentWithPhoto} data={DATA} />);
    const photoWrapper = container.querySelector("[data-photo-wrapper]") as HTMLElement;
    expect(photoWrapper).toBeInTheDocument();
    expect(photoWrapper.style.borderRadius).toBe("50%");
  });

  it("renders horizontal layout (flex row)", () => {
    const { container } = render(<AboutCompact agent={ACCOUNT} data={DATA} />);
    const layout = container.querySelector("[data-about-layout]") as HTMLElement;
    expect(layout).toBeInTheDocument();
    expect(layout.style.display).toBe("flex");
  });

  it("uses default title fallback when title is absent", () => {
    render(<AboutCompact agent={ACCOUNT} data={{ bio: "Some bio.", credentials: [] }} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });

  it("renders each paragraph when bio is an array", () => {
    const arrayBioData = {
      bio: ["First paragraph about the agent.", "Second paragraph with more detail."],
      credentials: [],
    };
    render(<AboutCompact agent={ACCOUNT} data={arrayBioData} />);
    expect(screen.getByText("First paragraph about the agent.")).toBeInTheDocument();
    expect(screen.getByText("Second paragraph with more detail.")).toBeInTheDocument();
  });

  it("renders single paragraph when bio is a string", () => {
    const stringBioData = {
      bio: "Single string bio paragraph.",
      credentials: [],
    };
    render(<AboutCompact agent={ACCOUNT} data={stringBioData} />);
    expect(screen.getByText("Single string bio paragraph.")).toBeInTheDocument();
  });

  it("does not render title span when agent title is absent", () => {
    const agentNoTitle = {
      ...ACCOUNT,
      agent: { ...ACCOUNT.agent!, title: undefined },
    };
    render(<AboutCompact agent={agentNoTitle} data={DATA} />);
    expect(screen.queryByText("REALTOR")).not.toBeInTheDocument();
  });

  it("renders using minimal agent without crashing", () => {
    render(<AboutCompact agent={ACCOUNT_MINIMAL} data={{ bio: "Bob Jones minimal bio.", credentials: [] }} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
  });
});
