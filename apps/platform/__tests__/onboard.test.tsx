import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import OnboardPage from "../app/onboard/page";

let mockSearchParams = new URLSearchParams("profileUrl=https://zillow.com/profile/test");

vi.mock("next/navigation", () => ({
  useSearchParams: () => mockSearchParams,
}));

beforeEach(() => {
  mockSearchParams = new URLSearchParams("profileUrl=https://zillow.com/profile/test");
  global.fetch = vi.fn().mockResolvedValue({
    ok: true,
    headers: new Headers({ "content-type": "application/json" }),
    json: () => Promise.resolve({ sessionId: "abc123", token: "test-token", response: "" }),
  });
});

describe("OnboardPage", () => {
  it("creates a session on mount", async () => {
    render(<OnboardPage />);
    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining("/onboard"),
        expect.objectContaining({ method: "POST" })
      );
    });
  });

  it("shows loading state initially", () => {
    render(<OnboardPage />);
    expect(screen.getByText(/Starting your onboarding/i)).toBeInTheDocument();
  });

  it("shows success state when payment=success query param present", () => {
    mockSearchParams = new URLSearchParams("payment=success&session_id=cs_test_123");
    render(<OnboardPage />);
    expect(screen.getByText(/Trial Activated/i)).toBeInTheDocument();
    expect(screen.getByText(/7-day free trial has started/i)).toBeInTheDocument();
  });

  it("shows cancelled state when payment=cancelled query param present", () => {
    mockSearchParams = new URLSearchParams("payment=cancelled");
    render(<OnboardPage />);
    expect(screen.getByText(/Payment Cancelled/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /try again/i })).toHaveAttribute("href", "/onboard");
  });
});
