/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import { CmaSection } from "@/components/sections/CmaSection";
import type { CmaFormData } from "@/lib/types";

// --- Mock useCmaSubmit from @real-estate-star/ui ---
const mockSubmit = vi.fn();
const mockReset = vi.fn();

let mockCmaState = {
  phase: "idle" as string,
  jobId: null as string | null,
  errorMessage: null as string | null,
};

vi.mock("@real-estate-star/ui", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@real-estate-star/ui")>();
  return {
    ...actual,
    useCmaSubmit: (_apiBaseUrl: string, _options?: { onError?: (err: Error) => void }) => ({
      state: mockCmaState,
      submit: mockSubmit,
      reset: mockReset,
    }),
  };
});

// Mock useGoogleMapsAutocomplete so it doesn't try to load Google Maps SDK
vi.mock("@real-estate-star/ui/LeadForm/useGoogleMapsAutocomplete", () => ({
  useGoogleMapsAutocomplete: () => ({ loaded: false }),
}));

// Mock Sentry
vi.mock("@sentry/nextjs", () => ({
  captureException: vi.fn(),
}));

// Mock Analytics
vi.mock("@/components/Analytics", () => ({
  trackCmaConversion: vi.fn(),
}));

const FORM_DATA: CmaFormData = {
  title: "What's Your Home Worth?",
  subtitle: "Get a free CMA today",
};

const DEFAULT_PROPS = {
  agentId: "test-agent",
  agentName: "Jane Smith",
  defaultState: "NJ",
  data: FORM_DATA,
};

function fillForm() {
  fireEvent.change(screen.getByLabelText(/^first name/i), { target: { value: "Alice" } });
  fireEvent.change(screen.getByLabelText(/^last name/i), { target: { value: "Test" } });
  fireEvent.change(screen.getByLabelText(/^email/i), { target: { value: "alice@test.com" } });
  fireEvent.change(screen.getByLabelText(/^phone/i), { target: { value: "555-111-2222" } });
  fireEvent.change(screen.getByLabelText(/property address/i), { target: { value: "1 Test St" } });
  fireEvent.change(screen.getByLabelText(/^city/i), { target: { value: "Hoboken" } });
  fireEvent.change(screen.getByLabelText(/^zip/i), { target: { value: "07030" } });
  fireEvent.change(screen.getByLabelText(/looking to sell/), { target: { value: "asap" } });
  fireEvent.click(screen.getByTestId("tcpa-consent"));
}

