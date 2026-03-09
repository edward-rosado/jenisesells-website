/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import { CmaForm } from "@/components/sections/CmaForm";
import { AGENT, AGENT_MINIMAL } from "./fixtures";
import type { CmaFormData } from "@/lib/types";

const FORM_DATA: CmaFormData = {
  title: "What's Your Home Worth?",
  subtitle: "Get a free CMA today",
};

// Helper: fill out all required form fields
function fillForm() {
  fireEvent.change(screen.getByPlaceholderText("First Name"), { target: { value: "Alice" } });
  fireEvent.change(screen.getByPlaceholderText("Last Name"), { target: { value: "Test" } });
  fireEvent.change(screen.getByPlaceholderText("Email Address"), { target: { value: "alice@test.com" } });
  fireEvent.change(screen.getByPlaceholderText("Phone Number"), { target: { value: "555-111-2222" } });
  fireEvent.change(screen.getByPlaceholderText("Property Address"), { target: { value: "1 Test St" } });
  fireEvent.change(screen.getByPlaceholderText("City"), { target: { value: "Hoboken" } });
  fireEvent.change(screen.getByPlaceholderText("State"), { target: { value: "NJ" } });
  fireEvent.change(screen.getByPlaceholderText("Zip"), { target: { value: "07030" } });
  fireEvent.change(screen.getByRole("combobox"), { target: { value: "asap" } });
}

describe("CmaForm rendering", () => {
  it("renders the form title", () => {
    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    expect(screen.getByRole("heading", { level: 2, name: "What's Your Home Worth?" })).toBeInTheDocument();
  });

  it("renders the subtitle", () => {
    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    expect(screen.getByText("Get a free CMA today")).toBeInTheDocument();
  });

  it("renders all required input fields", () => {
    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    expect(screen.getByPlaceholderText("First Name")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Last Name")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Email Address")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Phone Number")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Property Address")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("City")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("State")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Zip")).toBeInTheDocument();
  });

  it("renders the timeline select dropdown", () => {
    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    const select = screen.getByRole("combobox");
    expect(select).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "As soon as possible" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "1-3 months" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "3-6 months" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "6-12 months" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: /Just curious/ })).toBeInTheDocument();
  });

  it("renders the textarea for additional notes", () => {
    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    expect(screen.getByPlaceholderText("Anything else I should know?")).toBeInTheDocument();
  });

  it("renders the submit button with default text", () => {
    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    expect(screen.getByRole("button", { name: /Get My Free Home Value Report/ })).toBeInTheDocument();
  });

  it("pre-fills the state field from agent location", () => {
    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    const stateInput = screen.getByPlaceholderText("State") as HTMLInputElement;
    expect(stateInput.value).toBe("NJ");
  });

  it("does not show error message initially", () => {
    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    expect(screen.queryByText("Something went wrong. Please try again.")).not.toBeInTheDocument();
  });

  it("submit button is not disabled initially", () => {
    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    expect(screen.getByRole("button")).not.toBeDisabled();
  });

  it("renders section with id cma-form", () => {
    const { container } = render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    expect(container.querySelector("#cma-form")).toBeInTheDocument();
  });

  it("all inputs have accessible labels via sr-only", () => {
    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    expect(screen.getByLabelText("First Name")).toBeInTheDocument();
    expect(screen.getByLabelText("Last Name")).toBeInTheDocument();
    expect(screen.getByLabelText("Email Address")).toBeInTheDocument();
    expect(screen.getByLabelText("Phone Number")).toBeInTheDocument();
    expect(screen.getByLabelText("Property Address")).toBeInTheDocument();
    expect(screen.getByLabelText("City")).toBeInTheDocument();
    expect(screen.getByLabelText("State")).toBeInTheDocument();
    expect(screen.getByLabelText("Zip Code")).toBeInTheDocument();
    expect(screen.getByLabelText("When are you looking to sell?")).toBeInTheDocument();
    expect(screen.getByLabelText("Additional notes")).toBeInTheDocument();
  });
});

describe("CmaForm form submission — formspree handler", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("calls the formspree endpoint when form_handler is formspree", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockResolvedValueOnce({ ok: true } as Response);

    // We need to mock window.location.href assignment
    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    await waitFor(() => {
      expect(mockFetch).toHaveBeenCalledWith(
        "https://formspree.io/f/abc123",
        expect.objectContaining({ method: "POST" })
      );
    });
  });

  it("shows loading state during submission", async () => {
    const mockFetch = vi.mocked(fetch);
    // Delay resolution so we can observe the loading state
    let resolvePromise!: (value: Response) => void;
    mockFetch.mockReturnValueOnce(
      new Promise<Response>((resolve) => { resolvePromise = resolve; })
    );

    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    fillForm();

    act(() => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    await waitFor(() => {
      expect(screen.getByRole("button")).toBeDisabled();
      expect(screen.getByRole("button")).toHaveTextContent("Submitting...");
    });

    // Clean up
    act(() => {
      resolvePromise({ ok: true } as Response);
    });
  });

  it("redirects to thank-you page on success", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockResolvedValueOnce({ ok: true } as Response);

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    await waitFor(() => {
      expect(locationMock.href).toContain("/thank-you");
      expect(locationMock.href).toContain("test-agent");
    });
  });

  it("shows error message when fetch returns non-ok response", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockResolvedValueOnce({ ok: false, status: 500 } as Response);

    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    await waitFor(() => {
      expect(screen.getByText("Something went wrong. Please try again.")).toBeInTheDocument();
    });
  });

  it("shows error message when fetch throws a network error", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockRejectedValueOnce(new Error("Network error"));

    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    await waitFor(() => {
      expect(screen.getByText("Something went wrong. Please try again.")).toBeInTheDocument();
    });
  });

  it("re-enables submit button after error", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockRejectedValueOnce(new Error("Network error"));

    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    await waitFor(() => {
      expect(screen.getByRole("button")).not.toBeDisabled();
    });
  });

  it("clears previous error on new submission attempt", async () => {
    const mockFetch = vi.mocked(fetch);
    // First call fails, second call succeeds
    mockFetch
      .mockRejectedValueOnce(new Error("Network error"))
      .mockResolvedValueOnce({ ok: true } as Response);

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaForm agent={AGENT} data={FORM_DATA} />);
    fillForm();

    // First submission — should produce error
    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    await waitFor(() => {
      expect(screen.getByText("Something went wrong. Please try again.")).toBeInTheDocument();
    });

    // Second submission — error should disappear while in-flight
    act(() => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    await waitFor(() => {
      expect(screen.queryByText("Something went wrong. Please try again.")).not.toBeInTheDocument();
    });
  });
});

describe("CmaForm form submission — custom API handler", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("calls the internal API endpoint when form_handler is not formspree", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockResolvedValueOnce({ ok: true } as Response);

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaForm agent={AGENT_MINIMAL} data={FORM_DATA} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    await waitFor(() => {
      expect(mockFetch).toHaveBeenCalledWith(
        "/api/agents/minimal-agent/cma",
        expect.objectContaining({ method: "POST" })
      );
    });
  });

  it("includes Accept application/json header", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockResolvedValueOnce({ ok: true } as Response);

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaForm agent={AGENT_MINIMAL} data={FORM_DATA} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    await waitFor(() => {
      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({ headers: { Accept: "application/json" } })
      );
    });
  });
});
