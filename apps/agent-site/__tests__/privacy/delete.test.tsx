/**
 * @vitest-environment jsdom
 */
import { vi, describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ACCOUNT } from "../components/fixtures";

const mockLoadAccountConfig = vi.fn();
const mockRequestDeletion = vi.fn();

vi.mock("@/features/config/config", () => ({
  loadAccountConfig: (...args: unknown[]) => mockLoadAccountConfig(...args),
}));

vi.mock("@/actions/privacy", () => ({
  requestDeletion: (...args: unknown[]) => mockRequestDeletion(...args),
}));

import DeletePage, { generateMetadata } from "@/app/[handle]/privacy/delete/page";

describe("generateMetadata (delete)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLoadAccountConfig.mockReturnValue(ACCOUNT);
  });

  it("returns title with agent name when config loads", async () => {
    const meta = await generateMetadata({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({}),
    });
    expect(meta.title).toBe("Data Deletion Request | Jane Smith");
  });

  it("returns fallback title when config fails", async () => {
    mockLoadAccountConfig.mockImplementation(() => { throw new Error("fail"); });
    const meta = await generateMetadata({
      params: Promise.resolve({ handle: "bad-agent" }),
      searchParams: Promise.resolve({}),
    });
    expect(meta.title).toBe("Data Deletion Request");
  });
});

describe("DeletePage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLoadAccountConfig.mockReturnValue(ACCOUNT);
    mockRequestDeletion.mockResolvedValue({ ok: true });
  });

  it("renders data deletion heading", async () => {
    const page = await DeletePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({}),
    });
    render(page);
    expect(screen.getByRole("heading", { level: 1, name: /Data Deletion/i })).toBeInTheDocument();
  });

  it("has email input field", async () => {
    const page = await DeletePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({}),
    });
    render(page);
    expect(screen.getByRole("textbox", { name: /Email Address/i })).toBeInTheDocument();
  });

  it("pre-fills email from query params", async () => {
    const page = await DeletePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "prefilled@example.com" }),
    });
    render(page);
    const input = screen.getByRole("textbox", { name: /Email Address/i }) as HTMLInputElement;
    expect(input.value).toBe("prefilled@example.com");
  });

  it("calls requestDeletion on form submission", async () => {
    const page = await DeletePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Submit Deletion Request/i }));

    await waitFor(() => {
      expect(mockRequestDeletion).toHaveBeenCalledWith("test-agent", "user@example.com");
    });
  });

  it("shows 'check your email' message after successful submission", async () => {
    mockRequestDeletion.mockResolvedValue({ ok: true });
    const page = await DeletePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Submit Deletion Request/i }));

    await waitFor(() => {
      expect(screen.getByText(/Check your email for a verification link/i)).toBeInTheDocument();
    });
  });

  it("shows error message when requestDeletion fails", async () => {
    mockRequestDeletion.mockResolvedValue({ ok: false, error: "Something went wrong. Please try again." });
    const page = await DeletePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Submit Deletion Request/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
      expect(screen.getByText(/Something went wrong/i)).toBeInTheDocument();
    });
  });

  it("shows fallback error message when result.error is undefined", async () => {
    mockRequestDeletion.mockResolvedValue({ ok: false });
    const page = await DeletePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Submit Deletion Request/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
      expect(screen.getByText(/Something went wrong/i)).toBeInTheDocument();
    });
  });

  it("does not call requestDeletion when email is whitespace", async () => {
    const page = await DeletePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "" }),
    });
    render(page);
    const form = screen.getByRole("button", { name: /Submit Deletion Request/i }).closest("form")!;
    fireEvent.submit(form);
    expect(mockRequestDeletion).not.toHaveBeenCalled();
  });

  it("trims email before submitting", async () => {
    const page = await DeletePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "  user@example.com  " }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Submit Deletion Request/i }));

    await waitFor(() => {
      expect(mockRequestDeletion).toHaveBeenCalledWith("test-agent", "user@example.com");
    });
  });

  it("updates email when user types into the input", async () => {
    const page = await DeletePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "" }),
    });
    render(page);

    const input = screen.getByRole("textbox", { name: /Email Address/i }) as HTMLInputElement;
    fireEvent.change(input, { target: { value: "typed@example.com" } });

    expect(input.value).toBe("typed@example.com");
  });

  it("displays agent name in description", async () => {
    const page = await DeletePage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({}),
    });
    render(page);
    expect(screen.getByText(/Jane Smith/)).toBeInTheDocument();
  });

  it("uses fallback agent name when config fails", async () => {
    mockLoadAccountConfig.mockImplementation(() => { throw new Error("fail"); });
    const page = await DeletePage({
      params: Promise.resolve({ handle: "bad-agent" }),
      searchParams: Promise.resolve({}),
    });
    render(page);
    expect(screen.getByText(/your agent/i)).toBeInTheDocument();
  });
});
