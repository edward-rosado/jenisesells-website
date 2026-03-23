/**
 * @vitest-environment jsdom
 */
import { vi, describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ACCOUNT } from "../components/fixtures";

const mockLoadAccountConfig = vi.fn();
const mockRequestSubscribe = vi.fn();

vi.mock("@/features/config/config", () => ({
  loadAccountConfig: (...args: unknown[]) => mockLoadAccountConfig(...args),
}));

vi.mock("@/features/privacy/privacy", () => ({
  requestSubscribe: (...args: unknown[]) => mockRequestSubscribe(...args),
}));

vi.mock("@/features/shared/telemetry", () => ({
  trackFormEvent: vi.fn(),
  EventType: {
    Viewed: "Viewed",
    Started: "Started",
    Submitted: "Submitted",
    Succeeded: "Succeeded",
    Failed: "Failed",
  },
}));

import SubscribePage, { generateMetadata } from "@/app/[handle]/privacy/subscribe/page";

describe("generateMetadata (subscribe)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLoadAccountConfig.mockReturnValue(ACCOUNT);
  });

  it("returns title with agent name when config loads", async () => {
    const meta = await generateMetadata({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({}),
    });
    expect(meta.title).toBe("Re-subscribe | Jane Smith");
  });

  it("returns fallback title when config fails", async () => {
    mockLoadAccountConfig.mockImplementation(() => { throw new Error("fail"); });
    const meta = await generateMetadata({
      params: Promise.resolve({ handle: "bad-agent" }),
      searchParams: Promise.resolve({}),
    });
    expect(meta.title).toBe("Re-subscribe");
  });
});

describe("SubscribePage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLoadAccountConfig.mockReturnValue(ACCOUNT);
    mockRequestSubscribe.mockResolvedValue({ ok: true });
  });

  it("renders re-subscribe confirmation heading", async () => {
    const page = await SubscribePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Re-subscribe/i })).toBeInTheDocument();
  });

  it("shows re-subscribe confirmation prompt with agent name", async () => {
    const page = await SubscribePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);
    expect(screen.getByText(/Jane Smith/)).toBeInTheDocument();
    expect(screen.getAllByText(/re-subscribe/i).length).toBeGreaterThanOrEqual(1);
  });

  it("shows email from query params", async () => {
    const page = await SubscribePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);
    expect(screen.getByText(/user@example\.com/)).toBeInTheDocument();
  });

  it("renders confirm re-subscribe button", async () => {
    const page = await SubscribePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);
    expect(screen.getByRole("button", { name: /Confirm Re-subscribe/i })).toBeInTheDocument();
  });

  it("calls requestSubscribe server action when confirm button clicked", async () => {
    const page = await SubscribePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Confirm Re-subscribe/i }));

    await waitFor(() => {
      expect(mockRequestSubscribe).toHaveBeenCalledWith("test-agent", "user@example.com", "abc123");
    });
  });

  it("shows success message after successful re-subscribe", async () => {
    mockRequestSubscribe.mockResolvedValue({ ok: true });
    const page = await SubscribePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Confirm Re-subscribe/i }));

    await waitFor(() => {
      expect(screen.getByText(/successfully re-subscribed/i)).toBeInTheDocument();
    });
  });

  it("shows error message when requestSubscribe fails", async () => {
    mockRequestSubscribe.mockResolvedValue({ ok: false, error: "Something went wrong. Please try again." });
    const page = await SubscribePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Confirm Re-subscribe/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
      expect(screen.getByText(/Something went wrong/i)).toBeInTheDocument();
    });
  });

  it("shows fallback error message when result.error is undefined", async () => {
    mockRequestSubscribe.mockResolvedValue({ ok: false });
    const page = await SubscribePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Confirm Re-subscribe/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
      expect(screen.getByText(/Something went wrong/i)).toBeInTheDocument();
    });
  });

  it("uses fallback agent name when config fails", async () => {
    mockLoadAccountConfig.mockImplementation(() => { throw new Error("fail"); });
    const page = await SubscribePage({
      params: Promise.resolve({ handle: "bad-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);
    expect(screen.getByText(/your agent/i)).toBeInTheDocument();
  });
});
