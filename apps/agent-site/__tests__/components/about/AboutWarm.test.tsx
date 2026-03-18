/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutWarm } from "@/components/sections/about/AboutWarm";
import { AGENT } from "../fixtures";
import type { AboutData } from "@/lib/types";

const ABOUT_DATA: AboutData = {
  bio: "We are Rachel & David Kim — a husband-and-wife team dedicated to helping families find their forever home.",
  credentials: ["Licensed NC Brokers", "500+ Happy Families", "CLT Magazine Top Agents"],
};

describe("AboutWarm", () => {
  it("renders the default heading About", () => {
    render(<AboutWarm agent={AGENT} data={ABOUT_DATA} />);
    expect(screen.getByRole("heading", { level: 2, name: /About/ })).toBeInTheDocument();
  });

  it("renders a custom title when provided via data.title", () => {
    const dataWithTitle: AboutData = { ...ABOUT_DATA, title: "Meet the Team" };
    render(<AboutWarm agent={AGENT} data={dataWithTitle} />);
    expect(screen.getByRole("heading", { level: 2, name: "Meet the Team" })).toBeInTheDocument();
  });

  it("renders the bio text", () => {
    render(<AboutWarm agent={AGENT} data={ABOUT_DATA} />);
    expect(
      screen.getByText(
        "We are Rachel & David Kim — a husband-and-wife team dedicated to helping families find their forever home."
      )
    ).toBeInTheDocument();
  });

  it("handles array bio with multiple paragraphs", () => {
    const arrayBio: AboutData = {
      bio: ["First paragraph about us.", "Second paragraph about values."],
      credentials: [],
    };
    render(<AboutWarm agent={AGENT} data={arrayBio} />);
    expect(screen.getByText("First paragraph about us.")).toBeInTheDocument();
    expect(screen.getByText("Second paragraph about values.")).toBeInTheDocument();
  });

  it("renders credentials as green pills", () => {
    render(<AboutWarm agent={AGENT} data={ABOUT_DATA} />);
    expect(screen.getByText("Licensed NC Brokers")).toBeInTheDocument();
    expect(screen.getByText("500+ Happy Families")).toBeInTheDocument();
    expect(screen.getByText("CLT Magazine Top Agents")).toBeInTheDocument();
  });

  it("renders agent photo when headshot_url is set", () => {
    const agentWithPhoto = {
      ...AGENT,
      identity: { ...AGENT.identity, headshot_url: "/agents/test/headshot.jpg" },
    };
    render(<AboutWarm agent={agentWithPhoto} data={ABOUT_DATA} />);
    const img = screen.getByRole("img");
    expect(img).toBeInTheDocument();
  });

  it("does not render photo when headshot_url is not set", () => {
    const agentNoPhoto = { ...AGENT, identity: { ...AGENT.identity, headshot_url: undefined } };
    render(<AboutWarm agent={agentNoPhoto} data={ABOUT_DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("uses id=about for anchor linking", () => {
    const { container } = render(<AboutWarm agent={AGENT} data={ABOUT_DATA} />);
    expect(container.querySelector("#about")).toBeInTheDocument();
  });

  it("uses a warm background", () => {
    const { container } = render(<AboutWarm agent={AGENT} data={ABOUT_DATA} />);
    const section = container.querySelector("#about") as HTMLElement;
    expect(section?.style.background).toBeTruthy();
  });

  it("does not render credentials section when empty", () => {
    const noCreds: AboutData = { bio: "Bio text", credentials: [] };
    render(<AboutWarm agent={AGENT} data={noCreds} />);
    // None of the original credential texts should appear
    expect(screen.queryByText("Licensed NC Brokers")).not.toBeInTheDocument();
  });
});
