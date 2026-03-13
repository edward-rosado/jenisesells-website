import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
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

  it("shows verifying state then success when payment=success and server confirms", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      headers: new Headers({ "content-type": "application/json" }),
      json: () => Promise.resolve({ state: "TrialActivated" }),
    });
    mockSearchParams = new URLSearchParams("payment=success&session_id=cs_test_123");
    render(<OnboardPage />);
    // Initially shows verifying state
    expect(screen.getByText(/Verifying payment/i)).toBeInTheDocument();
    // After server responds, shows success
    await waitFor(() => {
      expect(screen.getByText(/Trial Activated/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/7-day free trial has started/i)).toBeInTheDocument();
  });

  it("shows cancelled state when payment=cancelled query param present", () => {
    mockSearchParams = new URLSearchParams("payment=cancelled");
    render(<OnboardPage />);
    expect(screen.getByText(/Payment Cancelled/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /try again/i })).toHaveAttribute("href", "/onboard");
  });

  // ---- Additional onboard page branch coverage ----

  it("shows payment not confirmed when server returns non-TrialActivated state", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      headers: new Headers({ "content-type": "application/json" }),
      json: () => Promise.resolve({ state: "Pending" }),
    });
    mockSearchParams = new URLSearchParams("payment=success&session_id=cs_test_123");
    render(<OnboardPage />);
    // Initially shows verifying state
    expect(screen.getByText(/Verifying payment/i)).toBeInTheDocument();
    // After server responds with non-TrialActivated, shows failure
    await waitFor(() => {
      expect(screen.getByText(/Payment Not Confirmed/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/could not verify your payment/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /try again/i })).toHaveAttribute("href", "/onboard");
  });

  it("shows payment not confirmed when verify fetch throws", async () => {
    global.fetch = vi.fn().mockRejectedValue(new Error("Network error"));
    mockSearchParams = new URLSearchParams("payment=success&session_id=cs_test_123");
    render(<OnboardPage />);
    await waitFor(() => {
      expect(screen.getByText(/Payment Not Confirmed/i)).toBeInTheDocument();
    });
  });

  it("shows payment not confirmed when verify fetch returns non-ok", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      headers: new Headers({ "content-type": "application/json" }),
    });
    mockSearchParams = new URLSearchParams("payment=success&session_id=cs_test_123");
    render(<OnboardPage />);
    await waitFor(() => {
      expect(screen.getByText(/Payment Not Confirmed/i)).toBeInTheDocument();
    });
  });

  it("shows error message when createSession fetch fails with Error", async () => {
    global.fetch = vi.fn().mockRejectedValue(new Error("Failed to create session"));
    mockSearchParams = new URLSearchParams("profileUrl=https://zillow.com/profile/test");
    render(<OnboardPage />);
    await waitFor(() => {
      expect(screen.getByText("Failed to create session")).toBeInTheDocument();
    });
  });

  it("shows generic error when createSession fetch fails with non-Error", async () => {
    global.fetch = vi.fn().mockRejectedValue("string error");
    mockSearchParams = new URLSearchParams("profileUrl=https://zillow.com/profile/test");
    render(<OnboardPage />);
    await waitFor(() => {
      expect(screen.getByText("Something went wrong")).toBeInTheDocument();
    });
  });

  it("shows error when createSession fetch returns non-ok", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      headers: new Headers({ "content-type": "application/json" }),
    });
    mockSearchParams = new URLSearchParams("profileUrl=https://zillow.com/profile/test");
    render(<OnboardPage />);
    await waitFor(() => {
      expect(screen.getByText("Failed to create session")).toBeInTheDocument();
    });
  });

  it("does not create session when payment=success", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      headers: new Headers({ "content-type": "application/json" }),
      json: () => Promise.resolve({ state: "TrialActivated" }),
    });
    global.fetch = fetchMock;
    mockSearchParams = new URLSearchParams("payment=success&session_id=cs_test_123");
    render(<OnboardPage />);
    await waitFor(() => {
      expect(screen.getByText(/Trial Activated/i)).toBeInTheDocument();
    });
    // Only verifyPayment GET call, no POST createSession
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining("/onboard/cs_test_123"),
      expect.objectContaining({ method: "GET" })
    );
  });

  it("renders ChatWindow without autoMessage when no profileUrl", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      headers: new Headers({ "content-type": "application/json" }),
      json: () => Promise.resolve({ sessionId: "abc123", token: "test-token" }),
    });
    mockSearchParams = new URLSearchParams("");
    render(<OnboardPage />);
    await waitFor(() => {
      expect(screen.getByPlaceholderText(/type a message/i)).toBeInTheDocument();
    });
  });

  it("does not verify payment when payment=success but no session_id", () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      headers: new Headers({ "content-type": "application/json" }),
      json: () => Promise.resolve({ sessionId: "abc123", token: "test-token" }),
    });
    global.fetch = fetchMock;
    // payment=success but missing session_id — should skip verifyPayment and skip createSession
    mockSearchParams = new URLSearchParams("payment=success");
    render(<OnboardPage />);
    // Since payment=success, skip createSession. Since no session_id, skip verifyPayment.
    // paymentVerified stays null -> shows verifying state forever
    expect(screen.getByText(/Verifying payment/i)).toBeInTheDocument();
  });
});

describe("OnboardPage - Coming Soon", () => {
  const originalEnv = process.env.NEXT_PUBLIC_COMING_SOON;

  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    if (originalEnv === undefined) {
      delete process.env.NEXT_PUBLIC_COMING_SOON;
    } else {
      process.env.NEXT_PUBLIC_COMING_SOON = originalEnv;
    }
  });

  it("renders Coming Soon page when NEXT_PUBLIC_COMING_SOON is true", async () => {
    process.env.NEXT_PUBLIC_COMING_SOON = "true";
    const mod = await import("../app/onboard/page");
    const Page = mod.default;
    render(<Page />);
    expect(screen.getByRole("heading", { name: /coming soon/i })).toBeInTheDocument();
    expect(screen.getByText(/finishing touches/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /back to home/i })).toHaveAttribute("href", "/");
    expect(screen.getByText(/\$10\/mo after your website goes live/i)).toBeInTheDocument();
  });

  it("does not render Coming Soon when NEXT_PUBLIC_COMING_SOON is not set", async () => {
    delete process.env.NEXT_PUBLIC_COMING_SOON;
    const mod = await import("../app/onboard/page");
    const Page = mod.default;
    render(<Page />);
    // Should show loading/onboarding, not Coming Soon
    expect(screen.queryByRole("heading", { name: /coming soon/i })).not.toBeInTheDocument();
  });
});
