/**
 * @vitest-environment jsdom
 */
import { vi, describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";

const mockRequestOptOut = vi.fn();

vi.mock("@/actions/privacy", () => ({
  requestOptOut: (...args: unknown[]) => mockRequestOptOut(...args),
}));

import { OptOutForm } from "@/components/privacy/OptOutForm";

describe("OptOutForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the confirm button and email", () => {
    render(<OptOutForm agentId="test" email="user@example.com" token="abc" />);
    expect(screen.getByRole("button", { name: /confirm opt out/i })).toBeInTheDocument();
    expect(screen.getByText("user@example.com")).toBeInTheDocument();
  });

  it("shows success state after successful opt-out", async () => {
    mockRequestOptOut.mockResolvedValue({ ok: true });
    render(<OptOutForm agentId="test" email="user@example.com" token="abc" />);
    fireEvent.click(screen.getByRole("button", { name: /confirm opt out/i }));
    await waitFor(() => {
      expect(screen.getByRole("status")).toBeInTheDocument();
    });
    expect(screen.getByText(/successfully opted out/i)).toBeInTheDocument();
  });

  it("shows error message from result when opt-out fails with error", async () => {
    mockRequestOptOut.mockResolvedValue({ ok: false, error: "Token expired" });
    render(<OptOutForm agentId="test" email="user@example.com" token="abc" />);
    fireEvent.click(screen.getByRole("button", { name: /confirm opt out/i }));
    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
    expect(screen.getByText("Token expired")).toBeInTheDocument();
  });

  it("shows fallback error message when result.error is undefined", async () => {
    mockRequestOptOut.mockResolvedValue({ ok: false });
    render(<OptOutForm agentId="test" email="user@example.com" token="abc" />);
    fireEvent.click(screen.getByRole("button", { name: /confirm opt out/i }));
    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
    expect(screen.getByText("Something went wrong. Please try again.")).toBeInTheDocument();
  });

  it("shows loading text while processing", async () => {
    let resolvePromise: (value: { ok: boolean }) => void;
    mockRequestOptOut.mockReturnValue(new Promise((resolve) => { resolvePromise = resolve; }));
    render(<OptOutForm agentId="test" email="user@example.com" token="abc" />);
    fireEvent.click(screen.getByRole("button", { name: /confirm opt out/i }));
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /processing/i })).toBeDisabled();
    });
    resolvePromise!({ ok: true });
  });

  it("renders without email display when email is empty", () => {
    render(<OptOutForm agentId="test" email="" token="abc" />);
    expect(screen.queryByText(/opting out email/i)).not.toBeInTheDocument();
  });
});
