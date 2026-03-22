/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { LegalPageLayout } from "../src/LegalPageLayout";
import type { AccountBranding } from "../src/branding";

const BRANDING: AccountBranding = {
  primary_color: "#1B5E20",
  secondary_color: "#2E7D32",
  accent_color: "#C8A951",
  font_family: "Segoe UI",
};

const BRANDING_MINIMAL: AccountBranding = {};

describe("LegalPageLayout", () => {
  it("renders children (standard legal content)", () => {
    render(
      <LegalPageLayout branding={BRANDING} accountId="test-agent">
        <h1>Privacy Policy</h1>
      </LegalPageLayout>
    );
    expect(screen.getByRole("heading", { name: "Privacy Policy" })).toBeInTheDocument();
  });

  it("renders customAbove markdown before standard content", () => {
    render(
      <LegalPageLayout branding={BRANDING} accountId="test-agent" customAbove="## Custom Above">
        <p>Standard content</p>
      </LegalPageLayout>
    );
    expect(screen.getByRole("heading", { level: 2, name: "Custom Above" })).toBeInTheDocument();
    const customHeading = screen.getByText("Custom Above");
    const standard = screen.getByText("Standard content");
    expect(customHeading.compareDocumentPosition(standard) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("renders customBelow markdown after standard content", () => {
    render(
      <LegalPageLayout branding={BRANDING} accountId="test-agent" customBelow="## Custom Below">
        <p>Standard content</p>
      </LegalPageLayout>
    );
    expect(screen.getByRole("heading", { level: 2, name: "Custom Below" })).toBeInTheDocument();
    const standard = screen.getByText("Standard content");
    const customHeading = screen.getByText("Custom Below");
    expect(standard.compareDocumentPosition(customHeading) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("does not render custom sections when not provided", () => {
    render(
      <LegalPageLayout branding={BRANDING} accountId="test-agent">
        <p>Only standard</p>
      </LegalPageLayout>
    );
    expect(screen.getByText("Only standard")).toBeInTheDocument();
  });

  it("renders nav slot when provided", () => {
    render(
      <LegalPageLayout
        branding={BRANDING}
        accountId="test-agent"
        nav={<nav aria-label="Site navigation">Nav</nav>}
      >
        <p>Content</p>
      </LegalPageLayout>
    );
    expect(screen.getByRole("navigation", { name: "Site navigation" })).toBeInTheDocument();
  });

  it("renders footer slot when provided", () => {
    render(
      <LegalPageLayout
        branding={BRANDING}
        accountId="test-agent"
        footer={<footer aria-label="Site footer">Footer</footer>}
      >
        <p>Content</p>
      </LegalPageLayout>
    );
    expect(screen.getByRole("contentinfo")).toBeInTheDocument();
  });

  it("renders CookieConsentBanner", () => {
    render(
      <LegalPageLayout branding={BRANDING} accountId="test-agent">
        <p>Content</p>
      </LegalPageLayout>
    );
    expect(screen.getByRole("dialog", { name: "Cookie consent" })).toBeInTheDocument();
  });

  it("injects CSS variables from agent branding", () => {
    const { container } = render(
      <LegalPageLayout branding={BRANDING} accountId="test-agent">
        <p>Content</p>
      </LegalPageLayout>
    );
    const root = container.firstChild as HTMLElement;
    expect(root.style.getPropertyValue("--color-primary")).toBe("#1B5E20");
  });

  it("works with minimal branding config (default branding)", () => {
    render(
      <LegalPageLayout branding={BRANDING_MINIMAL} accountId="minimal-agent">
        <p>Content</p>
      </LegalPageLayout>
    );
    expect(screen.getByText("Content")).toBeInTheDocument();
  });

  it("renders without nav or footer slots", () => {
    render(
      <LegalPageLayout branding={BRANDING} accountId="test-agent">
        <p>Content</p>
      </LegalPageLayout>
    );
    expect(screen.getByText("Content")).toBeInTheDocument();
  });
});
