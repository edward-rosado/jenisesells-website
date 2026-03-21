/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import { CmaSection } from "@/components/sections/shared/CmaSection";
import type { ContactFormData } from "@/lib/types";

// --- Mock submitLead server action ---
const mockSubmitLead = vi.fn();

vi.mock("@/actions/submit-lead", () => ({
  submitLead: (...args: unknown[]) => mockSubmitLead(...args),
}));

// Mock useGoogleMapsAutocomplete so it doesn't try to load Google Maps SDK
vi.mock("@real-estate-star/ui/LeadForm/useGoogleMapsAutocomplete", () => ({
  useGoogleMapsAutocomplete: () => ({ loaded: false }),
}));

// Mock Analytics
vi.mock("@/components/Analytics", () => ({
  trackCmaConversion: vi.fn(),
}));

// Mock Turnstile
vi.mock("@marsidev/react-turnstile", () => ({
  Turnstile: (props: { siteKey: string; onSuccess: (token: string) => void }) => (
    <div data-testid="turnstile-widget" data-site-key={props.siteKey} />
  ),
}));

const FORM_DATA: ContactFormData = {
  title: "What's Your Home Worth?",
  subtitle: "Get a free CMA today",
};

const DEFAULT_PROPS = {
  accountId: "test-agent",
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

function switchToBuyerAndFill(options?: { skipEmail?: boolean }) {
  fireEvent.click(screen.getByRole("checkbox", { name: /I'm Selling/ }));
  fireEvent.click(screen.getByRole("checkbox", { name: /I'm Buying/ }));
  fireEvent.change(screen.getByLabelText(/^first name/i), { target: { value: "Alice" } });
  fireEvent.change(screen.getByLabelText(/^last name/i), { target: { value: "Test" } });
  if (!options?.skipEmail) {
    fireEvent.change(screen.getByLabelText(/^email/i), { target: { value: "alice@test.com" } });
  }
  fireEvent.change(screen.getByLabelText(/^phone/i), { target: { value: "555-111-2222" } });
  fireEvent.change(screen.getByLabelText(/desired area/i), { target: { value: "Hoboken" } });
  fireEvent.change(screen.getByLabelText(/looking to buy/i), { target: { value: "asap" } });
  fireEvent.click(screen.getByTestId("tcpa-consent"));
}

beforeEach(() => {
  mockSubmitLead.mockReset();
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

  it("renders section with id contact_form", () => {
    const { container } = render(<CmaSection {...DEFAULT_PROPS} />);
    expect(container.querySelector("#contact_form")).toBeInTheDocument();
  });

  it("renders submit button with seller label", () => {
    render(<CmaSection {...DEFAULT_PROPS} />);
    expect(screen.getByRole("button", { name: /Get My Free Home Value Report/ })).toBeInTheDocument();
  });

  it("shows buyer-specific submit label when only buying is selected", () => {
    render(<CmaSection {...DEFAULT_PROPS} />);
    fireEvent.click(screen.getByRole("checkbox", { name: /I'm Selling/ }));
    fireEvent.click(screen.getByRole("checkbox", { name: /I'm Buying/ }));
    expect(screen.getByRole("button", { name: /Find My Dream Home/ })).toBeInTheDocument();
  });

  it("renders description paragraph when provided in data", () => {
    const props = {
      ...DEFAULT_PROPS,
      data: { ...FORM_DATA, description: "Enter your address for a **free** report." },
    };
    render(<CmaSection {...props} />);
    expect(screen.getByText(/Enter your address/)).toBeInTheDocument();
    // **bold** markdown renders as <strong>
    const strong = screen.getByText("free");
    expect(strong.tagName).toBe("STRONG");
  });

  it("does not render description paragraph when absent", () => {
    render(<CmaSection {...DEFAULT_PROPS} />);
    // FORM_DATA has no description — no extra paragraph between subtitle and form
    const section = screen.getByRole("region", { name: /home value request/i });
    const paragraphs = section.querySelectorAll("p");
    // Only subtitle paragraph, no description paragraph
    const descParagraph = Array.from(paragraphs).find(
      (p) => p.style.color === "#555" && p.style.marginBottom === "30px"
    );
    expect(descParagraph).toBeUndefined();
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
  it("calls submitLead with accountId and LeadFormData on submit", async () => {
    mockSubmitLead.mockResolvedValueOnce({ leadId: "lead-123", status: "received" });

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaSection {...DEFAULT_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(mockSubmitLead).toHaveBeenCalledWith(
      "test-agent",
      expect.objectContaining({
        firstName: "Alice",
        lastName: "Test",
        email: "alice@test.com",
        phone: "555-111-2222",
      }),
      expect.any(String),
    );
  });

  it("calls submitLead for buyer-only leads (all lead types submitted)", async () => {
    mockSubmitLead.mockResolvedValueOnce({ leadId: "lead-456", status: "received" });

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaSection {...DEFAULT_PROPS} />);
    switchToBuyerAndFill();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(mockSubmitLead).toHaveBeenCalledWith(
      "test-agent",
      expect.objectContaining({ firstName: "Alice" }),
      expect.any(String),
    );
    expect(locationMock.href).toContain("/thank-you");
  });

  it("redirects to thank-you page on successful submission", async () => {
    mockSubmitLead.mockResolvedValueOnce({ leadId: "lead-123", status: "received" });

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaSection {...DEFAULT_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(locationMock.href).toContain("/thank-you");
    expect(locationMock.href).toContain("test-agent");
    expect(locationMock.href).not.toContain("email=");
  });

  it("fires analytics conversion tracking on success", async () => {
    const { trackCmaConversion } = await import("@/components/Analytics");
    mockSubmitLead.mockResolvedValueOnce({ leadId: "lead-123", status: "received" });

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

  it("does NOT redirect when submitLead returns an error", async () => {
    mockSubmitLead.mockResolvedValueOnce({ error: "Verification failed. Please try again." });

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaSection {...DEFAULT_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(locationMock.href).toBe("");
  });

  it("shows error message when submitLead returns an error", async () => {
    mockSubmitLead.mockResolvedValueOnce({ error: "Verification failed. Please try again." });

    render(<CmaSection {...DEFAULT_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(screen.getByText("Verification failed. Please try again.")).toBeInTheDocument();
  });

  it("logs error when submitLead returns an error", async () => {
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    mockSubmitLead.mockResolvedValueOnce({ error: "Something went wrong. Please try again." });

    render(<CmaSection {...DEFAULT_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(spy).toHaveBeenCalledWith(
      "[agent-site] Lead submission error:",
      "Something went wrong. Please try again.",
    );
    spy.mockRestore();
  });

  it("logs error and shows message when submitLead throws", async () => {
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    const err = new Error("Network failure");
    mockSubmitLead.mockRejectedValueOnce(err);

    render(<CmaSection {...DEFAULT_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(spy).toHaveBeenCalledWith(
      "[agent-site] Lead submission failed:",
      err,
    );
    expect(screen.getByText("Something went wrong. Please try again.")).toBeInTheDocument();
    spy.mockRestore();
  });

  it("disables submit button while processing", async () => {
    let resolveSubmit: (v: unknown) => void;
    mockSubmitLead.mockReturnValueOnce(
      new Promise((resolve) => { resolveSubmit = resolve; }),
    );

    render(<CmaSection {...DEFAULT_PROPS} />);
    fillForm();

    act(() => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    // While pending, button should be disabled
    expect(screen.getByRole("button", { name: /Get My Free Home Value Report/ })).toBeDisabled();

    await act(async () => {
      resolveSubmit!({ leadId: "lead-123" });
    });
  });

  it("omits email param from redirect URL when email is empty", async () => {
    mockSubmitLead.mockResolvedValueOnce({ leadId: "lead-456", status: "received" });

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaSection {...DEFAULT_PROPS} />);
    switchToBuyerAndFill({ skipEmail: true });

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(locationMock.href).toBe("/thank-you?accountId=test-agent");
    expect(locationMock.href).not.toContain("email=");
  });
});

describe("CmaSection Turnstile integration", () => {
  const ORIGINAL_ENV = process.env;

  beforeEach(() => {
    process.env = { ...ORIGINAL_ENV };
  });

  afterEach(() => {
    process.env = ORIGINAL_ENV;
  });

  it("renders Turnstile widget when NEXT_PUBLIC_TURNSTILE_SITE_KEY is set", () => {
    process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY = "test-site-key";
    render(<CmaSection {...DEFAULT_PROPS} />);
    expect(screen.getByTestId("turnstile-widget")).toBeInTheDocument();
    expect(screen.getByTestId("turnstile-widget")).toHaveAttribute("data-site-key", "test-site-key");
  });

  it("does not render Turnstile widget when NEXT_PUBLIC_TURNSTILE_SITE_KEY is absent", () => {
    delete process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY;
    render(<CmaSection {...DEFAULT_PROPS} />);
    expect(screen.queryByTestId("turnstile-widget")).not.toBeInTheDocument();
  });

  it("does not gate submit when NEXT_PUBLIC_TURNSTILE_SITE_KEY is absent", () => {
    delete process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY;
    render(<CmaSection {...DEFAULT_PROPS} />);
    // When no Turnstile key, turnstileToken is passed as undefined so submit is not gated
    const submitButton = screen.getByRole("button", { name: /Get My Free Home Value Report/ });
    expect(submitButton).not.toBeDisabled();
  });

  it("gates submit when NEXT_PUBLIC_TURNSTILE_SITE_KEY is set but token not yet received", () => {
    process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY = "test-site-key";
    render(<CmaSection {...DEFAULT_PROPS} />);
    // Token starts as null — submit should be disabled until Turnstile resolves
    const submitButton = screen.getByRole("button", { name: /Get My Free Home Value Report/ });
    expect(submitButton).toBeDisabled();
  });
});

describe("CmaSection marketing consent", () => {
  it("includes marketingConsent in submitted data", async () => {
    mockSubmitLead.mockResolvedValueOnce({ leadId: "lead-789", status: "received" });

    const locationMock = { href: "" };
    Object.defineProperty(window, "location", { value: locationMock, writable: true });

    render(<CmaSection {...DEFAULT_PROPS} />);
    fillForm();

    await act(async () => {
      fireEvent.submit(screen.getByRole("button").closest("form")!);
    });

    expect(mockSubmitLead).toHaveBeenCalledWith(
      "test-agent",
      expect.objectContaining({
        marketingConsent: {
          optedIn: true,
          consentText: expect.stringContaining("consent to receive"),
          channels: ["calls", "texts"],
        },
      }),
      expect.any(String),
    );
  });
});
