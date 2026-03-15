/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import { CmaForm } from "@/components/sections/CmaForm";
import type { CmaFormData } from "@/lib/types";
import type { CmaStatusUpdate } from "@/lib/cma-api";

// --- Mock useCmaSubmit ---
const mockSubmit = vi.fn();
const mockReset = vi.fn();

let mockCmaState = {
  phase: "idle" as string,
  statusUpdate: null as CmaStatusUpdate | null,
  errorMessage: null as string | null,
};

vi.mock("@/lib/useCmaSubmit", () => ({
  useCmaSubmit: () => ({
    state: mockCmaState,
    submit: mockSubmit,
    reset: mockReset,
  }),
}));

// Mock useGoogleMapsAutocomplete so it doesn't try to load Google Maps SDK
vi.mock("@real-estate-star/ui/LeadForm/useGoogleMapsAutocomplete", () => ({
  useGoogleMapsAutocomplete: () => ({ loaded: false }),
}));

const FORM_DATA: CmaFormData = {
  title: "What's Your Home Worth?",
  subtitle: "Get a free CMA today",
};

// Props matching the AGENT fixture (formspree handler)
const FORMSPREE_PROPS = {
  agentId: "test-agent",
  agentName: "Jane Smith",
  defaultState: "NJ",
  formHandler: "formspree" as const,
  formHandlerId: "abc123",
  data: FORM_DATA,
};

// Props matching the AGENT_MINIMAL fixture (no form handler — uses API mode)
const API_PROPS = {
  agentId: "minimal-agent",
  agentName: "Bob Jones",
  defaultState: "TX",
  data: FORM_DATA,
};

// Helper: fill out all required form fields via LeadForm's labels
// LeadForm renders with initialMode=["selling"], so seller card is visible
function fillForm() {
  fireEvent.change(screen.getByLabelText("First Name"), { target: { value: "Alice" } });
  fireEvent.change(screen.getByLabelText("Last Name"), { target: { value: "Test" } });
  fireEvent.change(screen.getByLabelText("Email"), { target: { value: "alice@test.com" } });
  fireEvent.change(screen.getByLabelText("Phone"), { target: { value: "555-111-2222" } });
  fireEvent.change(screen.getByLabelText("Property Address"), { target: { value: "1 Test St" } });
  fireEvent.change(screen.getByLabelText("City"), { target: { value: "Hoboken" } });
  fireEvent.change(screen.getByLabelText("State"), { target: { value: "NJ" } });
  fireEvent.change(screen.getByLabelText("Zip"), { target: { value: "07030" } });
  fireEvent.change(screen.getByLabelText(/looking to sell/), { target: { value: "asap" } });
}

beforeEach(() => {
  mockCmaState = { phase: "idle", statusUpdate: null, errorMessage: null };
  mockSubmit.mockReset();
  mockReset.mockReset();
});

