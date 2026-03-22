/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { AboutCoastal } from "@/components/sections/about/AboutCoastal";
import { ACCOUNT } from "../fixtures";
import type { AboutData } from "@/features/config/types";

const ACCOUNT_WITH_PHOTO = {
  ...ACCOUNT,
  agent: { ...ACCOUNT.agent!, headshot_url: "/agents/test-coastal/headshot.jpg" },
};

const ABOUT_DATA: AboutData = {
  title: "About Maya",
  bio: "Maya Torres is a coastal real estate expert with 15 years of Outer Banks experience.",
  credentials: ["NC Licensed Broker", "300+ Beach Sales", "OBX Specialist"],
};

describe("AboutCoastal", () => {
  it("renders the section heading using data.title", () => {
    render(<AboutCoastal agent={ACCOUNT} data={ABOUT_DATA} />);
    expect(screen.getByRole("heading", { level: 2, name: "About Maya" })).toBeInTheDocument();
  });

  it("falls back to 'About {name}' when data.title is absent", () => {
    const dataNoTitle: AboutData = { ...ABOUT_DATA, title: undefined };
    render(<AboutCoastal agent={ACCOUNT} data={dataNoTitle} />);
    expect(screen.getByRole("heading", { level: 2 }).textContent).toMatch(/About Jane Smith/);
  });

  it("renders the bio text", () => {
    render(<AboutCoastal agent={ACCOUNT} data={ABOUT_DATA} />);
    expect(screen.getByText(/Maya Torres is a coastal real estate expert/)).toBeInTheDocument();
  });

  it("renders credentials as teal pills", () => {
    render(<AboutCoastal agent={ACCOUNT} data={ABOUT_DATA} />);
    expect(screen.getByText("NC Licensed Broker")).toBeInTheDocument();
    expect(screen.getByText("300+ Beach Sales")).toBeInTheDocument();
    expect(screen.getByText("OBX Specialist")).toBeInTheDocument();
  });

  it("credential pills have teal styling", () => {
    render(<AboutCoastal agent={ACCOUNT} data={ABOUT_DATA} />);
    const pill = screen.getByText("NC Licensed Broker");
    expect(pill.tagName.toLowerCase()).toBe("li");
    expect((pill as HTMLElement).style.background).toMatch(/var\(--color-primary|#2c7a7b/);
  });

  it("renders agent headshot when headshot_url is provided", () => {
    render(<AboutCoastal agent={ACCOUNT_WITH_PHOTO} data={ABOUT_DATA} />);
    const img = screen.getByRole("img");
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("alt", "Photo of Jane Smith");
  });

  it("does not render agent photo when headshot_url is absent", () => {
    const accountNoPhoto = {
      ...ACCOUNT,
      agent: { ...ACCOUNT.agent!, headshot_url: undefined },
    };
    render(<AboutCoastal agent={accountNoPhoto} data={ABOUT_DATA} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("agent photo has teal border", () => {
    const { container } = render(<AboutCoastal agent={ACCOUNT_WITH_PHOTO} data={ABOUT_DATA} />);
    const photoWrapper = container.querySelector("div[style*='border']");
    expect(photoWrapper).toBeInTheDocument();
    expect((photoWrapper as HTMLElement).style.border).toMatch(/2c7a7b|var\(--color-primary/);
  });

  it("has id=about for anchor linking", () => {
    const { container } = render(<AboutCoastal agent={ACCOUNT} data={ABOUT_DATA} />);
    expect(container.querySelector("#about")).toBeInTheDocument();
  });

  it("agent photo has rounded rectangle shape (borderRadius 16px)", () => {
    const { container } = render(<AboutCoastal agent={ACCOUNT_WITH_PHOTO} data={ABOUT_DATA} />);
    const photoWrapper = container.querySelector("div[style*='border-radius: 16px']");
    expect(photoWrapper).toBeInTheDocument();
  });

  it("renders bio as array of paragraphs when bio is an array", () => {
    const dataArrayBio: AboutData = {
      ...ABOUT_DATA,
      bio: ["First paragraph.", "Second paragraph."],
    };
    render(<AboutCoastal agent={ACCOUNT} data={dataArrayBio} />);
    expect(screen.getByText("First paragraph.")).toBeInTheDocument();
    expect(screen.getByText("Second paragraph.")).toBeInTheDocument();
  });
});
