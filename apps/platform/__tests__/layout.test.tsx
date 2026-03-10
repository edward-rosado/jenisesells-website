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
});
