import { render, screen } from "@testing-library/react";

vi.mock("next/link", () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

describe("RootLayout", () => {
  // Dynamic import to avoid issues with layout's <html>/<body> tags in test
  let RootLayout: typeof import("../app/layout").default;

  beforeAll(async () => {
    const mod = await import("../app/layout");
    RootLayout = mod.default;
  });

  it("renders the brand name", () => {
    render(
      <RootLayout>
        <div>child</div>
      </RootLayout>
    );
    expect(screen.getByText("Real Estate Star")).toBeInTheDocument();
  });

  it("renders the header with brand link only (no login link)", () => {
    render(
      <RootLayout>
        <div>child</div>
      </RootLayout>
    );
    const header = screen.getByRole("banner") || document.querySelector("header");
    expect(screen.queryByRole("link", { name: /Log In/i })).not.toBeInTheDocument();
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
    const imgs = screen.getAllByRole("img", { hidden: true });
    expect(imgs.length).toBeGreaterThanOrEqual(1);
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

  it("renders a skip navigation link for ADA compliance", () => {
    render(
      <RootLayout>
        <div>child</div>
      </RootLayout>
    );
    const skipLink = screen.getByText("Skip to main content");
    expect(skipLink).toBeInTheDocument();
    expect(skipLink).toHaveAttribute("href", "#main-content");
  });

  it("brand link points to home", () => {
    render(
      <RootLayout>
        <div>child</div>
      </RootLayout>
    );
    const brandLink = screen.getByRole("link", { name: /real estate star home/i });
    expect(brandLink).toHaveAttribute("href", "/");
  });

  it("renders footer legal links", () => {
    render(
      <RootLayout>
        <div>child</div>
      </RootLayout>
    );
    const legalNav = screen.getByRole("navigation", { name: /legal links/i });
    const links = legalNav.querySelectorAll("a");
    const hrefs = Array.from(links).map((a) => a.getAttribute("href"));
    expect(hrefs).toContain("/privacy");
    expect(hrefs).toContain("/terms");
    expect(hrefs).toContain("/dmca");
    expect(hrefs).toContain("/accessibility");
  });

  it("renders the Equal Housing Opportunity statement in footer", () => {
    render(
      <RootLayout>
        <div>child</div>
      </RootLayout>
    );
    expect(screen.getByText("Equal Housing Opportunity")).toBeInTheDocument();
  });

  it("renders the legal links navigation with aria-label", () => {
    render(
      <RootLayout>
        <div>child</div>
      </RootLayout>
    );
    expect(screen.getByRole("navigation", { name: /legal links/i })).toBeInTheDocument();
  });
});
