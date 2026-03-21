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

  it("closes list before heading when heading follows list items", () => {
    const md = "- Item 1\n- Item 2\n## Section";
    render(<MarkdownContent content={md} />);
    expect(screen.getByText("Item 1")).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Section");
    // List should be properly closed before heading
    const list = document.querySelector("ul");
    expect(list).not.toBeNull();
    expect(list!.querySelectorAll("li")).toHaveLength(2);
  });

  it("renders italic text", () => {
    render(<MarkdownContent content="This is *italic* text" />);
    const em = document.querySelector("em");
    expect(em).toHaveTextContent("italic");
  });

  it("closes list at end of input", () => {
    render(<MarkdownContent content={"- Last item"} />);
    const list = document.querySelector("ul");
    expect(list).not.toBeNull();
    expect(list!.querySelectorAll("li")).toHaveLength(1);
  });

  it("closes list when followed by a non-list line", () => {
    const md = "- Item\nParagraph after list";
    render(<MarkdownContent content={md} />);
    expect(screen.getByText("Item")).toBeInTheDocument();
    expect(screen.getByText("Paragraph after list")).toBeInTheDocument();
  });
});