describe("CmaForm rendering", () => {
  it("renders the form title", () => {
    render(<CmaForm {...FORMSPREE_PROPS} />);
    expect(screen.getByRole("heading", { level: 2, name: "What's Your Home Worth?" })).toBeInTheDocument();
  });

  it("renders the subtitle", () => {
    render(<CmaForm {...FORMSPREE_PROPS} />);
    expect(screen.getByText("Get a free CMA today")).toBeInTheDocument();
  });

  it("renders all required input fields via LeadForm", () => {
    render(<CmaForm {...FORMSPREE_PROPS} />);
    expect(screen.getByLabelText("First Name")).toBeInTheDocument();
    expect(screen.getByLabelText("Last Name")).toBeInTheDocument();
    expect(screen.getByLabelText("Email")).toBeInTheDocument();
    expect(screen.getByLabelText("Phone")).toBeInTheDocument();
    expect(screen.getByLabelText("Property Address")).toBeInTheDocument();
    expect(screen.getByLabelText("City")).toBeInTheDocument();
    expect(screen.getByLabelText("State")).toBeInTheDocument();
    expect(screen.getByLabelText("Zip")).toBeInTheDocument();
  });

  it("renders the timeline select dropdown", () => {
    render(<CmaForm {...FORMSPREE_PROPS} />);
    const select = screen.getByLabelText(/looking to sell/);
    expect(select).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "ASAP" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "1-3 Months" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "3-6 Months" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "6-12 Months" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Just Curious" })).toBeInTheDocument();
  });

  it("renders the notes textarea", () => {
    render(<CmaForm {...FORMSPREE_PROPS} />);
    expect(screen.getByLabelText(/Notes/)).toBeInTheDocument();
  });

  it("renders the submit button with default text", () => {
    render(<CmaForm {...FORMSPREE_PROPS} />);
    expect(screen.getByRole("button", { name: /Get My Free Home Value Report/ })).toBeInTheDocument();
  });

  it("pre-fills the state field from defaultState prop", () => {
    render(<CmaForm {...FORMSPREE_PROPS} />);
    const stateInput = screen.getByLabelText("State") as HTMLInputElement;
    expect(stateInput.value).toBe("NJ");
  });

  it("does not show error message initially", () => {
    render(<CmaForm {...FORMSPREE_PROPS} />);
    expect(screen.queryByText("Something went wrong. Please try again.")).not.toBeInTheDocument();
  });

  it("submit button is not disabled initially", () => {
    render(<CmaForm {...FORMSPREE_PROPS} />);
    expect(screen.getByRole("button", { name: /Get My Free Home Value Report/ })).not.toBeDisabled();
  });

  it("renders section with id cma-form", () => {
    const { container } = render(<CmaForm {...FORMSPREE_PROPS} />);
    expect(container.querySelector("#cma-form")).toBeInTheDocument();
  });

  it("all inputs have accessible labels", () => {
    render(<CmaForm {...FORMSPREE_PROPS} />);
    expect(screen.getByLabelText("First Name")).toBeInTheDocument();
    expect(screen.getByLabelText("Last Name")).toBeInTheDocument();
    expect(screen.getByLabelText("Email")).toBeInTheDocument();
    expect(screen.getByLabelText("Phone")).toBeInTheDocument();
    expect(screen.getByLabelText("Property Address")).toBeInTheDocument();
    expect(screen.getByLabelText("City")).toBeInTheDocument();
    expect(screen.getByLabelText("State")).toBeInTheDocument();
    expect(screen.getByLabelText("Zip")).toBeInTheDocument();
    expect(screen.getByLabelText(/looking to sell/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Notes/)).toBeInTheDocument();
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

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaForm {...FORMSPREE_PROPS} />);
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

  it("disables submit button during submission", async () => {
    const mockFetch = vi.mocked(fetch);
    let resolvePromise!: (value: Response) => void;
    mockFetch.mockReturnValueOnce(
      new Promise<Response>((resolve) => { resolvePromise = resolve; })
    );

    render(<CmaForm {...FORMSPREE_PROPS} />);
    fillForm();

    act(() => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Get My Free Home Value Report/ })).toBeDisabled();
    });

    await act(async () => {
      resolvePromise({ ok: true } as Response);
    });
  });

  it("redirects to thank-you page on success", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockResolvedValueOnce({ ok: true } as Response);

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaForm {...FORMSPREE_PROPS} />);
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

    render(<CmaForm {...FORMSPREE_PROPS} />);
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

    render(<CmaForm {...FORMSPREE_PROPS} />);
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

    render(<CmaForm {...FORMSPREE_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Get My Free Home Value Report/ })).not.toBeDisabled();
    });
  });

  it("clears previous error on new submission attempt", async () => {
    const mockFetch = vi.mocked(fetch);
    mockFetch
      .mockRejectedValueOnce(new Error("Network error"))
      .mockResolvedValueOnce({ ok: true } as Response);

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaForm {...FORMSPREE_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    await waitFor(() => {
      expect(screen.getByText("Something went wrong. Please try again.")).toBeInTheDocument();
    });

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    await waitFor(() => {
      expect(screen.queryByText("Something went wrong. Please try again.")).not.toBeInTheDocument();
    });
  });
});

