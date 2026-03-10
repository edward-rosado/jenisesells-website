import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi } from "vitest";
import { ColorPalette } from "../../components/chat/ColorPalette";

describe("ColorPalette", () => {
  it("renders primary and accent labels", () => {
    render(
      <ColorPalette primaryColor="#ff0000" accentColor="#00ff00" onConfirm={() => {}} />
    );
    expect(screen.getByText("Primary")).toBeInTheDocument();
    expect(screen.getByText("Accent")).toBeInTheDocument();
  });

  it("shows color inputs when Customize clicked", async () => {
    render(
      <ColorPalette primaryColor="#ff0000" accentColor="#00ff00" onConfirm={() => {}} />
    );
    await userEvent.click(screen.getByRole("button", { name: /customize/i }));
    expect(screen.getByLabelText("Primary color")).toBeInTheDocument();
    expect(screen.getByLabelText("Accent color")).toBeInTheDocument();
  });

  it("calls onConfirm with colors", async () => {
    const onConfirm = vi.fn();
    render(
      <ColorPalette primaryColor="#ff0000" accentColor="#00ff00" onConfirm={onConfirm} />
    );
    await userEvent.click(screen.getByRole("button", { name: /confirm colors/i }));
    expect(onConfirm).toHaveBeenCalledWith({ primary: "#ff0000", accent: "#00ff00" });
  });
});
