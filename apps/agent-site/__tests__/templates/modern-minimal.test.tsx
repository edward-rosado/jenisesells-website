/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ModernMinimal } from "@/templates/modern-minimal";
import { AGENT, CONTENT, CONTENT_ALL_DISABLED } from "../components/fixtures";

vi.mock("next/script", () => ({
  __esModule: true,
  default: ({ id, src }: { id?: string; src: string }) => (
    <script data-testid={id} data-src={src} />
  ),
}));

describe("ModernMinimal template", () => {
  it("always renders the Nav", () => {
    render(<ModernMinimal agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("navigation", { name: "Main navigation" })).toBeInTheDocument();
  });

  it("always renders the Footer", () => {
    render(<ModernMinimal agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("contentinfo")).toBeInTheDocument();
  });

  it("renders Hero section when enabled", () => {
    render(<ModernMinimal agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  });

  it("does not render Hero when disabled", () => {
    render(<ModernMinimal agent={AGENT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
  });

  it("renders all sections when all enabled", () => {
    render(<ModernMinimal agent={AGENT} content={CONTENT} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
    expect(screen.getByText("150+")).toBeInTheDocument();
    expect(screen.getByText("Market Analysis")).toBeInTheDocument();
    expect(screen.getByText("Submit Info")).toBeInTheDocument();
    expect(screen.getByText("$750,000")).toBeInTheDocument();
    expect(screen.getByText("Amazing service!")).toBeInTheDocument();
    expect(screen.getByText(/About Jane Smith/)).toBeInTheDocument();
  });

  it("does not render disabled sections", () => {
    render(<ModernMinimal agent={AGENT} content={CONTENT_ALL_DISABLED} />);
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
    expect(screen.queryByText("Homes Sold")).not.toBeInTheDocument();
  });
});
