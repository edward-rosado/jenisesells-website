/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MarkdownContent } from "@/components/legal/MarkdownContent";

describe("MarkdownContent", () => {
  it("renders markdown headings", () => {
    render(<MarkdownContent content="## Hello World" />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Hello World");
  });

  it("renders markdown paragraphs", () => {
    render(<MarkdownContent content="This is a paragraph." />);
    expect(screen.getByText("This is a paragraph.")).toBeInTheDocument();
  });

  it("renders markdown links", () => {
    render(<MarkdownContent content="[Click here](https://example.com)" />);
    const link = screen.getByRole("link", { name: "Click here" });
    expect(link).toHaveAttribute("href", "https://example.com");
  });

  it("renders markdown lists", () => {
    render(<MarkdownContent content={"- Item A\n- Item B"} />);
    expect(screen.getByText("Item A")).toBeInTheDocument();
    expect(screen.getByText("Item B")).toBeInTheDocument();
  });

  it("renders bold text", () => {
    render(<MarkdownContent content="This is **bold** text" />);
    const bold = document.querySelector("strong");
    expect(bold).toHaveTextContent("bold");
  });

  it("renders nothing when content is empty string", () => {
    const { container } = render(<MarkdownContent content="" />);
    expect(container.innerHTML).toBe("");
  });

  it("renders nothing when content is whitespace only", () => {
    const { container } = render(<MarkdownContent content="   " />);
    expect(container.innerHTML).toBe("");
  });

  it("applies prose styling class when content present", () => {
    const { container } = render(<MarkdownContent content="Hello" />);
    expect(container.firstChild).toHaveClass("prose");
  });
});
