/**
 * @vitest-environment jsdom
 */
import { vi, describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";

const mockRequestSubscribe = vi.fn();
const mockTrackFormEvent = vi.fn();

vi.mock("@/features/privacy/privacy", () => ({
  requestSubscribe: (...args: unknown[]) => mockRequestSubscribe(...args),
}));

vi.mock("@/features/shared/telemetry", () => ({
  trackFormEvent: (...args: unknown[]) => mockTrackFormEvent(...args),
  EventType: {
    Viewed: "Viewed",
    Started: "Started",
    Submitted: "Submitted",
    Succeeded: "Succeeded",
    Failed: "Failed",
  },
}));

import { SubscribeForm } from "@/features/privacy/SubscribeForm";

describe("SubscribeForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the confirm button and email", () => {
    render(<SubscribeForm agentId="test" email="user@example.com" token="abc" />);
    expect(screen.getByRole("button", { name: /confirm re-subscribe/i })).toBeInTheDocument();
    expect(screen.getByText("user@example.com")).toBeInTheDocument();
  });

  it("shows success state after successful re-subscribe", async () => {
    mockRequestSubscribe.mockResolvedValue({ ok: true });
    render(<SubscribeForm agentId="test" email="user@example.com" token="abc" />);
    fireEvent.click(screen.getByRole("button", { name: /confirm re-subscribe/i }));
    await waitFor(() => {
      expect(screen.getByRole("status")).toBeInTheDocument();
    });
    expect(screen.getByText(/successfully re-subscribed/i)).toBeInTheDocument();
  });

  it("shows error message from result when subscribe fails with error", async () => {
    mockRequestSubscribe.mockResolvedValue({ ok: false, error: "Token expired" });
    render(<SubscribeForm agentId="test" email="user@example.com" token="abc" />);
    fireEvent.click(screen.getByRole("button", { name: /confirm re-subscribe/i }));
    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
    expect(screen.getByText("Token expired")).toBeInTheDocument();
  });

  it("shows fallback error message when result.error is undefined", async () => {
    mockRequestSubscribe.mockResolvedValue({ ok: false });
    render(<SubscribeForm agentId="test" email="user@example.com" token="abc" />);
    fireEvent.click(screen.getByRole("button", { name: /confirm re-subscribe/i }));
    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
    expect(screen.getByText("Something went wrong. Please try again.")).toBeInTheDocument();
  });

  it("shows loading text while processing", async () => {
    let resolvePromise: (value: { ok: boolean }) => void;
    mockRequestSubscribe.mockReturnValue(new Promise((resolve) => { resolvePromise = resolve; }));
    render(<SubscribeForm agentId="test" email="user@example.com" token="abc" />);
    fireEvent.click(screen.getByRole("button", { name: /confirm re-subscribe/i }));
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /processing/i })).toBeDisabled();
    });
    resolvePromise!({ ok: true });
  });

  it("renders without email display when email is empty", () => {
    render(<SubscribeForm agentId="test" email="" token="abc" />);
    expect(screen.queryByText(/re-subscribing email/i)).not.toBeInTheDocument();
  });

  it("fires Viewed event on mount", () => {
    render(<SubscribeForm agentId="test" email="user@example.com" token="abc" />);
    expect(mockTrackFormEvent).toHaveBeenCalledWith("Viewed", "test");
  });

  it("fires Started and Submitted events when button is clicked", async () => {
    mockRequestSubscribe.mockResolvedValue({ ok: true });
    render(<SubscribeForm agentId="test" email="user@example.com" token="abc" />);
    fireEvent.click(screen.getByRole("button", { name: /confirm re-subscribe/i }));
    await waitFor(() => expect(screen.getByRole("status")).toBeInTheDocument());
    expect(mockTrackFormEvent).toHaveBeenCalledWith("Started", "test");
    expect(mockTrackFormEvent).toHaveBeenCalledWith("Submitted", "test");
  });

  it("fires Succeeded event on successful re-subscribe", async () => {
    mockRequestSubscribe.mockResolvedValue({ ok: true });
    render(<SubscribeForm agentId="test" email="user@example.com" token="abc" />);
    fireEvent.click(screen.getByRole("button", { name: /confirm re-subscribe/i }));
    await waitFor(() => expect(screen.getByRole("status")).toBeInTheDocument());
    expect(mockTrackFormEvent).toHaveBeenCalledWith("Succeeded", "test");
  });

  it("fires Failed event on failed re-subscribe", async () => {
    mockRequestSubscribe.mockResolvedValue({ ok: false, error: "Token expired" });
    render(<SubscribeForm agentId="test" email="user@example.com" token="abc" />);
    fireEvent.click(screen.getByRole("button", { name: /confirm re-subscribe/i }));
    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(mockTrackFormEvent).toHaveBeenCalledWith("Failed", "test", "server_error");
  });
});
