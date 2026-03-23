/**
 * @vitest-environment jsdom
 */
import { vi, describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";

const mockRequestExport = vi.fn();
const mockTrackFormEvent = vi.fn();

vi.mock("@/features/privacy/privacy", () => ({
  requestExport: (...args: unknown[]) => mockRequestExport(...args),
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

import { MyDataForm } from "@/features/privacy/MyDataForm";

const DEFAULT_PROPS = {
  agentId: "test",
  initialEmail: "user@example.com",
  privacyHref: "/privacy",
};

describe("MyDataForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the email input and submit button", () => {
    render(<MyDataForm {...DEFAULT_PROPS} />);
    expect(screen.getByLabelText(/email address/i)).toHaveValue("user@example.com");
    expect(screen.getByRole("button", { name: /request my data/i })).toBeInTheDocument();
  });

  it("renders the back-to-privacy link", () => {
    render(<MyDataForm {...DEFAULT_PROPS} privacyHref="/privacy-policy" />);
    const link = screen.getByRole("link", { name: /back to privacy policy/i });
    expect(link).toHaveAttribute("href", "/privacy-policy");
  });

  it("updates the email field when the user types", () => {
    render(<MyDataForm {...DEFAULT_PROPS} initialEmail="" />);
    const input = screen.getByLabelText(/email address/i);
    fireEvent.change(input, { target: { value: "new@example.com" } });
    expect(input).toHaveValue("new@example.com");
  });

  it("disables submit button when email is empty", () => {
    render(<MyDataForm {...DEFAULT_PROPS} initialEmail="" />);
    expect(screen.getByRole("button", { name: /request my data/i })).toBeDisabled();
  });

  it("does not submit when email is whitespace only", () => {
    render(<MyDataForm {...DEFAULT_PROPS} initialEmail="   " />);
    const form = screen.getByRole("button", { name: /request my data/i }).closest("form")!;
    fireEvent.submit(form);
    expect(mockRequestExport).not.toHaveBeenCalled();
  });

  it("shows not-found state when result has no data", async () => {
    mockRequestExport.mockResolvedValue({ ok: true, data: [] });
    render(<MyDataForm {...DEFAULT_PROPS} />);
    fireEvent.submit(screen.getByRole("button", { name: /request my data/i }));
    await waitFor(() => {
      expect(screen.getByRole("status")).toBeInTheDocument();
    });
    expect(screen.getByText(/no data found/i)).toBeInTheDocument();
  });

  it("shows data when result has records", async () => {
    mockRequestExport.mockResolvedValue({
      ok: true,
      data: [{ email: "user@example.com", name: "Jane" }],
    });
    render(<MyDataForm {...DEFAULT_PROPS} />);
    fireEvent.submit(screen.getByRole("button", { name: /request my data/i }));
    await waitFor(() => {
      expect(screen.getByRole("status")).toBeInTheDocument();
    });
    expect(screen.getByText("Jane")).toBeInTheDocument();
    expect(screen.getByText("user@example.com")).toBeInTheDocument();
  });

  it("shows error message when request fails", async () => {
    mockRequestExport.mockResolvedValue({ ok: false, error: "Not authorized" });
    render(<MyDataForm {...DEFAULT_PROPS} />);
    fireEvent.submit(screen.getByRole("button", { name: /request my data/i }));
    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
    expect(screen.getByText("Not authorized")).toBeInTheDocument();
  });

  it("shows fallback error message when result.error is undefined", async () => {
    mockRequestExport.mockResolvedValue({ ok: false });
    render(<MyDataForm {...DEFAULT_PROPS} />);
    fireEvent.submit(screen.getByRole("button", { name: /request my data/i }));
    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
    expect(screen.getByText("Something went wrong. Please try again.")).toBeInTheDocument();
  });

  it("shows loading text while submitting", async () => {
    let resolvePromise: (value: { ok: boolean; data: [] }) => void;
    mockRequestExport.mockReturnValue(new Promise((resolve) => { resolvePromise = resolve; }));
    render(<MyDataForm {...DEFAULT_PROPS} />);
    fireEvent.submit(screen.getByRole("button", { name: /request my data/i }));
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /loading/i })).toBeDisabled();
    });
    resolvePromise!({ ok: true, data: [] });
  });

  it("fires Viewed event on mount", () => {
    render(<MyDataForm {...DEFAULT_PROPS} />);
    expect(mockTrackFormEvent).toHaveBeenCalledWith("Viewed", "test");
  });

  it("fires Started event on first input focus", () => {
    render(<MyDataForm {...DEFAULT_PROPS} />);
    fireEvent.focus(screen.getByLabelText(/email address/i));
    expect(mockTrackFormEvent).toHaveBeenCalledWith("Started", "test");
  });

  it("fires Started only once on multiple focus events", () => {
    render(<MyDataForm {...DEFAULT_PROPS} />);
    const input = screen.getByLabelText(/email address/i);
    fireEvent.focus(input);
    fireEvent.focus(input);
    const startedCalls = mockTrackFormEvent.mock.calls.filter((c) => c[0] === "Started");
    expect(startedCalls).toHaveLength(1);
  });

  it("fires Submitted event when form is submitted", async () => {
    mockRequestExport.mockResolvedValue({ ok: true, data: [] });
    render(<MyDataForm {...DEFAULT_PROPS} />);
    fireEvent.submit(screen.getByRole("button", { name: /request my data/i }));
    await waitFor(() => expect(screen.getByRole("status")).toBeInTheDocument());
    expect(mockTrackFormEvent).toHaveBeenCalledWith("Submitted", "test");
  });

  it("fires Succeeded event on successful response", async () => {
    mockRequestExport.mockResolvedValue({ ok: true, data: [] });
    render(<MyDataForm {...DEFAULT_PROPS} />);
    fireEvent.submit(screen.getByRole("button", { name: /request my data/i }));
    await waitFor(() => expect(screen.getByRole("status")).toBeInTheDocument());
    expect(mockTrackFormEvent).toHaveBeenCalledWith("Succeeded", "test");
  });

  it("fires Failed event on failed request", async () => {
    mockRequestExport.mockResolvedValue({ ok: false, error: "error" });
    render(<MyDataForm {...DEFAULT_PROPS} />);
    fireEvent.submit(screen.getByRole("button", { name: /request my data/i }));
    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(mockTrackFormEvent).toHaveBeenCalledWith("Failed", "test", "server_error");
  });
});
