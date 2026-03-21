/**
 * @vitest-environment jsdom
 */
import { vi, describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";

const mockRequestSubscribe = vi.fn();

vi.mock("@/actions/privacy", () => ({
  requestSubscribe: (...args: unknown[]) => mockRequestSubscribe(...args),
}));

import { SubscribeForm } from "@/components/privacy/SubscribeForm";

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
});
