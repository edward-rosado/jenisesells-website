import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi } from "vitest";
import { SitePreview } from "../../components/chat/SitePreview";

describe("SitePreview", () => {
  it("renders an iframe with the site URL", () => {
    render(<SitePreview siteUrl="https://example.realestatestar.com" onApprove={() => {}} />);
    const iframe = screen.getByTitle("Site preview");
    expect(iframe).toBeInTheDocument();
    expect(iframe).toHaveAttribute("src", "https://example.realestatestar.com");
  });

  it("sandboxes iframe with allow-scripts but not allow-same-origin", () => {
    render(<SitePreview siteUrl="https://example.com" onApprove={() => {}} />);
    const iframe = screen.getByTitle("Site preview");
    expect(iframe).toHaveAttribute("sandbox", "allow-scripts");
  });

  it("calls onApprove when button clicked", async () => {
    const onApprove = vi.fn();
    render(<SitePreview siteUrl="https://example.com" onApprove={onApprove} />);
    await userEvent.click(screen.getByRole("button", { name: /approve/i }));
    expect(onApprove).toHaveBeenCalledOnce();
  });

  it("appends #cma-form anchor when showCmaHighlight is true", () => {
    render(
      <SitePreview
        siteUrl="https://example.com"
        onApprove={() => {}}
        showCmaHighlight
      />
    );
    const iframe = screen.getByTitle("CMA form preview");
    expect(iframe).toHaveAttribute("src", "https://example.com#cma-form");
  });

  it("shows 'Your CMA Form' title when showCmaHighlight is true", () => {
    render(
      <SitePreview
        siteUrl="https://example.com"
        onApprove={() => {}}
        showCmaHighlight
      />
    );
    expect(screen.getByText("Your CMA Form")).toBeInTheDocument();
  });

  it("shows CMA description text when showCmaHighlight is true", () => {
    render(
      <SitePreview
        siteUrl="https://example.com"
        onApprove={() => {}}
        showCmaHighlight
      />
    );
    expect(screen.getByText(/cma form on your website/i)).toBeInTheDocument();
  });

  it("hides Approve button when showCmaHighlight is true", () => {
    render(
      <SitePreview
        siteUrl="https://example.com"
        onApprove={() => {}}
        showCmaHighlight
      />
    );
    expect(screen.queryByRole("button", { name: /approve/i })).not.toBeInTheDocument();
  });

  it("shows Approve button when showCmaHighlight is not set", () => {
    render(
      <SitePreview
        siteUrl="https://example.com"
        onApprove={() => {}}
      />
    );
    expect(screen.getByRole("button", { name: /approve/i })).toBeInTheDocument();
  });
});
