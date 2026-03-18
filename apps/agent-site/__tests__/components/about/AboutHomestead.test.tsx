/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutHomestead } from "@/components/sections/about/AboutHomestead";
import { AGENT, AGENT_MINIMAL } from "../fixtures";
import type { AboutData } from "@/lib/types";

const DATA: AboutData = {
  title: "About James",
  bio: [
    "James Whitfield has specialized in Virginia estate and land properties for over 20 years.",
    "A lifelong Loudoun County resident, James brings unmatched local knowledge and a deep passion for the land.",
  ],
  credentials: ["VA Licensed Broker", "1,200+ Acres Sold", "LCAR Member"],
};

const DATA_NO_CREDENTIALS: AboutData = {
  bio: "James is a dedicated land broker.",
  credentials: [],
};

describe("AboutHomestead", () => {
  it("renders the heading using data.title when provided", () => {
    render(<AboutHomestead agent={AGENT} data={DATA} />);
    expect(
      screen.getByRole("heading", { level: 2, name: "About James" })
    ).toBeInTheDocument();
  });

  it("renders default heading About {agent.name} when title is absent", () => {
    render(<AboutHomestead agent={AGENT} data={DATA_NO_CREDENTIALS} />);
    expect(
      screen.getByRole("heading", { level: 2, name: "About Jane Smith" })
    ).toBeInTheDocument();
  });

  it("renders bio text — array form", () => {
    render(<AboutHomestead agent={AGENT} data={DATA} />);
    expect(
      screen.getByText(
        "James Whitfield has specialized in Virginia estate and land properties for over 20 years."
      )
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "A lifelong Loudoun County resident, James brings unmatched local knowledge and a deep passion for the land."
      )
    ).toBeInTheDocument();
  });

  it("renders bio text — string form", () => {
    render(<AboutHomestead agent={AGENT} data={DATA_NO_CREDENTIALS} />);
    expect(
      screen.getByText("James is a dedicated land broker.")
    ).toBeInTheDocument();
  });

  it("renders credentials as green pills", () => {
    render(<AboutHomestead agent={AGENT} data={DATA} />);
    expect(screen.getByText("VA Licensed Broker")).toBeInTheDocument();
    expect(screen.getByText("1,200+ Acres Sold")).toBeInTheDocument();
    expect(screen.getByText("LCAR Member")).toBeInTheDocument();
  });

  it("renders a credentials list with correct count", () => {
    render(<AboutHomestead agent={AGENT} data={DATA} />);
    const list = screen.getByRole("list", { name: "Credentials" });
    expect(list.querySelectorAll("li")).toHaveLength(3);
  });

  it("does not render credentials list when empty", () => {
    render(<AboutHomestead agent={AGENT} data={DATA_NO_CREDENTIALS} />);
    expect(
      screen.queryByRole("list", { name: "Credentials" })
    ).not.toBeInTheDocument();
  });

  it("renders agent photo when headshot_url is provided", () => {
    const agentWithPhoto = {
      ...AGENT,
      identity: { ...AGENT.identity, headshot_url: "/headshot.jpg" },
    };
    render(<AboutHomestead agent={agentWithPhoto} data={DATA} />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Jane Smith");
  });

  it("does not render photo when headshot_url is absent", () => {
    render(<AboutHomestead agent={AGENT} data={DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("renders section with id=about", () => {
    const { container } = render(<AboutHomestead agent={AGENT} data={DATA} />);
    expect(container.querySelector("#about")).toBeInTheDocument();
  });

  it("renders landscape-oriented photo container (wider than tall)", () => {
    const agentWithPhoto = {
      ...AGENT,
      identity: { ...AGENT.identity, headshot_url: "/headshot.jpg" },
    };
    const { container } = render(
      <AboutHomestead agent={agentWithPhoto} data={DATA} />
    );
    const imgWrapper = container.querySelector("[style*='width']");
    expect(imgWrapper).toBeInTheDocument();
  });

  it("prefers data.image_url over headshot_url", () => {
    const agentWithPhoto = {
      ...AGENT,
      identity: { ...AGENT.identity, headshot_url: "/headshot.jpg" },
    };
    render(
      <AboutHomestead
        agent={agentWithPhoto}
        data={{ ...DATA, image_url: "/about-landscape.jpg" }}
      />
    );
    const img = screen.getByRole("img");
    // Next.js Image with fill may transform the src attribute
    expect(img.getAttribute("src")).toContain("about-landscape");
    expect(img).toHaveAttribute("alt", "About Jane Smith");
  });

  it("renders minimal agent without crashing", () => {
    render(
      <AboutHomestead
        agent={AGENT_MINIMAL}
        data={{ bio: "Bob Jones serves rural Texas.", credentials: [] }}
      />
    );
    expect(
      screen.getByRole("heading", { level: 2, name: "About Bob Jones" })
    ).toBeInTheDocument();
  });
});
