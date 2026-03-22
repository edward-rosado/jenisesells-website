/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutEditorial } from "@/components/sections/about/AboutEditorial";
import { ACCOUNT, ACCOUNT_MINIMAL } from "../fixtures";
import type { AboutData } from "@/features/config/types";

const DATA_WITH_CREDENTIALS: AboutData = {
  bio: "Victoria Sterling has represented Manhattan's most prestigious properties for 18 years.",
  credentials: ["Top 1% NYC Agents", "$2.1B Career Volume", "REBNY Member"],
};

const DATA_ARRAY_BIO: AboutData = {
  bio: [
    "First paragraph about Victoria.",
    "Second paragraph about her career.",
    "Third paragraph about accolades.",
  ],
  credentials: ["ABR", "CRS"],
};

const DATA_NO_CREDENTIALS: AboutData = {
  bio: "Simple bio without credentials.",
  credentials: [],
};

describe("AboutEditorial", () => {
  it("renders section with id=about", () => {
    const { container } = render(<AboutEditorial agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    expect(container.querySelector("section#about")).toBeInTheDocument();
  });

  it("renders default heading using agent name when title is absent", () => {
    render(<AboutEditorial agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    expect(screen.getByRole("heading", { level: 2, name: "About Jane Smith" })).toBeInTheDocument();
  });

  it("renders custom title when provided in data", () => {
    const dataWithTitle: AboutData = { ...DATA_WITH_CREDENTIALS, title: "Meet Victoria" };
    render(<AboutEditorial agent={ACCOUNT} data={dataWithTitle} />);
    expect(screen.getByRole("heading", { level: 2, name: "Meet Victoria" })).toBeInTheDocument();
  });

  it("renders single bio string as paragraph", () => {
    render(<AboutEditorial agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    expect(screen.getByText(/Victoria Sterling has represented/)).toBeInTheDocument();
  });

  it("renders multiple bio paragraphs when bio is an array", () => {
    render(<AboutEditorial agent={ACCOUNT} data={DATA_ARRAY_BIO} />);
    expect(screen.getByText("First paragraph about Victoria.")).toBeInTheDocument();
    expect(screen.getByText("Second paragraph about her career.")).toBeInTheDocument();
    expect(screen.getByText("Third paragraph about accolades.")).toBeInTheDocument();
  });

  it("renders credentials as badge pills", () => {
    render(<AboutEditorial agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    expect(screen.getByText("Top 1% NYC Agents")).toBeInTheDocument();
    expect(screen.getByText("$2.1B Career Volume")).toBeInTheDocument();
    expect(screen.getByText("REBNY Member")).toBeInTheDocument();
  });

  it("does not render credentials list when credentials is empty", () => {
    render(<AboutEditorial agent={ACCOUNT} data={DATA_NO_CREDENTIALS} />);
    expect(screen.queryByRole("list", { name: "Credentials" })).not.toBeInTheDocument();
  });

  it("credentials have accent border styling", () => {
    render(<AboutEditorial agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    const list = screen.getByRole("list", { name: "Credentials" });
    const firstItem = list.querySelector("li");
    expect(firstItem!.style.border).toContain("color-accent");
  });

  it("renders agent headshot when headshot_url is provided", () => {
    const agentWithHeadshot = {
      ...ACCOUNT,
      agent: { ...ACCOUNT.agent!, headshot_url: "/agents/victoria/headshot.jpg" },
    };
    render(<AboutEditorial agent={agentWithHeadshot} data={DATA_WITH_CREDENTIALS} />);
    const img = screen.getByRole("img");
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("alt", "Photo of Jane Smith");
  });

  it("does not render headshot when headshot_url is absent", () => {
    render(<AboutEditorial agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("headshot container has circular shape (borderRadius 50%)", () => {
    const agentWithHeadshot = {
      ...ACCOUNT,
      agent: { ...ACCOUNT.agent!, headshot_url: "/agents/victoria/headshot.jpg" },
    };
    const { container } = render(<AboutEditorial agent={agentWithHeadshot} data={DATA_WITH_CREDENTIALS} />);
    const photoWrapper = container.querySelector("[data-photo-wrapper]");
    expect(photoWrapper).toBeInTheDocument();
    expect((photoWrapper as HTMLElement).style.borderRadius).toBe("50%");
  });

  it("headshot wrapper has accent border", () => {
    const agentWithHeadshot = {
      ...ACCOUNT,
      agent: { ...ACCOUNT.agent!, headshot_url: "/agents/victoria/headshot.jpg" },
    };
    const { container } = render(<AboutEditorial agent={agentWithHeadshot} data={DATA_WITH_CREDENTIALS} />);
    const photoWrapper = container.querySelector("[data-photo-wrapper]");
    expect((photoWrapper as HTMLElement).style.border).toContain("color-accent");
  });

  it("uses dark background on section", () => {
    const { container } = render(<AboutEditorial agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    const section = container.querySelector("section#about");
    expect(section!.style.background).toContain("color-primary");
  });

  it("renders minimal agent correctly", () => {
    render(<AboutEditorial agent={ACCOUNT_MINIMAL} data={DATA_NO_CREDENTIALS} />);
    expect(screen.getByRole("heading", { level: 2, name: "About Bob Jones" })).toBeInTheDocument();
  });
});
