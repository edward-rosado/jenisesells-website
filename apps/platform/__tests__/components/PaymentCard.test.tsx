import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi } from "vitest";
import { PaymentCard } from "../../components/chat/PaymentCard";

describe("PaymentCard", () => {
  it("renders the default price when none provided", () => {
    render(<PaymentCard />);
    expect(screen.getByText("$900")).toBeInTheDocument();
  });

  it("renders a custom price when provided", () => {
    render(<PaymentCard price="$1,200" />);
    expect(screen.getByText("$1,200")).toBeInTheDocument();
  });

  it("renders the trial CTA button", () => {
    render(<PaymentCard />);
    expect(
      screen.getByRole("button", { name: /start free trial/i })
    ).toBeInTheDocument();
  });

  it("renders 7-day trial messaging", () => {
    render(<PaymentCard />);
    expect(screen.getByText(/7-day free trial/i)).toBeInTheDocument();
  });

  it("opens checkout URL in new tab when provided", async () => {
    const windowOpen = vi
      .spyOn(window, "open")
      .mockImplementation(() => null);
    const checkoutUrl = "https://checkout.stripe.com/c/pay_abc";

    render(<PaymentCard checkoutUrl={checkoutUrl} />);
    await userEvent.click(
      screen.getByRole("button", { name: /start free trial/i })
    );

    expect(windowOpen).toHaveBeenCalledWith(
      checkoutUrl,
      "_blank",
      "noopener,noreferrer"
    );

    windowOpen.mockRestore();
  });

  it("disables button when no checkout URL", () => {
    render(<PaymentCard />);
    expect(
      screen.getByRole("button", { name: /start free trial/i })
    ).toBeDisabled();
  });

  it("shows waiting message after clicking checkout", async () => {
    const windowOpen = vi
      .spyOn(window, "open")
      .mockImplementation(() => null);

    render(<PaymentCard checkoutUrl="https://checkout.stripe.com/c/pay_abc" />);
    await userEvent.click(
      screen.getByRole("button", { name: /start free trial/i })
    );

    expect(screen.getByText(/waiting for payment confirmation/i)).toBeInTheDocument();

    windowOpen.mockRestore();
  });
});
