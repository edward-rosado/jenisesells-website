/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { renderToString } from "react-dom/server";
import { CookieConsentBanner } from "@/components/legal/CookieConsentBanner";

describe("CookieConsentBanner", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("renders the banner when no consent stored", () => {
    render(<CookieConsentBanner agentId="test-agent" />);
    expect(screen.getByRole("dialog", { name: "Cookie consent" })).toBeInTheDocument();
  });

  it("does not render when consent is already accepted", () => {
    localStorage.setItem("res-cookie-consent-test-agent", "accepted");
    render(<CookieConsentBanner agentId="test-agent" />);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("does not render when consent is already declined", () => {
    localStorage.setItem("res-cookie-consent-test-agent", "declined");
    render(<CookieConsentBanner agentId="test-agent" />);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("hides banner and stores accepted on accept click", () => {
    render(<CookieConsentBanner agentId="test-agent" />);
    fireEvent.click(screen.getByRole("button", { name: "Accept cookies" }));
    expect(localStorage.getItem("res-cookie-consent-test-agent")).toBe("accepted");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("hides banner and stores declined on decline click", () => {
    render(<CookieConsentBanner agentId="test-agent" />);
    fireEvent.click(screen.getByRole("button", { name: "Decline cookies" }));
    expect(localStorage.getItem("res-cookie-consent-test-agent")).toBe("declined");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("uses agent-specific localStorage key", () => {
    render(<CookieConsentBanner agentId="agent-abc" />);
    fireEvent.click(screen.getByRole("button", { name: "Accept cookies" }));
    expect(localStorage.getItem("res-cookie-consent-agent-abc")).toBe("accepted");
    expect(localStorage.getItem("res-cookie-consent-test-agent")).toBeNull();
  });

  it("links to /privacy with agentId", () => {
    render(<CookieConsentBanner agentId="test-agent" />);
    const link = screen.getByRole("link", { name: /privacy/i });
    expect(link).toHaveAttribute("href", "/privacy?agentId=test-agent");
  });

  it("renders accept and decline buttons", () => {
    render(<CookieConsentBanner agentId="test-agent" />);
    expect(screen.getByRole("button", { name: "Accept cookies" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Decline cookies" })).toBeInTheDocument();
  });

  it("server snapshot returns pending (SSR path)", () => {
    const html = renderToString(<CookieConsentBanner agentId="test-agent" />);
    expect(html).toBe("");
  });

  it("has aria-modal and aria-describedby on dialog", () => {
    render(<CookieConsentBanner agentId="test" />);
    const dialog = screen.getByRole("dialog");
    expect(dialog).toHaveAttribute("aria-modal", "true");
    expect(dialog).toHaveAttribute("aria-describedby", "cookie-desc");
  });
});