function switchToBuyerAndFill() {
  fireEvent.click(screen.getByLabelText(/I'm Selling/));
  fireEvent.click(screen.getByLabelText(/I'm Buying/));
  fireEvent.change(screen.getByLabelText(/^first name/i), { target: { value: "Alice" } });
  fireEvent.change(screen.getByLabelText(/^last name/i), { target: { value: "Test" } });
  fireEvent.change(screen.getByLabelText(/^email/i), { target: { value: "alice@test.com" } });
  fireEvent.change(screen.getByLabelText(/^phone/i), { target: { value: "555-111-2222" } });
  fireEvent.change(screen.getByLabelText(/desired area/i), { target: { value: "Hoboken" } });
  fireEvent.change(screen.getByLabelText(/looking to buy/i), { target: { value: "asap" } });
  fireEvent.click(screen.getByTestId("tcpa-consent"));
}

beforeEach(() => {
  mockCmaState = { phase: "idle", jobId: null, errorMessage: null };
  mockSubmit.mockReset();
  mockReset.mockReset();
});

describe("CmaSection rendering", () => {
  it("renders the form title and subtitle", () => {
    render(<CmaSection {...DEFAULT_PROPS} />);
    expect(screen.getByRole("heading", { level: 2, name: "What's Your Home Worth?" })).toBeInTheDocument();
    expect(screen.getByText("Get a free CMA today")).toBeInTheDocument();
  });

  it("renders all required input fields via LeadForm", () => {
    render(<CmaSection {...DEFAULT_PROPS} />);
    expect(screen.getByLabelText(/^first name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^last name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^email/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^phone/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/property address/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^city/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^state/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^zip/i)).toBeInTheDocument();
  });

  it("renders section with id cma-form", () => {
    const { container } = render(<CmaSection {...DEFAULT_PROPS} />);
    expect(container.querySelector("#cma-form")).toBeInTheDocument();
  });

  it("renders submit button with seller label", () => {
    render(<CmaSection {...DEFAULT_PROPS} />);
    expect(screen.getByRole("button", { name: /Get My Free Home Value Report/ })).toBeInTheDocument();
  });

  it("shows buyer-specific submit label when only buying is selected", () => {
    render(<CmaSection {...DEFAULT_PROPS} />);
    fireEvent.click(screen.getByLabelText(/I'm Selling/));
    fireEvent.click(screen.getByLabelText(/I'm Buying/));
    expect(screen.getByRole("button", { name: /Tell Jane you're ready to buy/ })).toBeInTheDocument();
  });

  it("renders CMA disclaimer", () => {
    render(<CmaSection {...DEFAULT_PROPS} />);
    expect(screen.getByText(/not an appraisal/i)).toBeInTheDocument();
  });

  it("pre-fills the state field from defaultState", () => {
    render(<CmaSection {...DEFAULT_PROPS} />);
    const stateInput = screen.getByLabelText(/^state/i) as HTMLInputElement;
    expect(stateInput.value).toBe("NJ");
  });
});

describe("CmaSection form submission", () => {
  it("calls useCmaSubmit.submit with agentId and LeadFormData on submit", async () => {
    mockSubmit.mockResolvedValueOnce(true);

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaSection {...DEFAULT_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(mockSubmit).toHaveBeenCalledWith(
      "test-agent",
      expect.objectContaining({
        firstName: "Alice",
        lastName: "Test",
        email: "alice@test.com",
        phone: "555-111-2222",
      }),
    );
  });

  it("redirects to thank-you page on successful submission", async () => {
    mockSubmit.mockResolvedValueOnce(true);

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaSection {...DEFAULT_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(locationMock.href).toContain("/thank-you");
    expect(locationMock.href).toContain("test-agent");
  });

  it("fires analytics conversion tracking on success", async () => {
    const { trackCmaConversion } = await import("@/components/Analytics");
    mockSubmit.mockResolvedValueOnce(true);

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    const tracking = { google_analytics_id: "G-TEST" };
    render(<CmaSection {...DEFAULT_PROPS} tracking={tracking} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(trackCmaConversion).toHaveBeenCalledWith(tracking);
  });

  it("does NOT redirect on failed submission", async () => {
    mockSubmit.mockResolvedValueOnce(false);

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaSection {...DEFAULT_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(locationMock.href).toBe("");
  });

  it("disables submit button when phase is submitting", () => {
    mockCmaState = { phase: "submitting", jobId: null, errorMessage: null };
    render(<CmaSection {...DEFAULT_PROPS} />);
    expect(screen.getByRole("button", { name: /Get My Free Home Value Report/ })).toBeDisabled();
  });

  it("shows error message from cmaSubmit state", () => {
    mockCmaState = { phase: "error", jobId: null, errorMessage: "CMA submission failed (500)" };
    render(<CmaSection {...DEFAULT_PROPS} />);
    expect(screen.getByText("CMA submission failed (500)")).toBeInTheDocument();
  });

  it("submits buyer-only leads (LeadFormData.seller will be undefined)", async () => {
    mockSubmit.mockResolvedValueOnce(true);

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaSection {...DEFAULT_PROPS} />);
    switchToBuyerAndFill();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(mockSubmit).toHaveBeenCalledWith(
      "test-agent",
      expect.objectContaining({
        firstName: "Alice",
        leadTypes: ["buying"],
      }),
    );
  });

  it("passes numeric beds, baths, and sqft when provided", async () => {
    mockSubmit.mockResolvedValueOnce(true);

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaSection {...DEFAULT_PROPS} />);
    fillForm();
    fireEvent.change(screen.getByLabelText(/^beds$/i), { target: { value: "4" } });
    fireEvent.change(screen.getByLabelText(/^baths$/i), { target: { value: "2" } });
    fireEvent.change(screen.getByLabelText(/^sqft$/i), { target: { value: "2200" } });

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(mockSubmit).toHaveBeenCalledWith(
      "test-agent",
      expect.objectContaining({
        seller: expect.objectContaining({ beds: 4, baths: 2, sqft: 2200 }),
      }),
    );
  });

  it("does NOT call fetch directly — uses shared hook", async () => {
    vi.stubGlobal("fetch", vi.fn());
    const mockFetch = vi.mocked(fetch);
    mockSubmit.mockResolvedValueOnce(true);

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaSection {...DEFAULT_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(mockFetch).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });
});
