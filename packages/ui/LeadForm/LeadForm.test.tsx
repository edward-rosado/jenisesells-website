// @vitest-environment jsdom
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { LeadForm } from "./LeadForm";
import type { LeadFormData } from "@real-estate-star/shared-types";

let capturedOnPlaceSelected: ((place: { address: string; city: string; state: string; zip: string }) => void) | null = null;

vi.mock("./useGoogleMapsAutocomplete", () => ({
  useGoogleMapsAutocomplete: (opts: { onPlaceSelected: (place: { address: string; city: string; state: string; zip: string }) => void }) => {
    capturedOnPlaceSelected = opts.onPlaceSelected;
    return { loaded: true };
  },
}));

const defaultProps = {
  defaultState: "NJ",
  googleMapsApiKey: "test-api-key",
  onSubmit: vi.fn(),
};

function fillContactFields() {
  fireEvent.change(screen.getByLabelText(/first name/i), {
    target: { value: "Jane" },
  });
  fireEvent.change(screen.getByLabelText(/last name/i), {
    target: { value: "Doe" },
  });
  fireEvent.change(screen.getByLabelText(/email/i), {
    target: { value: "jane@example.com" },
  });
  fireEvent.change(document.getElementById("lf-phone")!, {
    target: { value: "555-123-4567" },
  });
}

function fillBuyerFields() {
  // When serviceAreas is provided, it's a select; otherwise input
  const desiredArea = screen.getByLabelText(/desired area/i);
  fireEvent.change(desiredArea, {
    target: { value: "Hoboken" },
  });
  fireEvent.change(screen.getByLabelText(/min price/i), {
    target: { value: "200000" },
  });
  fireEvent.change(screen.getByLabelText(/max price/i), {
    target: { value: "500000" },
  });
  fireEvent.change(screen.getByLabelText(/min beds/i), {
    target: { value: "3" },
  });
  fireEvent.change(screen.getByLabelText(/min baths/i), {
    target: { value: "2" },
  });
  fireEvent.change(screen.getByLabelText(/pre-approved/i), {
    target: { value: "yes" },
  });
}

function fillSellerFields() {
  fireEvent.change(screen.getByLabelText(/property address/i), {
    target: { value: "123 Main St" },
  });
  fireEvent.change(screen.getByLabelText(/city/i), {
    target: { value: "Newark" },
  });
  // State should already be pre-filled from defaultState
  fireEvent.change(screen.getByLabelText(/zip/i), {
    target: { value: "07102" },
  });
  fireEvent.change(screen.getByLabelText("Beds"), {
    target: { value: "4" },
  });
  fireEvent.change(screen.getByLabelText("Baths"), {
    target: { value: "2" },
  });
  fireEvent.change(screen.getByLabelText(/sqft/i), {
    target: { value: "2000" },
  });
}

function selectTimeline(value: string = "asap") {
  fireEvent.change(screen.getByLabelText(/when are you looking/i), {
    target: { value },
  });
}

