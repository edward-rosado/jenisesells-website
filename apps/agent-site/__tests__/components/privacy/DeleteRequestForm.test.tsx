/**
 * @vitest-environment jsdom
 */
import { vi, describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";

const mockRequestDeletion = vi.fn();

vi.mock("@/actions/privacy", () => ({
  requestDeletion: (...args: unknown[]) => mockRequestDeletion(...args),
}));

import { DeleteRequestForm } from "@/components/privacy/DeleteRequestForm";

describe("DeleteRequestForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the form with initial email", () => {
    render(<DeleteRequestForm agentId="test" initialEmail="user@example.com" />);
    expect(screen.getByLabelText(/email address/i)).toHaveValue("user@example.com");
    expect(screen.getByRole("button", { name: /submit deletion request/i })).toBeInTheDocument();
  });

  it("updates the email field when the user types", () => {
    render(<DeleteRequestForm agentId="test" initialEmail="" />);
    const input = screen.getByLabelText(/email address/i);
    fireEvent.change(input, { target: { value: "new@example.com" } });
    expect(input).toHaveValue("new@example.com");
  });

  it("disables submit button when email is empty", () => {
    render(<DeleteRequestForm agentId="test" initialEmail="" />);
    expect(screen.getByRole("button", { name: /submit deletion request/i })).toBeDisabled();
  });

  it("does not submit when email is whitespace only", async () => {
    render(<DeleteRequestForm agentId="test" initialEmail="   " />);
    // Use fireEvent.submit on the form element directly to bypass disabled button
    const form = screen.getByRole("button", { name: /submit deletion request/i }).closest("form")!;
    fireEvent.submit(form);
    expect(mockRequestDeletion).not.toHaveBeenCalled();
  });

  it("trims email before submitting", async () => {
    mockRequestDeletion.mockResolvedValue({ ok: true });
    render(<DeleteRequestForm agentId="test" initialEmail="  user@example.com  " />);
    fireEvent.submit(screen.getByRole("button", { name: /submit deletion request/i }));
    await waitFor(() => {
      expect(mockRequestDeletion).toHaveBeenCalledWith("test", "user@example.com");
    });
  });

  it("shows success state after successful submission", async () => {
    mockRequestDeletion.mockResolvedValue({ ok: true });
    render(<DeleteRequestForm agentId="test" initialEmail="user@example.com" />);
    fireEvent.submit(screen.getByRole("button", { name: /submit deletion request/i }));
    await waitFor(() => {
      expect(screen.getByRole("status")).toBeInTheDocument();
    });
    expect(screen.getByText(/check your email for a verification link/i)).toBeInTheDocument();
    expect(screen.getByText("user@example.com")).toBeInTheDocument();
  });

  it("shows error message from result when submission fails with error", async () => {
    mockRequestDeletion.mockResolvedValue({ ok: false, error: "Custom error" });
    render(<DeleteRequestForm agentId="test" initialEmail="user@example.com" />);
    fireEvent.submit(screen.getByRole("button", { name: /submit deletion request/i }));
    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
    expect(screen.getByText("Custom error")).toBeInTheDocument();
  });

  it("shows fallback error message when result.error is undefined", async () => {
    mockRequestDeletion.mockResolvedValue({ ok: false });
    render(<DeleteRequestForm agentId="test" initialEmail="user@example.com" />);
    fireEvent.submit(screen.getByRole("button", { name: /submit deletion request/i }));
    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
    expect(screen.getByText("Something went wrong. Please try again.")).toBeInTheDocument();
  });

  it("shows loading text while submitting", async () => {
    let resolvePromise: (value: { ok: boolean }) => void;
    mockRequestDeletion.mockReturnValue(new Promise((resolve) => { resolvePromise = resolve; }));
    render(<DeleteRequestForm agentId="test" initialEmail="user@example.com" />);
    fireEvent.submit(screen.getByRole("button", { name: /submit deletion request/i }));
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /submitting/i })).toBeDisabled();
    });
    resolvePromise!({ ok: true });
  });
});
