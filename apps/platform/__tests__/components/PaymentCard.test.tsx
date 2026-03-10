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

  it("calls onPaymentComplete when button clicked", async () => {
    const onComplete = vi.fn();
    render(<PaymentCard onPaymentComplete={onComplete} />);
    await userEvent.click(screen.getByRole("button", { name: /start free trial/i }));
    expect(onComplete).toHaveBeenCalledOnce();
  });
});
