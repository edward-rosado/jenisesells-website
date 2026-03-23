import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi } from "vitest";
import { SitePreview } from "../SitePreview";

describe("SitePreview", () => {
  it("renders an iframe with the site URL", () => {
    render(<SitePreview siteUrl="https://example.realestatestar.com" onApprove={() => {}} />);
    const iframe = screen.getByTitle("Site preview");
    expect(iframe).toBeInTheDocument();
    expect(iframe).toHaveAttribute("src", "https://example.realestatestar.com");
  });

  it("sandboxes iframe with allow-scripts but not allow-same-origin", () => {
    render(<SitePreview siteUrl="https://test.realestatestar.com" onApprove={() => {}} />);
    const iframe = screen.getByTitle("Site preview");
    expect(iframe).toHaveAttribute("sandbox", "allow-scripts");
  });

  it("calls onApprove when button clicked", async () => {
    const onApprove = vi.fn();
    render(<SitePreview siteUrl="https://test.realestatestar.com" onApprove={onApprove} />);
    await userEvent.click(screen.getByRole("button", { name: /approve/i }));
    expect(onApprove).toHaveBeenCalledOnce();
  });

  it("appends #cma-form anchor when showCmaHighlight is true", () => {
    render(
      <SitePreview
        siteUrl="https://test.realestatestar.com"
        onApprove={() => {}}
        showCmaHighlight
      />
    );
    const iframe = screen.getByTitle("CMA form preview");
    expect(iframe).toHaveAttribute("src", "https://test.realestatestar.com#cma-form");
  });

  it("shows 'Your CMA Form' title when showCmaHighlight is true", () => {
    render(
      <SitePreview
        siteUrl="https://test.realestatestar.com"
        onApprove={() => {}}
        showCmaHighlight
      />
    );
    expect(screen.getByText("Your CMA Form")).toBeInTheDocument();
  });

  it("shows CMA description text when showCmaHighlight is true", () => {
    render(
      <SitePreview
        siteUrl="https://test.realestatestar.com"
        onApprove={() => {}}
        showCmaHighlight
      />
    );
    expect(screen.getByText(/cma form on your website/i)).toBeInTheDocument();
  });

  it("hides Approve button when showCmaHighlight is true", () => {
    render(
      <SitePreview
        siteUrl="https://test.realestatestar.com"
        onApprove={() => {}}
        showCmaHighlight
      />
    );
    expect(screen.queryByRole("button", { name: /approve/i })).not.toBeInTheDocument();
  });

  it("shows Approve button when showCmaHighlight is not set", () => {
    render(
      <SitePreview
        siteUrl="https://test.realestatestar.com"
        onApprove={() => {}}
      />
    );
    expect(screen.getByRole("button", { name: /approve/i })).toBeInTheDocument();
  });

  it("blocks unsafe URLs and shows error message", () => {
    render(<SitePreview siteUrl="https://evil.example.com" onApprove={() => {}} />);
    expect(screen.queryByTitle("Site preview")).not.toBeInTheDocument();
    expect(screen.getByText("Unable to preview this URL")).toBeInTheDocument();
  });

  it("allows localhost URLs", () => {
    render(<SitePreview siteUrl="http://localhost:3000" onApprove={() => {}} />);
    const iframe = screen.getByTitle("Site preview");
    expect(iframe).toHaveAttribute("src", "http://localhost:3000");
  });

  it("allows .pages.dev URLs", () => {
    render(<SitePreview siteUrl="https://my-site.pages.dev" onApprove={() => {}} />);
    const iframe = screen.getByTitle("Site preview");
    expect(iframe).toHaveAttribute("src", "https://my-site.pages.dev");
  });

  it("shows error for completely invalid URL (triggers URL parse catch)", () => {
    render(<SitePreview siteUrl="not-a-url-at-all" onApprove={() => {}} />);
    expect(screen.queryByTitle("Site preview")).not.toBeInTheDocument();
    expect(screen.getByText("Unable to preview this URL")).toBeInTheDocument();
  });

  it("blocks ftp:// protocol URLs", () => {
    render(<SitePreview siteUrl="ftp://files.example.com" onApprove={() => {}} />);
    expect(screen.queryByTitle("Site preview")).not.toBeInTheDocument();
    expect(screen.getByText("Unable to preview this URL")).toBeInTheDocument();
  });
});
