// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { EqualHousingNotice } from "./EqualHousingNotice";

describe("EqualHousingNotice", () => {
  it("renders Equal Housing Opportunity text", () => {
    render(<EqualHousingNotice />);
    expect(screen.getByText("Equal Housing Opportunity")).toBeInTheDocument();
  });

  it("renders the EHO logo SVG", () => {
    render(<EqualHousingNotice />);
    expect(
      screen.getByRole("img", { name: "Equal Housing Opportunity logo" }),
    ).toBeInTheDocument();
  });

  it("renders federal protected classes by default", () => {
    render(<EqualHousingNotice />);
    expect(
      screen.getByText(/race, color, religion, sex, national origin, disability, familial status/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/federal fair housing laws/),
    ).toBeInTheDocument();
  });

  it("renders NJ-specific protected classes when agentState is NJ", () => {
    render(<EqualHousingNotice agentState="NJ" />);
    expect(
      screen.getByText(/New Jersey fair housing laws/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/gender identity or expression/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/source of lawful income/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/domestic partnership or civil union status/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/affectional or sexual orientation/),
    ).toBeInTheDocument();
  });

  it("does NOT render NJ classes when agentState is TX", () => {
    render(<EqualHousingNotice agentState="TX" />);
    expect(
      screen.getByText(/federal fair housing laws/),
    ).toBeInTheDocument();
    expect(
      screen.queryByText(/gender identity or expression/),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByText(/source of lawful income/),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByText(/New Jersey/),
    ).not.toBeInTheDocument();
  });

  it("uses default text color when none provided", () => {
    const { container } = render(<EqualHousingNotice />);
    const wrapper = container.firstChild?.firstChild as HTMLElement;
    expect(wrapper.style.color).toBe("rgba(255, 255, 255, 0.7)");
  });

  it("uses custom text color when provided", () => {
    const { container } = render(
      <EqualHousingNotice textColor="rgba(0,0,0,0.7)" />,
    );
    const wrapper = container.firstChild?.firstChild as HTMLElement;
    expect(wrapper.style.color).toBe("rgba(0, 0, 0, 0.7)");
  });

  it("uses matching statement color when custom textColor is provided", () => {
    const { container } = render(
      <EqualHousingNotice textColor="rgba(0,0,0,0.7)" />,
    );
    const paragraph = container.querySelector("p") as HTMLElement;
    expect(paragraph.style.color).toBe("rgba(0, 0, 0, 0.7)");
  });

  it("uses same color for statement as default textColor", () => {
    const { container } = render(<EqualHousingNotice />);
    const paragraph = container.querySelector("p") as HTMLElement;
    expect(paragraph.style.color).toBe("rgba(255, 255, 255, 0.7)");
  });
});