function checkBuying() {
  fireEvent.click(screen.getByLabelText(/i'm buying/i));
}

function checkSelling() {
  fireEvent.click(screen.getByLabelText(/i'm selling/i));
}

function checkTcpaConsent() {
  fireEvent.click(screen.getByRole("checkbox", { name: /consent to receive/i }));
}

function submitForm() {
  fireEvent.click(screen.getByRole("button", { name: /get started/i }));
}

describe("LeadForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // Test 1
  it("renders all contact fields with accessible labels", () => {
    render(<LeadForm {...defaultProps} />);

    expect(screen.getByLabelText(/first name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/last name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    expect(document.getElementById("lf-phone")).toBeInTheDocument();
  });

  // Test 2
  it("renders pill checkboxes for 'I'm Buying' and 'I'm Selling'", () => {
    render(<LeadForm {...defaultProps} />);

    expect(screen.getByLabelText(/i'm buying/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/i'm selling/i)).toBeInTheDocument();
  });

  // Test 3
  it("shows buyer card when 'I'm Buying' is checked", () => {
    render(<LeadForm {...defaultProps} />);

    checkBuying();

    const buyerCard = screen.getByTestId("buyer-card");
    expect(buyerCard).toBeVisible();
  });

  // Test 4
  it("hides buyer card when 'I'm Buying' is unchecked", () => {
    render(<LeadForm {...defaultProps} />);

    // Check then uncheck
    checkBuying();
    checkBuying();

    const buyerCard = screen.getByTestId("buyer-card");
    expect(buyerCard).toHaveStyle({ maxHeight: "0" });
  });

  // Test 5
  it("shows seller card when 'I'm Selling' is checked", () => {
    render(<LeadForm {...defaultProps} />);

    checkSelling();

    const sellerCard = screen.getByTestId("seller-card");
    expect(sellerCard).toBeVisible();
  });

  // Test 6
  it("hides seller card when 'I'm Selling' is unchecked", () => {
    render(<LeadForm {...defaultProps} />);

    // Check then uncheck
    checkSelling();
    checkSelling();

    const sellerCard = screen.getByTestId("seller-card");
    expect(sellerCard).toHaveStyle({ maxHeight: "0" });
  });

  // Test 7
  it("shows both cards when both checkboxes are checked", () => {
    render(<LeadForm {...defaultProps} />);

    checkBuying();
    checkSelling();

    expect(screen.getByTestId("buyer-card")).toBeVisible();
    expect(screen.getByTestId("seller-card")).toBeVisible();
  });

  // Test 8
  it("prevents submission with neither checkbox checked and shows validation error", () => {
    const onSubmit = vi.fn();
    render(<LeadForm {...defaultProps} onSubmit={onSubmit} />);

    fillContactFields();
    submitForm();

    expect(onSubmit).not.toHaveBeenCalled();
    expect(
      screen.getByText(/please select at least one/i)
    ).toBeInTheDocument();
  });

  // Test 9
  it("calls onSubmit with correct LeadFormData shape — buyer only", async () => {
    const onSubmit = vi.fn();
    render(<LeadForm {...defaultProps} onSubmit={onSubmit} />);

    checkBuying();
    fillContactFields();
    fillBuyerFields();
    selectTimeline("1-3months");
    checkTcpaConsent();
    submitForm();

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    const data: LeadFormData = onSubmit.mock.calls[0][0];
    expect(data.leadTypes).toEqual(["buying"]);
    expect(data.firstName).toBe("Jane");
    expect(data.lastName).toBe("Doe");
    expect(data.email).toBe("jane@example.com");
    expect(data.phone).toBe("555-123-4567");
    expect(data.timeline).toBe("1-3months");
    expect(data.buyer).toBeDefined();
    expect(data.buyer!.desiredArea).toBe("Hoboken");
    expect(data.buyer!.minPrice).toBe(200000);
    expect(data.buyer!.maxPrice).toBe(500000);
    expect(data.buyer!.minBeds).toBe(3);
    expect(data.buyer!.minBaths).toBe(2);
    expect(data.buyer!.preApproved).toBe("yes");
    expect(data.seller).toBeUndefined();
  });

  // Test 10
  it("calls onSubmit with correct LeadFormData shape — seller only", async () => {
    const onSubmit = vi.fn();
    render(<LeadForm {...defaultProps} onSubmit={onSubmit} />);

    checkSelling();
    fillContactFields();
    fillSellerFields();
    selectTimeline("asap");
    checkTcpaConsent();
    submitForm();

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    const data: LeadFormData = onSubmit.mock.calls[0][0];
    expect(data.leadTypes).toEqual(["selling"]);
    expect(data.firstName).toBe("Jane");
    expect(data.lastName).toBe("Doe");
    expect(data.email).toBe("jane@example.com");
    expect(data.phone).toBe("555-123-4567");
    expect(data.timeline).toBe("asap");
    expect(data.seller).toBeDefined();
    expect(data.seller!.address).toBe("123 Main St");
    expect(data.seller!.city).toBe("Newark");
    expect(data.seller!.state).toBe("NJ");
    expect(data.seller!.zip).toBe("07102");
    expect(data.seller!.beds).toBe(4);
    expect(data.seller!.baths).toBe(2);
    expect(data.seller!.sqft).toBe(2000);
    expect(data.buyer).toBeUndefined();
  });

  // Test 11
  it("calls onSubmit with correct LeadFormData shape — both buyer and seller", async () => {
    const onSubmit = vi.fn();
    render(<LeadForm {...defaultProps} onSubmit={onSubmit} />);

    checkBuying();
    checkSelling();
    fillContactFields();
    fillBuyerFields();
    fillSellerFields();
    selectTimeline("3-6months");
    checkTcpaConsent();
    submitForm();

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    const data: LeadFormData = onSubmit.mock.calls[0][0];
    expect(data.leadTypes).toContain("buying");
    expect(data.leadTypes).toContain("selling");
    expect(data.buyer).toBeDefined();
    expect(data.seller).toBeDefined();
    expect(data.timeline).toBe("3-6months");
  });

  // Test 12
  it("pre-fills state field from defaultState prop", () => {
    render(<LeadForm {...defaultProps} defaultState="CA" />);

    checkSelling();

    const stateInput = screen.getByLabelText(/state/i);
    expect(stateInput).toHaveValue("CA");
  });

  // Test 13
  it("respects initialMode prop — pre-checks specified pills on mount", () => {
    render(
      <LeadForm {...defaultProps} initialMode={["buying", "selling"]} />
    );

    expect(screen.getByLabelText(/i'm buying/i)).toBeChecked();
    expect(screen.getByLabelText(/i'm selling/i)).toBeChecked();
    expect(screen.getByTestId("buyer-card")).toBeVisible();
    expect(screen.getByTestId("seller-card")).toBeVisible();
  });

  // Test 14
  it("respects submitLabel prop with custom button text", () => {
    render(<LeadForm {...defaultProps} submitLabel="Send Request" />);

    expect(
      screen.getByRole("button", { name: /send request/i })
    ).toBeInTheDocument();
  });

  // Test 15
  it("respects disabled prop — button is disabled", () => {
    render(<LeadForm {...defaultProps} disabled />);

    expect(
      screen.getByRole("button", { name: /get started/i })
    ).toBeDisabled();
  });

  // Test 16
  it("timeline label shows 'sell' when only selling, 'buy' when only buying, 'buy/sell' when both", () => {
    render(<LeadForm {...defaultProps} />);

    // Buying only
    checkBuying();
    expect(screen.getByText(/when are you looking to buy\?/i)).toBeInTheDocument();

    // Uncheck buying, check selling
    checkBuying();
    checkSelling();
    expect(screen.getByText(/when are you looking to sell\?/i)).toBeInTheDocument();

    // Check both
    checkBuying();
    expect(screen.getByText(/when are you looking to buy\/sell\?/i)).toBeInTheDocument();
  });

  // Test 17
  it("all inputs have accessible labels", () => {
    render(
      <LeadForm {...defaultProps} initialMode={["buying", "selling"]} />
    );

    // Contact fields
    expect(screen.getByLabelText(/first name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/last name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    expect(document.getElementById("lf-phone")).toBeInTheDocument();

    // Buyer fields
    expect(screen.getByLabelText(/desired area/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/min price/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/max price/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/min beds/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/min baths/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/pre-approved/i)).toBeInTheDocument();

    // Seller fields
    expect(screen.getByLabelText(/property address/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/city/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/state/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/zip/i)).toBeInTheDocument();

    // Shared fields
    expect(screen.getByLabelText(/when are you looking/i)).toBeInTheDocument();
  });

  // Test 18
  it("does not call onSubmit twice while first call is in flight", async () => {
    let resolveSubmit: () => void;
    const onSubmit = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          resolveSubmit = resolve;
        })
    );
    render(
      <LeadForm {...defaultProps} onSubmit={onSubmit} initialMode={["buying"]} />
    );

    fillContactFields();
    fillBuyerFields();
    selectTimeline();
    checkTcpaConsent();

    // First submit
    submitForm();
    // Second submit while first is in flight
    submitForm();

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    // Resolve the first call
    resolveSubmit!();

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });
  });

  // Test 19
  it("does not crash if onSubmit throws — resets submitting state", async () => {
    const onSubmit = vi.fn().mockRejectedValue(new Error("Network error"));
    render(
      <LeadForm {...defaultProps} onSubmit={onSubmit} initialMode={["selling"]} />
    );

    fillContactFields();
    fillSellerFields();
    selectTimeline();
    checkTcpaConsent();
    submitForm();

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    // Button should be re-enabled after error (submitting state reset)
    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: /get started/i })
      ).not.toBeDisabled();
    });
  });

  // Test 20
  it("shows error prop value when provided by consumer", () => {
    render(
      <LeadForm {...defaultProps} error="Something went wrong, please try again." />
    );

    expect(
      screen.getByText(/something went wrong, please try again/i)
    ).toBeInTheDocument();
  });

  // Test 21
  it("renders Desired Area as a dropdown when serviceAreas are provided", () => {
    render(
      <LeadForm {...defaultProps} initialMode={["buying"]} serviceAreas={["Middlesex County", "Monmouth County", "Ocean County"]} />
    );

    const select = screen.getByLabelText(/desired area/i);
    expect(select.tagName).toBe("SELECT");
    expect(screen.getByText("Middlesex County")).toBeInTheDocument();
    expect(screen.getByText("Monmouth County")).toBeInTheDocument();
    expect(screen.getByText("Ocean County")).toBeInTheDocument();
  });

  // Test 22
  it("renders Desired Area as a free-form input when no serviceAreas provided", () => {
    render(
      <LeadForm {...defaultProps} initialMode={["buying"]} />
    );

    const input = screen.getByLabelText(/desired area/i);
    expect(input.tagName).toBe("INPUT");
  });

  // Test 23
  it("supports dynamic submitLabel as a function", () => {
    render(
      <LeadForm
        {...defaultProps}
        initialMode={["selling"]}
        submitLabel={(isBuying, isSelling) => {
          if (isSelling) return "Get CMA Report";
          if (isBuying) return "Connect Me";
          return "Get Started";
        }}
      />
    );

    expect(screen.getByRole("button", { name: /get cma report/i })).toBeInTheDocument();
  });

  // Test 24
  it("dynamic submitLabel updates when pills change", () => {
    render(
      <LeadForm
        {...defaultProps}
        submitLabel={(isBuying, isSelling) => {
          if (isSelling) return "Get CMA Report";
          if (isBuying) return "Connect Me";
          return "Get Started";
        }}
      />
    );

    // Neither selected — default
    expect(screen.getByRole("button", { name: /get started/i })).toBeInTheDocument();

    // Check buying only
    checkBuying();
    expect(screen.getByRole("button", { name: /connect me/i })).toBeInTheDocument();

    // Check selling (both now)
    checkSelling();
    expect(screen.getByRole("button", { name: /get cma report/i })).toBeInTheDocument();
  });

  // Test 25
  it("clears validation error when a pill is selected", () => {
    render(<LeadForm {...defaultProps} />);

    fillContactFields();
    submitForm();

    expect(screen.getByText(/please select at least one/i)).toBeInTheDocument();

    // Select buying — error should clear
    checkBuying();
    expect(screen.queryByText(/please select at least one/i)).not.toBeInTheDocument();
  });

  // Test 26
  it("pills stretch to fill the full width of the container", () => {
    render(<LeadForm {...defaultProps} />);

    const buyingPill = screen.getByLabelText(/i'm buying/i).closest("span");
    expect(buyingPill).toHaveStyle({ flex: "1" });
  });

  // Test 27
  it("pills get red border when validation error is shown", () => {
    render(<LeadForm {...defaultProps} />);

    fillContactFields();
    submitForm();

    const buyingPill = screen.getByLabelText(/i'm buying/i).closest("span")!;
    expect(buyingPill.style.borderColor).toBe("red");
  });

  // Test 28
  it("prevents submission when no timeline is selected and shows validation error", () => {
    const onSubmit = vi.fn();
    render(<LeadForm {...defaultProps} onSubmit={onSubmit} />);

    checkBuying();
    fillContactFields();
    fillBuyerFields();
    // Deliberately skip selectTimeline()
    // Use fireEvent.submit to bypass native required validation in jsdom
    fireEvent.submit(screen.getByRole("button", { name: /get started/i }).closest("form")!);

    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/please select a timeline/i)).toBeInTheDocument();
  });

  // Test 29
  it("error messages are in an aria-live region for screen readers", () => {
    render(<LeadForm {...defaultProps} />);

    // Use fireEvent.submit to bypass native required validation in jsdom
    fireEvent.submit(screen.getByRole("button", { name: /get started/i }).closest("form")!);

    const alertRegion = screen.getByRole("alert");
    expect(alertRegion).toHaveAttribute("aria-live", "polite");
    expect(alertRegion).toHaveTextContent(/please select at least one/i);
  });

  // Test 30
  it("shows validation error when autocomplete selects an out-of-state address", () => {
    render(<LeadForm {...defaultProps} defaultState="NJ" initialMode={["selling"]} />);

    act(() => {
      capturedOnPlaceSelected!({
        address: "100 Broadway",
        city: "New York",
        state: "NY",
        zip: "10001",
      });
    });

    expect(screen.getByText(/only serve NJ/i)).toBeInTheDocument();
    expect(screen.getByText(/address you selected is in NY/i)).toBeInTheDocument();
    // Address fields should be cleared
    expect(screen.getByLabelText(/property address/i)).toHaveValue("");
    expect(screen.getByLabelText(/city/i)).toHaveValue("");
    expect(screen.getByLabelText(/zip/i)).toHaveValue("");
    // State should reset to defaultState
    expect(screen.getByLabelText(/state/i)).toHaveValue("NJ");
  });

  // Test 31
  it("accepts autocomplete selection when state matches defaultState", () => {
    render(<LeadForm {...defaultProps} defaultState="NJ" initialMode={["selling"]} />);

    act(() => {
      capturedOnPlaceSelected!({
        address: "123 Main St",
        city: "Newark",
        state: "NJ",
        zip: "07102",
      });
    });

    expect(screen.queryByText(/only serve/i)).not.toBeInTheDocument();
    expect(screen.getByLabelText(/property address/i)).toHaveValue("123 Main St");
    expect(screen.getByLabelText(/city/i)).toHaveValue("Newark");
    expect(screen.getByLabelText(/state/i)).toHaveValue("NJ");
    expect(screen.getByLabelText(/zip/i)).toHaveValue("07102");
  });

  // Test 32
  it("blocks submission when seller state does not match defaultState", async () => {
    const onSubmit = vi.fn();
    render(<LeadForm {...defaultProps} defaultState="NJ" onSubmit={onSubmit} initialMode={["selling"]} />);

    fillContactFields();
    fillSellerFields();
    selectTimeline();
    checkTcpaConsent();

    // Simulate someone altering the state field via DOM manipulation
    // (the field is readOnly, but can be changed via DevTools)
    // We use the captured callback to set an out-of-state value, then manually
    // fix the address so the form looks valid, but state is wrong
    act(() => {
      capturedOnPlaceSelected!({
        address: "100 Broadway",
        city: "New York",
        state: "PA",
        zip: "19101",
      });
    });
    // Error from autocomplete fires, clear it and re-fill fields to simulate
    // a user who dismissed the autocomplete error and edited DOM
    // Instead, let's directly set the state via a fresh autocomplete pick
    // that bypasses the check — simulate the tampered scenario:
    // The readOnly field already has "PA" from the rejected pick being cleared,
    // so let's use a different approach: accept a valid address first, then
    // call the callback with wrong state to leave it in a bad state

    // Simpler: just accept the NJ address, then use act to simulate
    // the state being tampered after autocomplete
    act(() => {
      capturedOnPlaceSelected!({
        address: "123 Main St",
        city: "Newark",
        state: "NJ",
        zip: "07102",
      });
    });

    // Now simulate DOM tampering by firing change on the readOnly state field
    // Since it's readOnly, we force it via the updateField pathway
    const stateInput = screen.getByLabelText(/state/i);
    // Override the readOnly by directly dispatching a change event
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(stateInput, 'PA');
    fireEvent.change(stateInput, { target: { value: "PA" } });

    // Submit the form
    fireEvent.submit(screen.getByRole("button").closest("form")!);

    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/only serve NJ/i)).toBeInTheDocument();
    expect(screen.getByText(/address you entered is in PA/i)).toBeInTheDocument();
  });

  // Test 33
  it("clears out-of-state error when a valid address is selected", () => {
    render(<LeadForm {...defaultProps} defaultState="NJ" initialMode={["selling"]} />);

    act(() => {
      capturedOnPlaceSelected!({
        address: "100 Broadway",
        city: "New York",
        state: "NY",
        zip: "10001",
      });
    });
    expect(screen.getByText(/only serve NJ/i)).toBeInTheDocument();

    act(() => {
      capturedOnPlaceSelected!({
        address: "456 Park Ave",
        city: "Hoboken",
        state: "NJ",
        zip: "07030",
      });
    });
    expect(screen.queryByText(/only serve/i)).not.toBeInTheDocument();
    expect(screen.getByLabelText(/property address/i)).toHaveValue("456 Park Ave");
  });

  // Test 34
  it("renders TCPA consent checkbox unchecked by default", () => {
    render(<LeadForm {...defaultProps} />);
    const checkbox = screen.getByRole("checkbox", { name: /consent to receive/i });
    expect(checkbox).toBeInTheDocument();
    expect(checkbox).not.toBeChecked();
  });

  // Test 35
  it("blocks submit when TCPA consent is not checked", () => {
    const onSubmit = vi.fn();
    render(<LeadForm {...defaultProps} onSubmit={onSubmit} initialMode={["buying"]} />);

    fillContactFields();
    fillBuyerFields();
    selectTimeline();
    // Do NOT check TCPA consent
    fireEvent.submit(screen.getByRole("button", { name: /get started/i }).closest("form")!);

    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/you must consent/i)).toBeInTheDocument();
  });

  // Test 36 — CMA disclaimer
  it("shows CMA disclaimer when showCmaDisclaimer is true", () => {
    render(<LeadForm {...defaultProps} showCmaDisclaimer />);
    expect(screen.getByText(/not an appraisal/i)).toBeInTheDocument();
    expect(screen.getByText(/licensed appraiser/i)).toBeInTheDocument();
  });

  // Test 37
  it("does not show CMA disclaimer when showCmaDisclaimer is false or undefined", () => {
    const { rerender } = render(<LeadForm {...defaultProps} />);
    expect(screen.queryByText(/not an appraisal/i)).not.toBeInTheDocument();

    rerender(<LeadForm {...defaultProps} showCmaDisclaimer={false} />);
    expect(screen.queryByText(/not an appraisal/i)).not.toBeInTheDocument();
  });

  // Test 38 — Google Maps attribution
  it("shows Google Maps attribution when selling mode is active", () => {
    render(<LeadForm {...defaultProps} initialMode={["selling"]} />);
    expect(screen.getByText(/address autocomplete powered by google maps/i)).toBeInTheDocument();
  });

  // Test 39
  it("does not show Google Maps attribution when selling mode is inactive", () => {
    render(<LeadForm {...defaultProps} initialMode={["buying"]} />);
    expect(screen.queryByText(/address autocomplete powered by google maps/i)).not.toBeInTheDocument();
  });

  // Test 40
  it("allows submit when TCPA consent is checked", async () => {
    const onSubmit = vi.fn();
    render(<LeadForm {...defaultProps} onSubmit={onSubmit} initialMode={["buying"]} />);

    fillContactFields();
    fillBuyerFields();
    selectTimeline();
    fireEvent.click(screen.getByRole("checkbox", { name: /consent to receive/i }));
    submitForm();

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalled();
    });
  });
});
