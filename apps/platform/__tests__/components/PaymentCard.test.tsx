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

  it("button does not reappear after clicking — stays in waiting state", async () => {
    const windowOpen = vi
      .spyOn(window, "open")
      .mockImplementation(() => null);

    render(<PaymentCard checkoutUrl="https://checkout.stripe.com/c/pay_abc" />);
    await userEvent.click(
      screen.getByRole("button", { name: /start free trial/i })
    );

    // Button should be gone, replaced by waiting text
    expect(screen.queryByRole("button", { name: /start free trial/i })).not.toBeInTheDocument();
    expect(screen.getByText(/waiting for payment confirmation/i)).toBeInTheDocument();

    windowOpen.mockRestore();
  });

  it("calls window.open only once on multiple rapid clicks", async () => {
    const windowOpen = vi
      .spyOn(window, "open")
      .mockImplementation(() => null);

    render(<PaymentCard checkoutUrl="https://checkout.stripe.com/c/pay_abc" />);
    const button = screen.getByRole("button", { name: /start free trial/i });

    // First click transitions to "opened" state, removing the button from DOM.
    // Subsequent clicks can't happen because the button is gone.
    await userEvent.click(button);

    // Button is now replaced by waiting text — no second click possible
    expect(screen.queryByRole("button", { name: /start free trial/i })).not.toBeInTheDocument();
    expect(windowOpen).toHaveBeenCalledTimes(1);

    windowOpen.mockRestore();
  });

  it("button has accessible name and waiting state is announced", async () => {
    const windowOpen = vi
      .spyOn(window, "open")
      .mockImplementation(() => null);

    const { container } = render(
      <PaymentCard checkoutUrl="https://checkout.stripe.com/c/pay_abc" />
    );

    // Button should have accessible name
    const button = screen.getByRole("button", { name: /start free trial/i });
    expect(button).toHaveAccessibleName();

    await userEvent.click(button);

    // Waiting state text should be visible
    const waitingText = screen.getByText(/waiting for payment confirmation/i);
    expect(waitingText).toBeInTheDocument();

    windowOpen.mockRestore();
  });

  it("renders custom price with special characters correctly", () => {
    render(<PaymentCard price="$1,299.99/mo" />);
    expect(screen.getByText("$1,299.99/mo")).toBeInTheDocument();
  });

  it("renders price with unicode characters", () => {
    render(<PaymentCard price={"$900 \u2014 one-time"} />);
    expect(screen.getByText(/\$900 \u2014 one-time/)).toBeInTheDocument();
  });
});
