import { describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { renderToString } from "react-dom/server";
import { CookieConsent } from "../src/CookieConsent";

describe("CookieConsent", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("returns null when no measurementId is provided", () => {
    const { container } = render(<CookieConsent />);
    expect(container.innerHTML).toBe("");
  });

  it("shows the banner when measurementId is provided and no consent stored", () => {
    render(<CookieConsent measurementId="G-ABC123" />);
    expect(screen.getByRole("dialog", { name: "Analytics consent" })).toBeInTheDocument();
  });

  it("hides the banner when consent is already granted", () => {
    localStorage.setItem("analytics-consent", "granted");
    render(<CookieConsent measurementId="G-ABC123" />);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("hides the banner when consent is already denied", () => {
    localStorage.setItem("analytics-consent", "denied");
    render(<CookieConsent measurementId="G-ABC123" />);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("sets analytics-consent to granted and hides banner on accept", () => {
    render(<CookieConsent measurementId="G-ABC123" />);
    fireEvent.click(screen.getByRole("button", { name: "Accept analytics" }));
    expect(localStorage.getItem("analytics-consent")).toBe("granted");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("sets analytics-consent to denied and hides banner on decline", () => {
    render(<CookieConsent measurementId="G-ABC123" />);
    fireEvent.click(screen.getByRole("button", { name: "Decline analytics" }));
    expect(localStorage.getItem("analytics-consent")).toBe("denied");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("renders accept and decline buttons", () => {
    render(<CookieConsent measurementId="G-ABC123" />);
    expect(screen.getByRole("button", { name: "Accept analytics" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Decline analytics" })).toBeInTheDocument();
  });

  it("server snapshot returns empty string (SSR path)", () => {
    const html = renderToString(<CookieConsent measurementId="G-ABC123" />);
    expect(html).toBe("");
  });
});
