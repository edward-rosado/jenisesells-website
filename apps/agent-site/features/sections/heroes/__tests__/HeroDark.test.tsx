/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { HeroDark } from "@/features/sections/heroes/HeroDark";
import type { HeroData } from "@/features/config/types";

const heroData: HeroData = {
  headline: "Exceptional Homes for Exceptional Lives",
  highlight_word: "Exceptional Lives",
  tagline: "Curating premier properties",
  body: "Since 2008, we have served Manhattan's most prestigious addresses.",
  cta_text: "View Portfolio",
  cta_link: "#sold",
};

describe("HeroDark", () => {
  it("renders the headline in an h1", () => {
    render(<HeroDark data={heroData} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 1 }).textContent).toContain("Exceptional Homes for");
  });

  it("renders the tagline", () => {
    render(<HeroDark data={heroData} />);
    expect(screen.getByText("Curating premier properties")).toBeInTheDocument();
  });

  it("renders the body text", () => {
    render(<HeroDark data={heroData} />);
    expect(screen.getByText("Since 2008, we have served Manhattan's most prestigious addresses.")).toBeInTheDocument();
  });

  it("renders the CTA link with correct href", () => {
    render(<HeroDark data={heroData} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#sold");
    expect(screen.getByRole("link").textContent).toContain("View Portfolio");
  });

  it("highlights the accent word with var(--color-accent)", () => {
    render(<HeroDark data={heroData} />);
    const heading = screen.getByRole("heading", { level: 1 });
    const span = heading.querySelector("span");
    expect(span).toBeInTheDocument();
    expect(span!.textContent).toBe("Exceptional Lives");
    expect(span!.style.color).toBe("var(--color-accent)");
  });

  it("renders agent photo with correct alt when agentPhotoUrl is provided", () => {
    render(<HeroDark data={heroData} agentPhotoUrl="/agents/test/headshot.jpg" agentName="Victoria" />);
    const img = screen.getByRole("img");
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("alt", "Photo of Victoria");
  });

  it("omits agent photo when agentPhotoUrl is not provided", () => {
    render(<HeroDark data={heroData} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("uses dark background via inline style containing #0a0a0a", () => {
    const { container } = render(<HeroDark data={heroData} />);
    const section = container.querySelector("section");
    expect(section).toBeInTheDocument();
    expect(section!.style.background).toContain("#0a0a0a");
  });

  it("renders tagline in uppercase via textTransform", () => {
    render(<HeroDark data={heroData} />);
    const tagline = screen.getByText("Curating premier properties");
    expect(tagline.style.textTransform).toBe("uppercase");
  });

  it("does not render body text when body is absent", () => {
    const dataNoBody: HeroData = { ...heroData, body: undefined };
    render(<HeroDark data={dataNoBody} />);
    expect(screen.queryByText("Since 2008")).not.toBeInTheDocument();
  });

  it("sanitizes javascript: cta_link to #", () => {
    render(<HeroDark data={{ ...heroData, cta_link: "javascript:alert(1)" }} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("CTA changes style on hover (fills with accent color)", () => {
    render(<HeroDark data={heroData} />);
    const cta = screen.getByRole("link");
    // Before hover — transparent background
    expect(cta.style.background).toBe("transparent");
    fireEvent.mouseEnter(cta);
    // After hover — accent fills
    expect(cta.style.background).toContain("color-accent");
    fireEvent.mouseLeave(cta);
    expect(cta.style.background).toBe("transparent");
  });

  it("renders a section element", () => {
    const { container } = render(<HeroDark data={heroData} />);
    expect(container.querySelector("section")).toBeInTheDocument();
  });

  it("CTA changes style on focus and reverts on blur", () => {
    render(<HeroDark data={heroData} />);
    const cta = screen.getByRole("link");
    // Before focus — transparent background
    expect(cta.style.background).toBe("transparent");
    fireEvent.focus(cta);
    // After focus — accent fills
    expect(cta.style.background).toContain("color-accent");
    fireEvent.blur(cta);
    // After blur — transparent restored
    expect(cta.style.background).toBe("transparent");
  });

  it("renders agent photo with generic alt when agentName is not provided", () => {
    render(<HeroDark data={heroData} agentPhotoUrl="/agents/test/headshot.jpg" />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Agent photo");
  });
});
