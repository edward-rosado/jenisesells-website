import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import OnboardPage from "../app/onboard/page";

let mockSearchParams = new URLSearchParams("profileUrl=https://zillow.com/profile/test");

vi.mock("next/navigation", () => ({
  useSearchParams: () => mockSearchParams,
}));

const mockGet = vi.fn();
const mockPost = vi.fn();

vi.mock("@/lib/api", () => ({
  api: {
    GET: (...args: unknown[]) => mockGet(...args),
    POST: (...args: unknown[]) => mockPost(...args),
  },
}));

beforeEach(() => {
  mockSearchParams = new URLSearchParams("profileUrl=https://zillow.com/profile/test");
  mockGet.mockClear();
  mockPost.mockClear();
  mockGet.mockResolvedValue({ data: undefined, error: undefined, response: { ok: true, status: 200 } });
  mockPost.mockResolvedValue({
    data: { sessionId: "abc123", token: "test-token" },
    error: undefined,
    response: { ok: true, status: 200 },
  });
});

describe("OnboardPage", () => {
  it("creates a session on mount", async () => {
    render(<OnboardPage />);
    await waitFor(() => {
      expect(mockPost).toHaveBeenCalled();
    });
  });

  it("shows loading state initially", () => {
    mockPost.mockReturnValue(new Promise(() => {}));
    render(<OnboardPage />);
    expect(screen.getByText(/Starting your onboarding/i)).toBeInTheDocument();
  });

  it("shows verifying state then success when payment=success and server confirms", async () => {
    mockGet.mockResolvedValue({
      data: { state: "TrialActivated" },
      error: undefined,
      response: { ok: true, status: 200 },
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
    mockGet.mockResolvedValue({
      data: { state: "Pending" },
      error: undefined,
      response: { ok: true, status: 200 },
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
    mockGet.mockRejectedValue(new Error("Network error"));
    mockSearchParams = new URLSearchParams("payment=success&session_id=cs_test_123");
    render(<OnboardPage />);
    await waitFor(() => {
      expect(screen.getByText(/Payment Not Confirmed/i)).toBeInTheDocument();
    });
  });

  it("shows payment not confirmed when verify fetch returns error", async () => {
    mockGet.mockResolvedValue({
      data: undefined,
      error: { message: "Service unavailable" },
      response: { ok: false, status: 503 },
    });
    mockSearchParams = new URLSearchParams("payment=success&session_id=cs_test_123");
    render(<OnboardPage />);
    await waitFor(() => {
      expect(screen.getByText(/Payment Not Confirmed/i)).toBeInTheDocument();
    });
  });

  it("shows error message when createSession fetch fails with Error", async () => {
    mockPost.mockRejectedValue(new Error("Failed to create session"));
    mockSearchParams = new URLSearchParams("profileUrl=https://zillow.com/profile/test");
    render(<OnboardPage />);
    await waitFor(() => {
      expect(screen.getByText("Failed to create session")).toBeInTheDocument();
    });
  });

  it("shows generic error when createSession fetch fails with non-Error", async () => {
    mockPost.mockRejectedValue("string error");
    mockSearchParams = new URLSearchParams("profileUrl=https://zillow.com/profile/test");
    render(<OnboardPage />);
    await waitFor(() => {
      expect(screen.getByText("Something went wrong")).toBeInTheDocument();
    });
  });

  it("shows error when createSession returns error", async () => {
    mockPost.mockResolvedValue({
      data: undefined,
      error: { message: "Internal Server Error" },
      response: { ok: false, status: 500 },
    });
    mockSearchParams = new URLSearchParams("profileUrl=https://zillow.com/profile/test");
    render(<OnboardPage />);
    await waitFor(() => {
      expect(screen.getByText("Failed to create session")).toBeInTheDocument();
    });
  });

  it("does not create session when payment=success", async () => {
    mockGet.mockResolvedValue({
      data: { state: "TrialActivated" },
      error: undefined,
      response: { ok: true, status: 200 },
    });
    mockSearchParams = new URLSearchParams("payment=success&session_id=cs_test_123");
    render(<OnboardPage />);
    await waitFor(() => {
      expect(screen.getByText(/Trial Activated/i)).toBeInTheDocument();
    });
    // Only verifyPayment GET call, no POST createSession
    expect(mockGet).toHaveBeenCalledTimes(1);
    expect(mockPost).not.toHaveBeenCalled();
  });

  it("renders ChatWindow without autoMessage when no profileUrl", async () => {
    mockPost.mockResolvedValue({
      data: { sessionId: "abc123", token: "test-token" },
      error: undefined,
      response: { ok: true, status: 200 },
    });
    mockSearchParams = new URLSearchParams("");
    render(<OnboardPage />);
    await waitFor(() => {
      expect(screen.getByPlaceholderText(/type a message/i)).toBeInTheDocument();
    });
  });

  it("does not verify payment when payment=success but no session_id", () => {
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
    expect(screen.getByText(/14 days free\. \$14\.99\/mo after\./i)).toBeInTheDocument();
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
