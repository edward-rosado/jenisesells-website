import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi } from "vitest";
import { ColorPalette } from "../ColorPalette";

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

  it("toggles editing mode and shows Done editing text", async () => {
    render(
      <ColorPalette primaryColor="#ff0000" accentColor="#00ff00" onConfirm={() => {}} />
    );
    expect(screen.getByRole("button", { name: /customize/i })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /customize/i }));
    expect(screen.getByRole("button", { name: /done editing/i })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /done editing/i }));
    expect(screen.getByRole("button", { name: /customize/i })).toBeInTheDocument();
  });

  it("allows changing primary color in editing mode", async () => {
    const onConfirm = vi.fn();
    render(
      <ColorPalette primaryColor="#ff0000" accentColor="#00ff00" onConfirm={onConfirm} />
    );
    await userEvent.click(screen.getByRole("button", { name: /customize/i }));

    const primaryInput = screen.getByLabelText("Primary color");
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")?.set?.call(
      primaryInput,
      "#0000ff"
    );
    primaryInput.dispatchEvent(new Event("input", { bubbles: true }));

    await userEvent.click(screen.getByRole("button", { name: /confirm colors/i }));
    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it("allows changing accent color in editing mode", async () => {
    const onConfirm = vi.fn();
    render(
      <ColorPalette primaryColor="#ff0000" accentColor="#00ff00" onConfirm={onConfirm} />
    );
    await userEvent.click(screen.getByRole("button", { name: /customize/i }));

    const accentInput = screen.getByLabelText("Accent color");
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")?.set?.call(
      accentInput,
      "#ff00ff"
    );
    accentInput.dispatchEvent(new Event("input", { bubbles: true }));

    await userEvent.click(screen.getByRole("button", { name: /confirm colors/i }));
    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it("does not show color inputs before entering editing mode", () => {
    render(
      <ColorPalette primaryColor="#ff0000" accentColor="#00ff00" onConfirm={() => {}} />
    );
    expect(screen.queryByLabelText("Primary color")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Accent color")).not.toBeInTheDocument();
  });

  it("renders Brand Colors heading", () => {
    render(
      <ColorPalette primaryColor="#ff0000" accentColor="#00ff00" onConfirm={() => {}} />
    );
    expect(screen.getByText("Brand Colors")).toBeInTheDocument();
  });
});
