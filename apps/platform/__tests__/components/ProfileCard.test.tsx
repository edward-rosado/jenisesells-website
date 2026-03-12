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

  // ---- Additional branch coverage ----

  it("renders photo when photoUrl is provided", () => {
    render(
      <ProfileCard name="Jane Doe" photoUrl="https://example.com/photo.jpg" onConfirm={() => {}} />
    );
    const img = screen.getByAltText("Jane Doe");
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("src", "https://example.com/photo.jpg");
  });

  it("does not render photo when photoUrl is not provided", () => {
    render(<ProfileCard name="Jane Doe" onConfirm={() => {}} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("renders state when provided", () => {
    render(<ProfileCard name="Jane Doe" state="New Jersey" onConfirm={() => {}} />);
    expect(screen.getByText("New Jersey")).toBeInTheDocument();
  });

  it("does not render state when not provided", () => {
    render(<ProfileCard name="Jane Doe" onConfirm={() => {}} />);
    expect(screen.queryByText("New Jersey")).not.toBeInTheDocument();
  });

  it("does not render homesSold when not provided", () => {
    render(<ProfileCard name="Jane Doe" onConfirm={() => {}} />);
    expect(screen.queryByText(/homes sold/i)).not.toBeInTheDocument();
  });

  it("does not render avgRating when not provided", () => {
    render(<ProfileCard name="Jane Doe" onConfirm={() => {}} />);
    expect(screen.queryByText(/avg rating/i)).not.toBeInTheDocument();
  });

  it("renders all optional fields together", () => {
    render(
      <ProfileCard
        name="Jane Doe"
        brokerage="Keller Williams"
        state="NJ"
        photoUrl="https://example.com/j.jpg"
        homesSold={200}
        avgRating={5.0}
        onConfirm={() => {}}
      />
    );
    expect(screen.getByText("Jane Doe")).toBeInTheDocument();
    expect(screen.getByText("Keller Williams")).toBeInTheDocument();
    expect(screen.getByText("NJ")).toBeInTheDocument();
    expect(screen.getByAltText("Jane Doe")).toBeInTheDocument();
    expect(screen.getByText("200 homes sold")).toBeInTheDocument();
    expect(screen.getByText("5 avg rating")).toBeInTheDocument();
  });
});
