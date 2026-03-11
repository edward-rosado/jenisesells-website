import { render, screen } from "@testing-library/react";
import RootLayout from "../app/layout";

vi.mock("next/link", () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

describe("RootLayout", () => {
  it("renders the brand name", () => {
    render(
      <RootLayout>
        <div>child</div>
      </RootLayout>
    );
    expect(screen.getByText("Real Estate Star")).toBeInTheDocument();
  });

  it("renders the Log In link", () => {
    render(
      <RootLayout>
        <div>child</div>
      </RootLayout>
    );
    expect(screen.getByRole("link", { name: /Log In/i })).toBeInTheDocument();
  });

  it("renders children", () => {
    render(
      <RootLayout>
        <div>test child</div>
      </RootLayout>
    );
    expect(screen.getByText("test child")).toBeInTheDocument();
  });

  it("renders the GeometricStar logo in the header", () => {
    render(
      <RootLayout>
        <div>child</div>
      </RootLayout>
    );
    expect(screen.getByRole("img", { hidden: true })).toBeInTheDocument();
  });

  it("header has frosted-glass backdrop-blur styling", () => {
    const { container } = render(
      <RootLayout>
        <div>child</div>
      </RootLayout>
    );
    const header = container.querySelector("header");
    expect(header).toHaveClass("backdrop-blur-md");
  });

  it("renders a footer with copyright", () => {
    render(
      <RootLayout>
        <div>child</div>
      </RootLayout>
    );
    expect(screen.getByText(/All rights reserved/i)).toBeInTheDocument();
    expect(screen.getByRole("contentinfo")).toBeInTheDocument();
  });
});
