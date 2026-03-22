/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render } from "@testing-library/react";
import { createElement } from "react";
import { safeHref, renderHeadline } from "@/features/sections/heroes/hero-utils";

describe("safeHref", () => {
  it("allows hash links", () => {
    expect(safeHref("#cma-form")).toBe("#cma-form");
  });

  it("allows relative paths", () => {
    expect(safeHref("/about")).toBe("/about");
  });

  it("allows https URLs", () => {
    expect(safeHref("https://example.com")).toBe("https://example.com");
  });

  it("blocks http URLs (downgrade protection)", () => {
    expect(safeHref("http://example.com")).toBe("#");
  });

  it("sanitizes javascript: links to #", () => {
    expect(safeHref("javascript:alert(1)")).toBe("#");
  });

  it("sanitizes data: URLs to #", () => {
    expect(safeHref("data:text/html,<h1>hack</h1>")).toBe("#");
  });

  it("sanitizes invalid URLs to #", () => {
    expect(safeHref("not a url at all")).toBe("#");
  });
});

describe("renderHeadline", () => {
  it("returns plain string when no highlightWord", () => {
    expect(renderHeadline("Hello World")).toBe("Hello World");
  });

  it("returns plain string when highlightWord not found", () => {
    expect(renderHeadline("Hello World", "Missing")).toBe("Hello World");
  });

  it("wraps the highlight word in a styled span", () => {
    const result = renderHeadline("Find Your Dream Home", "Dream");
    const { container } = render(createElement("h1", null, result));
    const span = container.querySelector("span");
    expect(span).toBeInTheDocument();
    expect(span!.textContent).toBe("Dream");
  });

  it("highlights the last occurrence of the word", () => {
    const result = renderHeadline("Dream big, live the Dream", "Dream");
    const { container } = render(createElement("h1", null, result));
    const spans = container.querySelectorAll("span");
    expect(spans).toHaveLength(1);
    expect(container.textContent).toBe("Dream big, live the Dream");
  });
});
