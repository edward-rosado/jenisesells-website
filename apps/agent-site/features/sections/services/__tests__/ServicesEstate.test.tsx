/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ServicesEstate } from "@/features/sections/services/ServicesEstate";
import type { ServiceItem } from "@/features/config/types";

const ITEMS: ServiceItem[] = [
  {
    title: "Estate Valuation",
    description: "Comprehensive market analysis for luxury estates.",
  },
  {
    title: "Land Assessment",
    description: "Expert evaluation of acreage, soil, and development potential.",
  },
  {
    title: "Equestrian Properties",
    description: "Specializing in horse farms and equestrian estates.",
  },
];

describe("ServicesEstate", () => {
  it("renders the section heading with default title", () => {
    render(<ServicesEstate items={ITEMS} />);
    expect(
      screen.getByRole("heading", { level: 2, name: "Our Services" })
    ).toBeInTheDocument();
  });

  it("renders custom title when provided", () => {
    render(<ServicesEstate items={ITEMS} title="Estate Expertise" />);
    expect(
      screen.getByRole("heading", { level: 2, name: "Estate Expertise" })
    ).toBeInTheDocument();
  });

  it("renders subtitle when provided", () => {
    render(<ServicesEstate items={ITEMS} subtitle="Land and luxury specialists." />);
    expect(screen.getByText("Land and luxury specialists.")).toBeInTheDocument();
  });

  it("renders all service item titles", () => {
    render(<ServicesEstate items={ITEMS} />);
    expect(screen.getByText("Estate Valuation")).toBeInTheDocument();
    expect(screen.getByText("Land Assessment")).toBeInTheDocument();
    expect(screen.getByText("Equestrian Properties")).toBeInTheDocument();
  });

  it("renders all service item descriptions", () => {
    render(<ServicesEstate items={ITEMS} />);
    expect(
      screen.getByText("Comprehensive market analysis for luxury estates.")
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "Expert evaluation of acreage, soil, and development potential."
      )
    ).toBeInTheDocument();
  });

  it("renders section with id=features for anchor linking", () => {
    const { container } = render(<ServicesEstate items={ITEMS} />);
    expect(container.querySelector("#features")).toBeInTheDocument();
  });

  it("renders each service as an article element", () => {
    const { container } = render(<ServicesEstate items={ITEMS} />);
    const articles = container.querySelectorAll("article");
    expect(articles.length).toBe(3);
  });

  it("renders two-column layout (icon + text)", () => {
    const { container } = render(<ServicesEstate items={ITEMS} />);
    // Each article should have at least 2 children (icon area + text)
    const articles = container.querySelectorAll("article");
    expect(articles[0].children.length).toBeGreaterThanOrEqual(2);
  });

  it("applies hover lift on mouse enter", () => {
    const { container } = render(<ServicesEstate items={ITEMS} />);
    const card = container.querySelectorAll("article")[0] as HTMLElement;
    fireEvent.mouseEnter(card);
    expect(card.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(card);
    expect(card.style.transform).toBe("none");
  });

  it("renders earthy card background on articles", () => {
    const { container } = render(<ServicesEstate items={ITEMS} />);
    const article = container.querySelector("article");
    expect(article?.style.background).toBeTruthy();
  });
});
