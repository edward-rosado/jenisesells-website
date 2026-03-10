import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi } from "vitest";
import { PaymentCard } from "../../components/chat/PaymentCard";

describe("PaymentCard", () => {
  it("renders the price", () => {
    render(<PaymentCard onPaymentComplete={() => {}} />);
    expect(screen.getByText("$900")).toBeInTheDocument();
  });

  it("renders the trial CTA button", () => {
    render(<PaymentCard onPaymentComplete={() => {}} />);
    expect(
      screen.getByRole("button", { name: /start free trial/i })
    ).toBeInTheDocument();
  });

  it("renders 7-day trial messaging", () => {
    render(<PaymentCard onPaymentComplete={() => {}} />);
    expect(screen.getByText(/7-day free trial/i)).toBeInTheDocument();
  });

  it("calls onPaymentComplete when button clicked", async () => {
    const onComplete = vi.fn();
    render(<PaymentCard onPaymentComplete={onComplete} />);
    await userEvent.click(
      screen.getByRole("button", { name: /start free trial/i })
    );
    expect(onComplete).toHaveBeenCalledOnce();
  });

  it("opens checkout URL in new tab when provided", async () => {
    const windowOpen = vi
      .spyOn(window, "open")
      .mockImplementation(() => null);
    const onComplete = vi.fn();
    const checkoutUrl = "https://checkout.stripe.com/c/pay_abc";

    render(
      <PaymentCard checkoutUrl={checkoutUrl} onPaymentComplete={onComplete} />
    );
    await userEvent.click(
      screen.getByRole("button", { name: /start free trial/i })
    );

    expect(windowOpen).toHaveBeenCalledWith(
      checkoutUrl,
      "_blank",
      "noopener,noreferrer"
    );
    expect(onComplete).toHaveBeenCalledOnce();

    windowOpen.mockRestore();
  });

  it("does not call window.open when no checkout URL", async () => {
    const windowOpen = vi
      .spyOn(window, "open")
      .mockImplementation(() => null);
    const onComplete = vi.fn();

    render(<PaymentCard onPaymentComplete={onComplete} />);
    await userEvent.click(
      screen.getByRole("button", { name: /start free trial/i })
    );

    expect(windowOpen).not.toHaveBeenCalled();
    expect(onComplete).toHaveBeenCalledOnce();

    windowOpen.mockRestore();
  });
});
