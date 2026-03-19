// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render } from "@testing-library/react";
import { AboutParallax } from "@/components/sections/about/AboutParallax";
import { ACCOUNT, AGENT_PROP } from "../fixtures";
import type { AboutData } from "@/lib/types";

vi.mock("@/hooks/useParallax", () => ({
  useParallax: vi.fn(),
}));
vi.mock("@/hooks/useReducedMotion", () => ({
  useReducedMotion: vi.fn(() => false),
}));

const BIO_DATA: AboutData = {
  bio: ["First paragraph.", "Second paragraph."],
  credentials: ["ABR", "CRS"],
  image_url: "https://example.com/agent-photo.jpg",
};

describe("AboutParallax", () => {
  afterEach(() => { vi.restoreAllMocks(); });

  it("renders agent name", () => {
    const { getByText } = render(<AboutParallax agent={ACCOUNT} data={BIO_DATA} />);
    expect(getByText("Jane Smith")).toBeTruthy();
  });

  it("renders bio as multiple paragraphs when array", () => {
    const { getByText } = render(<AboutParallax agent={ACCOUNT} data={BIO_DATA} />);
    expect(getByText("First paragraph.")).toBeTruthy();
    expect(getByText("Second paragraph.")).toBeTruthy();
  });

  it("renders bio as single paragraph when string", () => {
    const data = { ...BIO_DATA, bio: "Single bio." };
    const { getByText } = render(<AboutParallax agent={ACCOUNT} data={data} />);
    expect(getByText("Single bio.")).toBeTruthy();
  });

  it("renders credentials as badges", () => {
    const { getByText } = render(<AboutParallax agent={ACCOUNT} data={BIO_DATA} />);
    expect(getByText("ABR")).toBeTruthy();
    expect(getByText("CRS")).toBeTruthy();
  });

  it("renders parallax background from data.image_url", () => {
    const { container } = render(<AboutParallax agent={ACCOUNT} data={BIO_DATA} />);
    const bg = container.querySelector("[data-parallax-bg]") as HTMLElement;
    expect(bg?.style.backgroundImage).toContain("agent-photo.jpg");
  });

  it("falls back to headshot_url when no image_url", () => {
    const data = { ...BIO_DATA, image_url: undefined };
    const agentWithPhoto = { ...AGENT_PROP, headshot_url: "https://example.com/headshot.jpg" };
    const { container } = render(<AboutParallax agent={agentWithPhoto} data={data} />);
    const bg = container.querySelector("[data-parallax-bg]") as HTMLElement;
    expect(bg?.style.backgroundImage).toContain("headshot.jpg");
  });

  it("renders solid background when no image available", () => {
    const data = { ...BIO_DATA, image_url: undefined };
    const { container } = render(<AboutParallax agent={ACCOUNT} data={data} />);
    const bg = container.querySelector("[data-parallax-bg]") as HTMLElement;
    // No backgroundImage, should have backgroundColor
    expect(bg?.style.backgroundImage).toBeFalsy();
  });

  it("renders content overlay card", () => {
    const { container } = render(<AboutParallax agent={ACCOUNT} data={BIO_DATA} />);
    const card = container.querySelector("[data-about-card]");
    expect(card).toBeTruthy();
  });
});
