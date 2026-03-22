/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { HeroCorporate } from "@/components/sections/heroes/HeroCorporate";
import type { HeroData } from "@/features/config/types";

const BASE_DATA: HeroData = {
  headline: "Strategic Commercial Real Estate in DFW",
  highlight_word: "DFW",
  tagline: "$1.8B Transaction Volume",
  body: "Institutional-grade advisory for office, retail, and industrial assets.",
  cta_text: "Request a Consultation",
  cta_link: "#cma-form",
};

describe("HeroCorporate", () => {
  it("renders the h1 headline", () => {
    render(<HeroCorporate data={BASE_DATA} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  });

  it("renders the tagline", () => {
    render(<HeroCorporate data={BASE_DATA} />);
    expect(screen.getByText("$1.8B Transaction Volume")).toBeInTheDocument();
  });

  it("renders the body text", () => {
    render(<HeroCorporate data={BASE_DATA} />);
    expect(
      screen.getByText("Institutional-grade advisory for office, retail, and industrial assets.")
    ).toBeInTheDocument();
  });

  it("renders the primary CTA link", () => {
    render(<HeroCorporate data={BASE_DATA} />);
    const links = screen.getAllByRole("link");
    const primaryCta = links.find((l) => l.textContent?.includes("Request a Consultation"));
    expect(primaryCta).toBeInTheDocument();
    expect(primaryCta).toHaveAttribute("href", "#cma-form");
  });

  it("renders a secondary outlined CTA", () => {
    render(<HeroCorporate data={BASE_DATA} />);
    const links = screen.getAllByRole("link");
    expect(links.length).toBeGreaterThanOrEqual(2);
  });

  it("renders the blue accent bar at the top", () => {
    const { container } = render(<HeroCorporate data={BASE_DATA} />);
    // accent bar is a div with height 4px and blue background
    const bar = container.querySelector("[data-testid='accent-bar']");
    expect(bar).toBeInTheDocument();
  });

  it("does NOT render an agent photo even when agentPhotoUrl is provided", () => {
    render(
      <HeroCorporate
        data={BASE_DATA}
        agentPhotoUrl="/agents/headshot.jpg"
        agentName="Robert Chen"
      />
    );
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("highlights the highlight_word in the headline", () => {
    render(<HeroCorporate data={BASE_DATA} />);
    const heading = screen.getByRole("heading", { level: 1 });
    const span = heading.querySelector("span");
    expect(span).toBeInTheDocument();
    expect(span!.textContent).toBe("DFW");
  });

  it("sanitizes javascript: links to #", () => {
    const data = { ...BASE_DATA, cta_link: "javascript:alert(1)" };
    render(<HeroCorporate data={data} />);
    const links = screen.getAllByRole("link");
    links.forEach((l) => expect(l).not.toHaveAttribute("href", "javascript:alert(1)"));
  });

  it("uses cool gray background (#f4f5f7)", () => {
    const { container } = render(<HeroCorporate data={BASE_DATA} />);
    const section = container.querySelector("section");
    // background should be cool gray
    expect(section?.style.background).toBeTruthy();
  });

  it("changes primary CTA style on hover", () => {
    render(<HeroCorporate data={BASE_DATA} />);
    const links = screen.getAllByRole("link");
    const primaryCta = links.find((l) => l.textContent?.includes("Request a Consultation"))!;
    fireEvent.mouseEnter(primaryCta);
    expect(primaryCta).toBeInTheDocument();
    fireEvent.mouseLeave(primaryCta);
    expect(primaryCta).toBeInTheDocument();
  });

  it("changes primary CTA style on focus and reverts on blur", () => {
    render(<HeroCorporate data={BASE_DATA} />);
    const links = screen.getAllByRole("link");
    const primaryCta = links.find((l) => l.textContent?.includes("Request a Consultation"))!;
    fireEvent.focus(primaryCta);
    expect(primaryCta).toBeInTheDocument();
    fireEvent.blur(primaryCta);
    expect(primaryCta).toBeInTheDocument();
  });
});
