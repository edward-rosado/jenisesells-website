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

  it("calls onApprove when button clicked", async () => {
    const onApprove = vi.fn();
    render(<SitePreview siteUrl="https://example.com" onApprove={onApprove} />);
    await userEvent.click(screen.getByRole("button", { name: /approve/i }));
    expect(onApprove).toHaveBeenCalledOnce();
  });
});
