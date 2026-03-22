/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutProfessional } from "../AboutProfessional";
import { ACCOUNT, ACCOUNT_MINIMAL } from "../../../../__tests__/fixtures";
import type { AboutData } from "@/features/config/types";

const DATA_WITH_CREDENTIALS: AboutData = {
  title: "About Robert Chen",
  bio: "Robert Chen is a senior commercial real estate advisor with $1.8B in career volume.",
  credentials: ["CCIM Designated", "SIOR Member", "$1.8B Career Volume", "TX Licensed Broker"],
};

const DATA_ARRAY_BIO: AboutData = {
  bio: ["First paragraph of professional bio.", "Second paragraph with more detail."],
  credentials: ["CCIM Designated"],
};

const DATA_NO_CREDENTIALS: AboutData = {
  bio: "A professional agent with deep market expertise.",
  credentials: [],
};

const AGENT_WITH_HEADSHOT = {
  ...ACCOUNT,
  agent: {
    ...ACCOUNT.agent!,
    headshot_url: "/agents/test-commercial/headshot.jpg",
  },
};

describe("AboutProfessional", () => {
  it("renders the section heading with custom title", () => {
    render(<AboutProfessional agent={AGENT_WITH_HEADSHOT} data={DATA_WITH_CREDENTIALS} />);
    expect(screen.getByRole("heading", { level: 2, name: "About Robert Chen" })).toBeInTheDocument();
  });

  it("renders default heading using agent name when no title provided", () => {
    render(<AboutProfessional agent={ACCOUNT} data={DATA_NO_CREDENTIALS} />);
    expect(screen.getByRole("heading", { level: 2, name: "About Jane Smith" })).toBeInTheDocument();
  });

  it("renders the bio text", () => {
    render(<AboutProfessional agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    expect(
      screen.getByText("Robert Chen is a senior commercial real estate advisor with $1.8B in career volume.")
    ).toBeInTheDocument();
  });

  it("renders multiple paragraphs when bio is an array", () => {
    render(<AboutProfessional agent={ACCOUNT} data={DATA_ARRAY_BIO} />);
    expect(screen.getByText("First paragraph of professional bio.")).toBeInTheDocument();
    expect(screen.getByText("Second paragraph with more detail.")).toBeInTheDocument();
  });

  it("renders designation/credential pills", () => {
    render(<AboutProfessional agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    expect(screen.getByText("CCIM Designated")).toBeInTheDocument();
    expect(screen.getByText("SIOR Member")).toBeInTheDocument();
    expect(screen.getByText("$1.8B Career Volume")).toBeInTheDocument();
    expect(screen.getByText("TX Licensed Broker")).toBeInTheDocument();
  });

  it("does not render credential pills when credentials is empty", () => {
    render(<AboutProfessional agent={ACCOUNT} data={DATA_NO_CREDENTIALS} />);
    expect(screen.queryByText("CCIM Designated")).not.toBeInTheDocument();
  });

  it("renders agent photo as rectangular (not circular) when headshot_url provided", () => {
    render(
      <AboutProfessional agent={AGENT_WITH_HEADSHOT} data={DATA_WITH_CREDENTIALS} />
    );
    const img = screen.getByRole("img");
    expect(img).toBeInTheDocument();
    // Check that the wrapper does NOT have borderRadius of 50%
    const imgWrapper = img.closest("[data-testid='headshot-wrapper']");
    expect(imgWrapper).toBeInTheDocument();
    expect((imgWrapper as HTMLElement).style.borderRadius).not.toBe("50%");
  });

  it("does not render image when headshot_url is absent", () => {
    render(<AboutProfessional agent={ACCOUNT_MINIMAL} data={DATA_WITH_CREDENTIALS} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("uses id=about for anchor linking", () => {
    const { container } = render(<AboutProfessional agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    expect(container.querySelector("#about")).toBeInTheDocument();
  });
});
