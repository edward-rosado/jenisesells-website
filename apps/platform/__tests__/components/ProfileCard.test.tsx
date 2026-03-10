import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi } from "vitest";
import { ProfileCard } from "../../components/chat/ProfileCard";

describe("ProfileCard", () => {
  it("renders agent name", () => {
    render(<ProfileCard name="Jane Doe" onConfirm={() => {}} />);
    expect(screen.getByText("Jane Doe")).toBeInTheDocument();
  });

  it("renders brokerage when provided", () => {
    render(<ProfileCard name="Jane Doe" brokerage="RE/MAX" onConfirm={() => {}} />);
    expect(screen.getByText("RE/MAX")).toBeInTheDocument();
  });

  it("renders stats when provided", () => {
    render(
      <ProfileCard name="Jane Doe" homesSold={150} avgRating={4.9} onConfirm={() => {}} />
    );
    expect(screen.getByText("150 homes sold")).toBeInTheDocument();
    expect(screen.getByText("4.9 avg rating")).toBeInTheDocument();
  });

  it("calls onConfirm when button clicked", async () => {
    const onConfirm = vi.fn();
    render(<ProfileCard name="Jane Doe" onConfirm={onConfirm} />);
    await userEvent.click(screen.getByRole("button", { name: /looks right/i }));
    expect(onConfirm).toHaveBeenCalledOnce();
  });
});
