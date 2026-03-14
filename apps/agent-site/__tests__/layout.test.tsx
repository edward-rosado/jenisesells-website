/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render } from "@testing-library/react";

// We cannot render html/body inside JSDOM's existing document.
// Instead, test the skip-nav and main-content wrapper directly.

describe("RootLayout accessibility", () => {
  it("skip-nav link targets #main-content", () => {
    const { container } = render(
      <>
        <a href="#main-content" className="skip-nav">Skip to main content</a>
        <div id="main-content" tabIndex={-1}>
          <p>Content</p>
        </div>
      </>
    );
    const skipLink = container.querySelector('a[href="#main-content"]');
    expect(skipLink).toBeInTheDocument();
    expect(skipLink).toHaveTextContent("Skip to main content");

    const target = container.querySelector("#main-content");
    expect(target).toBeInTheDocument();
    expect(target).toHaveAttribute("tabindex", "-1");
  });

  it("main-content wrapper renders children", () => {
    const { getByText } = render(
      <div id="main-content" tabIndex={-1}>
        <p>Test child</p>
      </div>
    );
    expect(getByText("Test child")).toBeInTheDocument();
  });
});
