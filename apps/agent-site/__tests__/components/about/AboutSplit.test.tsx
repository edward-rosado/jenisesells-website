/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutSplit } from "@/components/sections/about/AboutSplit";
import { ACCOUNT, ACCOUNT_MINIMAL } from "../fixtures";
import type { AboutData } from "@/lib/types";

const DATA_WITH_CREDENTIALS: AboutData = {
  bio: "Jane Smith is a top agent in New Jersey.",
  credentials: ["ABR", "CRS", "GRI"],
};

const DATA_NO_CREDENTIALS: AboutData = {
  bio: "Bob Jones is a dedicated agent.",
  credentials: [],
};

const DATA_CREDENTIALS_UNDEFINED: AboutData = {
  bio: "Alice Brown serves the community.",
};

describe("AboutSplit", () => {
  it("renders the heading with agent name", () => {
    render(<AboutSplit agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    expect(screen.getByRole("heading", { level: 2, name: "About Jane Smith" })).toBeInTheDocument();
  });

  it("renders the bio text", () => {
    render(<AboutSplit agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    expect(screen.getByText("Jane Smith is a top agent in New Jersey.")).toBeInTheDocument();
  });

  it("renders all credentials as badges when credentials array has items", () => {
    render(<AboutSplit agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    expect(screen.getByText("ABR")).toBeInTheDocument();
    expect(screen.getByText("CRS")).toBeInTheDocument();
    expect(screen.getByText("GRI")).toBeInTheDocument();
  });

  it("does not render credential badges when credentials is empty", () => {
    render(<AboutSplit agent={ACCOUNT} data={DATA_NO_CREDENTIALS} />);
    // The container div for credentials should not be rendered
    expect(screen.queryByText("ABR")).not.toBeInTheDocument();
  });

  it("does not render credential badges when credentials is undefined", () => {
    render(<AboutSplit agent={ACCOUNT} data={DATA_CREDENTIALS_UNDEFINED} />);
    expect(screen.queryByRole("list", { name: "Credentials" })).not.toBeInTheDocument();
  });

  it("uses minimal agent name in heading", () => {
    render(<AboutSplit agent={ACCOUNT_MINIMAL} data={DATA_NO_CREDENTIALS} />);
    expect(screen.getByRole("heading", { level: 2, name: "About Bob Jones" })).toBeInTheDocument();
  });

  it("renders correct number of credential badges", () => {
    render(<AboutSplit agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    const list = screen.getByRole("list", { name: "Credentials" });
    expect(list.querySelectorAll("li")).toHaveLength(3);
  });

  it("renders a single credential correctly", () => {
    render(<AboutSplit agent={ACCOUNT} data={{ bio: "Bio here", credentials: ["REALTOR"] }} />);
    expect(screen.getByText("REALTOR")).toBeInTheDocument();
  });

  it("renders multiple paragraphs when bio is an array", () => {
    const arrayBioData: AboutData = {
      bio: ["First paragraph.", "Second paragraph.", "Third paragraph."],
      credentials: [],
    };
    render(<AboutSplit agent={ACCOUNT} data={arrayBioData} />);
    expect(screen.getByText("First paragraph.")).toBeInTheDocument();
    expect(screen.getByText("Second paragraph.")).toBeInTheDocument();
    expect(screen.getByText("Third paragraph.")).toBeInTheDocument();
  });

  it("renders headshot image when agent has headshot_url", () => {
    const accountWithHeadshot = {
      ...ACCOUNT,
      agent: {
        ...ACCOUNT.agent!,
        headshot_url: "https://example.com/headshot.jpg",
      },
    };
    render(<AboutSplit agent={accountWithHeadshot} data={DATA_WITH_CREDENTIALS} />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Jane Smith");
  });

  it("does not render headshot image when headshot_url is absent", () => {
    render(<AboutSplit agent={ACCOUNT} data={DATA_WITH_CREDENTIALS} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });
});
