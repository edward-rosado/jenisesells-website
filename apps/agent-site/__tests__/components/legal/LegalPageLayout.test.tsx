/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { LegalPageLayout } from "@/components/legal/LegalPageLayout";
import { AGENT, AGENT_MINIMAL } from "../fixtures";

describe("LegalPageLayout", () => {
  it("renders children (standard legal content)", () => {
    render(
      <LegalPageLayout agent={AGENT} agentId="test-agent">
        <h1>Privacy Policy</h1>
      </LegalPageLayout>
    );
    expect(screen.getByRole("heading", { name: "Privacy Policy" })).toBeInTheDocument();
  });

  it("renders customAbove markdown before standard content", () => {
    render(
      <LegalPageLayout agent={AGENT} agentId="test-agent" customAbove="## Custom Above">
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
      <LegalPageLayout agent={AGENT} agentId="test-agent" customBelow="## Custom Below">
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
      <LegalPageLayout agent={AGENT} agentId="test-agent">
        <p>Only standard</p>
      </LegalPageLayout>
    );
    expect(screen.getByText("Only standard")).toBeInTheDocument();
  });

  it("renders Footer with legal links", () => {
    render(
      <LegalPageLayout agent={AGENT} agentId="test-agent">
        <p>Content</p>
      </LegalPageLayout>
    );
    expect(screen.getByRole("navigation", { name: "Legal links" })).toBeInTheDocument();
  });

  it("renders CookieConsentBanner", () => {
    render(
      <LegalPageLayout agent={AGENT} agentId="test-agent">
        <p>Content</p>
      </LegalPageLayout>
    );
    expect(screen.getByRole("dialog", { name: "Cookie consent" })).toBeInTheDocument();
  });

  it("injects CSS variables from agent branding", () => {
    const { container } = render(
      <LegalPageLayout agent={AGENT} agentId="test-agent">
        <p>Content</p>
      </LegalPageLayout>
    );
    const root = container.firstChild as HTMLElement;
    expect(root.style.getPropertyValue("--color-primary")).toBe("#1B5E20");
  });

  it("works with minimal agent config (default branding)", () => {
    render(
      <LegalPageLayout agent={AGENT_MINIMAL} agentId="minimal-agent">
        <p>Content</p>
      </LegalPageLayout>
    );
    expect(screen.getByText("Content")).toBeInTheDocument();
  });
});