describe("CmaForm — API mode submission", () => {
  it("calls useCmaSubmit.submit with correct request shape on submit", async () => {
    mockSubmit.mockResolvedValueOnce(undefined);

    render(<CmaForm {...API_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(mockSubmit).toHaveBeenCalledWith("minimal-agent", {
      firstName: "Alice",
      lastName: "Test",
      email: "alice@test.com",
      phone: "555-111-2222",
      address: "1 Test St",
      city: "Hoboken",
      state: "NJ",
      zip: "07030",
      timeline: "asap",
      notes: undefined,
    });
  });

  it("passes notes when provided", async () => {
    mockSubmit.mockResolvedValueOnce(undefined);

    render(<CmaForm {...API_PROPS} />);
    fillForm();
    fireEvent.change(screen.getByLabelText(/Notes/), {
      target: { value: "Corner lot, recently renovated" },
    });

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(mockSubmit).toHaveBeenCalledWith(
      "minimal-agent",
      expect.objectContaining({ notes: "Corner lot, recently renovated" }),
    );
  });

  it("does NOT call fetch directly in API mode", async () => {
    vi.stubGlobal("fetch", vi.fn());
    const mockFetch = vi.mocked(fetch);
    mockSubmit.mockResolvedValueOnce(undefined);

    render(<CmaForm {...API_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(mockFetch).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });

  it("disables submit button when phase is submitting", () => {
    mockCmaState = { phase: "submitting", statusUpdate: null, errorMessage: null };
    render(<CmaForm {...API_PROPS} />);
    expect(screen.getByRole("button", { name: /Get My Free Home Value Report/ })).toBeDisabled();
  });

  it("shows error from cmaSubmit state when phase is error (pre-SignalR)", () => {
    mockCmaState = { phase: "error", statusUpdate: null, errorMessage: "CMA submission failed (500)" };
    render(<CmaForm {...API_PROPS} />);
    expect(screen.getByText("CMA submission failed (500)")).toBeInTheDocument();
  });
});

describe("CmaForm — progress tracker UI", () => {
  it("shows progress view when phase is tracking", () => {
    mockCmaState = {
      phase: "tracking",
      statusUpdate: {
        status: "SearchingComps",
        step: 2,
        totalSteps: 9,
        message: "Searching MLS databases...",
      },
      errorMessage: null,
    };
    render(<CmaForm {...API_PROPS} />);

    expect(screen.getByText("Preparing Your Report...")).toBeInTheDocument();
    expect(screen.getByText("Searching MLS databases...")).toBeInTheDocument();
    expect(screen.getByText("Step 2 of 9")).toBeInTheDocument();
    expect(screen.getByRole("progressbar")).toBeInTheDocument();
  });

  it("shows correct progress percentage", () => {
    mockCmaState = {
      phase: "tracking",
      statusUpdate: { status: "Analyzing", step: 4, totalSteps: 9, message: "Analyzing..." },
      errorMessage: null,
    };
    render(<CmaForm {...API_PROPS} />);

    const progressbar = screen.getByRole("progressbar");
    expect(progressbar).toHaveAttribute("aria-valuenow", "44");
  });

  it("shows completion view when phase is complete", () => {
    mockCmaState = {
      phase: "complete",
      statusUpdate: {
        status: "Complete",
        step: 9,
        totalSteps: 9,
        message: "Your report has been sent to your email!",
      },
      errorMessage: null,
    };
    render(<CmaForm {...API_PROPS} />);

    expect(screen.getByText("Your Report Is Ready!")).toBeInTheDocument();
    expect(screen.getByText(/Check your inbox/)).toBeInTheDocument();
    expect(screen.getByRole("progressbar")).toHaveAttribute("aria-valuenow", "100");
  });

  it("shows link to thank-you page on complete", () => {
    mockCmaState = {
      phase: "complete",
      statusUpdate: { status: "Complete", step: 9, totalSteps: 9, message: "Done!" },
      errorMessage: null,
    };
    render(<CmaForm {...API_PROPS} />);

    const link = screen.getByRole("link", { name: /Back to Bob Jones/ });
    expect(link).toHaveAttribute("href", "/thank-you?agentId=minimal-agent");
  });

  it("shows error view with pipeline error message", () => {
    mockCmaState = {
      phase: "error",
      statusUpdate: {
        status: "Failed",
        step: 3,
        totalSteps: 9,
        message: "An error occurred while processing your report.",
        errorMessage: "Pipeline execution failed. Please try again or contact support.",
      },
      errorMessage: "Pipeline execution failed. Please try again or contact support.",
    };
    render(<CmaForm {...API_PROPS} />);

    expect(screen.getByText("Something Went Wrong")).toBeInTheDocument();
    expect(screen.getByText("Pipeline execution failed. Please try again or contact support.")).toBeInTheDocument();
  });

  it("shows Try Again button on error and calls reset when clicked", () => {
    mockCmaState = {
      phase: "error",
      statusUpdate: { status: "Failed", step: 3, totalSteps: 9, message: "Error", errorMessage: "Fail" },
      errorMessage: "Fail",
    };
    render(<CmaForm {...API_PROPS} />);

    const tryAgainButton = screen.getByRole("button", { name: "Try Again" });
    expect(tryAgainButton).toBeInTheDocument();

    fireEvent.click(tryAgainButton);
    expect(mockReset).toHaveBeenCalledTimes(1);
  });

  it("renders section id cma-form in all progress states", () => {
    mockCmaState = {
      phase: "tracking",
      statusUpdate: { status: "Parsing", step: 1, totalSteps: 9, message: "Starting..." },
      errorMessage: null,
    };
    const { container } = render(<CmaForm {...API_PROPS} />);
    expect(container.querySelector("#cma-form")).toBeInTheDocument();
  });

  it("shows default message when statusUpdate is null during tracking", () => {
    mockCmaState = { phase: "tracking", statusUpdate: null, errorMessage: null };
    render(<CmaForm {...API_PROPS} />);

    expect(screen.getByText("Preparing Your Report...")).toBeInTheDocument();
    expect(screen.getByText("Starting...")).toBeInTheDocument();
  });
});

describe("CmaForm — formspree vs API mode detection", () => {
  it("uses formspree path when formHandler is formspree", async () => {
    vi.stubGlobal("fetch", vi.fn());
    const mockFetch = vi.mocked(fetch);
    mockFetch.mockResolvedValueOnce({ ok: true } as Response);

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaForm {...FORMSPREE_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    // Formspree mode should call fetch, not useCmaSubmit.submit
    expect(mockFetch).toHaveBeenCalled();
    expect(mockSubmit).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });

  it("uses API mode when formHandler is undefined", async () => {
    mockSubmit.mockResolvedValueOnce(undefined);

    render(<CmaForm {...API_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(mockSubmit).toHaveBeenCalled();
  });

  it("uses API mode when formHandler is custom", async () => {
    mockSubmit.mockResolvedValueOnce(undefined);

    render(<CmaForm {...{ ...API_PROPS, formHandler: "custom" as const }} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(mockSubmit).toHaveBeenCalled();
  });

  it("passes numeric beds, baths, and sqft when provided", async () => {
    mockSubmit.mockResolvedValueOnce(undefined);

    render(<CmaForm {...API_PROPS} />);
    fillForm();
    fireEvent.change(screen.getByLabelText("Beds"), { target: { value: "4" } });
    fireEvent.change(screen.getByLabelText("Baths"), { target: { value: "2" } });
    fireEvent.change(screen.getByLabelText("Sqft"), { target: { value: "2200" } });

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(mockSubmit).toHaveBeenCalledWith(
      "minimal-agent",
      expect.objectContaining({ beds: 4, baths: 2, sqft: 2200 }),
    );
  });
});
