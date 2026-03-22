/**
 * @vitest-environment jsdom
 */
import { vi, describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ACCOUNT } from "../components/fixtures";

const mockLoadAccountConfig = vi.fn();
const mockRequestOptOut = vi.fn();

vi.mock("@/features/config/config", () => ({
  loadAccountConfig: (...args: unknown[]) => mockLoadAccountConfig(...args),
}));

vi.mock("@/features/privacy/privacy", () => ({
  requestOptOut: (...args: unknown[]) => mockRequestOptOut(...args),
}));

import OptOutPage, { generateMetadata } from "@/app/[handle]/privacy/opt-out/page";

describe("generateMetadata (opt-out)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLoadAccountConfig.mockReturnValue(ACCOUNT);
  });

  it("returns title with agent name when config loads", async () => {
    const meta = await generateMetadata({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({}),
    });
    expect(meta.title).toBe("Opt Out | Jane Smith");
  });

  it("returns fallback title when config fails", async () => {
    mockLoadAccountConfig.mockImplementation(() => { throw new Error("fail"); });
    const meta = await generateMetadata({
      params: Promise.resolve({ handle: "bad-agent" }),
      searchParams: Promise.resolve({}),
    });
    expect(meta.title).toBe("Opt Out");
  });
});

describe("OptOutPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLoadAccountConfig.mockReturnValue(ACCOUNT);
    mockRequestOptOut.mockResolvedValue({ ok: true });
  });

  it("renders opt-out confirmation heading", async () => {
    const page = await OptOutPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Opt Out/i })).toBeInTheDocument();
  });

  it("displays agent name in confirmation prompt", async () => {
    const page = await OptOutPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);
    expect(screen.getByText(/Jane Smith/)).toBeInTheDocument();
  });

  it("shows email from query params", async () => {
    const page = await OptOutPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);
    expect(screen.getByText(/user@example\.com/)).toBeInTheDocument();
  });

  it("renders confirm button", async () => {
    const page = await OptOutPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);
    expect(screen.getByRole("button", { name: /Confirm Opt Out/i })).toBeInTheDocument();
  });

  it("calls requestOptOut server action when confirm button clicked", async () => {
    const page = await OptOutPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Confirm Opt Out/i }));

    await waitFor(() => {
      expect(mockRequestOptOut).toHaveBeenCalledWith("test-agent", "user@example.com", "abc123");
    });
  });

  it("shows success message after successful opt-out", async () => {
    mockRequestOptOut.mockResolvedValue({ ok: true });
    const page = await OptOutPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Confirm Opt Out/i }));

    await waitFor(() => {
      expect(screen.getByText(/successfully opted out/i)).toBeInTheDocument();
    });
  });

  it("shows error message when requestOptOut fails", async () => {
    mockRequestOptOut.mockResolvedValue({ ok: false, error: "Something went wrong. Please try again." });
    const page = await OptOutPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Confirm Opt Out/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
      expect(screen.getByText(/Something went wrong/i)).toBeInTheDocument();
    });
  });

  it("shows fallback error message when result.error is undefined", async () => {
    mockRequestOptOut.mockResolvedValue({ ok: false });
    const page = await OptOutPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Confirm Opt Out/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
      expect(screen.getByText(/Something went wrong/i)).toBeInTheDocument();
    });
  });

  it("uses fallback agent name when config fails", async () => {
    mockLoadAccountConfig.mockImplementation(() => { throw new Error("fail"); });
    const page = await OptOutPage({
      params: Promise.resolve({ handle: "bad-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com", token: "abc123" }),
    });
    render(page);
    expect(screen.getByText(/your agent/i)).toBeInTheDocument();
  });
});
