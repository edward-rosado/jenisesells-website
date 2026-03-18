/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { HeroStory } from "@/components/sections/heroes/HeroStory";
import type { HeroData } from "@/lib/types";

const BASE_DATA: HeroData = {
  headline: "Your New Chapter Starts Here",
  highlight_word: "New Chapter",
  tagline: "Finding your place to call home",
  body: "We'll walk alongside you every step of the way.",
  cta_text: "Start Your Story",
  cta_link: "#cma-form",
};

describe("HeroStory", () => {
  it("renders the h1 headline", () => {
    render(<HeroStory data={BASE_DATA} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  });

  it("renders the tagline", () => {
    render(<HeroStory data={BASE_DATA} />);
    expect(screen.getByText("Finding your place to call home")).toBeInTheDocument();
  });

  it("renders the body text", () => {
    render(<HeroStory data={BASE_DATA} />);
    expect(screen.getByText("We'll walk alongside you every step of the way.")).toBeInTheDocument();
  });

  it("renders the CTA with correct href", () => {
    render(<HeroStory data={BASE_DATA} />);
    const link = screen.getByRole("link");
    expect(link.textContent).toContain("Start Your Story");
    expect(link).toHaveAttribute("href", "#cma-form");
  });

  it("sanitizes javascript: links to #", () => {
    const data = { ...BASE_DATA, cta_link: "javascript:alert(1)" };
    render(<HeroStory data={data} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "#");
  });

  it("renders agent photo badge when agentPhotoUrl is provided", () => {
    render(
      <HeroStory
        data={BASE_DATA}
        agentPhotoUrl="/agents/test/headshot.jpg"
        agentName="Rachel Kim"
      />
    );
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Photo of Rachel Kim");
  });

  it("omits agent photo when agentPhotoUrl is not provided", () => {
    render(<HeroStory data={BASE_DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("uses warm sage background", () => {
    const { container } = render(<HeroStory data={BASE_DATA} />);
    const section = container.querySelector("section");
    // jsdom normalizes hex to rgb: #f0f7f4 → rgb(240, 247, 244)
    expect(section?.style.background).toBe("rgb(240, 247, 244)");
  });

  it("renders CTA with rounded border radius of 30px", () => {
    render(<HeroStory data={BASE_DATA} />);
    const link = screen.getByRole("link");
    expect(link.style.borderRadius).toBe("30px");
  });

  it("highlights the highlight_word in the headline", () => {
    render(<HeroStory data={BASE_DATA} />);
    const heading = screen.getByRole("heading", { level: 1 });
    const span = heading.querySelector("span");
    expect(span).toBeInTheDocument();
    expect(span!.textContent).toBe("New Chapter");
  });

  it("changes style on CTA hover", () => {
    render(<HeroStory data={BASE_DATA} />);
    const link = screen.getByRole("link");
    fireEvent.mouseEnter(link);
    expect(link.style.transform).toBe("translateY(-2px)");
    fireEvent.mouseLeave(link);
    expect(link.style.transform).toBe("none");
  });

  it("changes style on CTA focus/blur", () => {
    render(<HeroStory data={BASE_DATA} />);
    const link = screen.getByRole("link");
    fireEvent.focus(link);
    expect(link.style.transform).toBe("translateY(-2px)");
    fireEvent.blur(link);
    expect(link.style.transform).toBe("none");
  });

  it("falls back to generic alt text when agentName is not provided", () => {
    render(<HeroStory data={BASE_DATA} agentPhotoUrl="/agents/test/headshot.jpg" />);
    const img = screen.getByRole("img");
    expect(img).toHaveAttribute("alt", "Agent photo");
  });
});
