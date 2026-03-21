/**
 * @vitest-environment jsdom
 */
import { vi, describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ACCOUNT } from "../components/fixtures";

const mockLoadAccountConfig = vi.fn();
const mockRequestExport = vi.fn();

vi.mock("@/lib/config", () => ({
  loadAccountConfig: (...args: unknown[]) => mockLoadAccountConfig(...args),
}));

vi.mock("@/actions/privacy", () => ({
  requestExport: (...args: unknown[]) => mockRequestExport(...args),
}));

import MyDataPage, { generateMetadata } from "@/app/[handle]/privacy/my-data/page";

describe("generateMetadata (my-data)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLoadAccountConfig.mockReturnValue(ACCOUNT);
  });

  it("returns title with agent name when config loads", async () => {
    const meta = await generateMetadata({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({}),
    });
    expect(meta.title).toBe("My Data | Jane Smith");
  });

  it("returns fallback title when config fails", async () => {
    mockLoadAccountConfig.mockImplementation(() => { throw new Error("fail"); });
    const meta = await generateMetadata({
      params: Promise.resolve({ handle: "bad-agent" }),
      searchParams: Promise.resolve({}),
    });
    expect(meta.title).toBe("My Data");
  });
});

describe("MyDataPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLoadAccountConfig.mockReturnValue(ACCOUNT);
    mockRequestExport.mockResolvedValue({ ok: true, data: [] });
  });

  it("renders the email input and submit button", async () => {
    const page = await MyDataPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({}),
    });
    render(page);
    expect(screen.getByRole("textbox", { name: /Email Address/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Request My Data/i })).toBeInTheDocument();
  });

  it("shows validation error for empty email (does not call API)", async () => {
    const page = await MyDataPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "" }),
    });
    render(page);
    const form = screen.getByRole("button", { name: /Request My Data/i }).closest("form")!;
    fireEvent.submit(form);
    expect(mockRequestExport).not.toHaveBeenCalled();
  });

  it("shows loading state during fetch", async () => {
    let resolveExport: (value: { ok: boolean; data: unknown[] }) => void;
    mockRequestExport.mockReturnValue(
      new Promise((resolve) => { resolveExport = resolve; }),
    );
    const page = await MyDataPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Request My Data/i }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Loading/i })).toBeInTheDocument();
    });

    resolveExport!({ ok: true, data: [] });
  });

  it("displays data when API returns results", async () => {
    mockRequestExport.mockResolvedValue({
      ok: true,
      data: [
        {
          email: "user@example.com",
          name: "Test User",
          phone: "555-000-1234",
          propertyAddress: "123 Main St",
          source: "website",
          status: "active",
          submittedAt: "2026-01-15",
        },
      ],
    });
    const page = await MyDataPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Request My Data/i }));

    await waitFor(() => {
      expect(screen.getByText("Your Data")).toBeInTheDocument();
      expect(screen.getByText("Test User")).toBeInTheDocument();
      expect(screen.getByText("user@example.com")).toBeInTheDocument();
      expect(screen.getByText("555-000-1234")).toBeInTheDocument();
      expect(screen.getByText("123 Main St")).toBeInTheDocument();
      expect(screen.getByText("website")).toBeInTheDocument();
      expect(screen.getByText("active")).toBeInTheDocument();
      expect(screen.getByText("2026-01-15")).toBeInTheDocument();
    });
  });

  it("shows 'no data found' when API returns empty array", async () => {
    mockRequestExport.mockResolvedValue({ ok: true, data: [] });
    const page = await MyDataPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "nobody@example.com" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Request My Data/i }));

    await waitFor(() => {
      expect(screen.getByText(/No data found/i)).toBeInTheDocument();
    });
  });

  it("shows error message when API call fails", async () => {
    mockRequestExport.mockResolvedValue({ ok: false, error: "Something went wrong. Please try again." });
    const page = await MyDataPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Request My Data/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
      expect(screen.getByText(/Something went wrong/i)).toBeInTheDocument();
    });
  });

  it("shows fallback error message when result.error is undefined", async () => {
    mockRequestExport.mockResolvedValue({ ok: false });
    const page = await MyDataPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "user@example.com" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Request My Data/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
      expect(screen.getByText(/Something went wrong/i)).toBeInTheDocument();
    });
  });

  it("pre-fills email from query params", async () => {
    const page = await MyDataPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "prefilled@example.com" }),
    });
    render(page);
    const input = screen.getByRole("textbox", { name: /Email Address/i }) as HTMLInputElement;
    expect(input.value).toBe("prefilled@example.com");
  });

  it("trims email before submitting", async () => {
    const page = await MyDataPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "  user@example.com  " }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Request My Data/i }));

    await waitFor(() => {
      expect(mockRequestExport).toHaveBeenCalledWith("test-agent", "user@example.com");
    });
  });

  it("updates email when user types into the input", async () => {
    const page = await MyDataPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "" }),
    });
    render(page);

    const input = screen.getByRole("textbox", { name: /Email Address/i }) as HTMLInputElement;
    fireEvent.change(input, { target: { value: "typed@example.com" } });

    expect(input.value).toBe("typed@example.com");
  });

  it("displays agent name in description", async () => {
    const page = await MyDataPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({}),
    });
    render(page);
    expect(screen.getByText(/Jane Smith/)).toBeInTheDocument();
  });

  it("uses fallback agent name when config fails", async () => {
    mockLoadAccountConfig.mockImplementation(() => { throw new Error("fail"); });
    const page = await MyDataPage({
      params: Promise.resolve({ handle: "bad-agent" }),
      searchParams: Promise.resolve({}),
    });
    render(page);
    expect(screen.getByText(/your agent/i)).toBeInTheDocument();
  });

  it("includes a link back to the privacy policy page", async () => {
    const page = await MyDataPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({}),
    });
    render(page);
    const link = screen.getByRole("link", { name: /Back to Privacy Policy/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute("href", "/test-agent/privacy");
  });

  it("displays data with only required fields (email only)", async () => {
    mockRequestExport.mockResolvedValue({
      ok: true,
      data: [{ email: "minimal@example.com" }],
    });
    const page = await MyDataPage({
      params: Promise.resolve({ handle: "test-agent" }),
      searchParams: Promise.resolve({ email: "minimal@example.com" }),
    });
    render(page);

    fireEvent.click(screen.getByRole("button", { name: /Request My Data/i }));

    await waitFor(() => {
      expect(screen.getByText("Your Data")).toBeInTheDocument();
      expect(screen.getByText("minimal@example.com")).toBeInTheDocument();
    });
  });
});
