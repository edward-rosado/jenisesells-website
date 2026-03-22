import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { MessageBubble } from "../MessageBubble";

describe("MessageBubble", () => {
  it("renders user message with right alignment", () => {
    render(<MessageBubble role="user" content="Hello!" />);
    expect(screen.getByText("Hello!")).toBeInTheDocument();
  });

  it("renders assistant message with left alignment", () => {
    render(<MessageBubble role="assistant" content="Hi there!" />);
    expect(screen.getByText("Hi there!")).toBeInTheDocument();
  });

  it("renders GeometricStar avatar for assistant messages", () => {
    render(<MessageBubble role="assistant" content="Hello" />);
    expect(screen.getByRole("img", { hidden: true })).toBeInTheDocument();
  });

  it("does not render GeometricStar avatar for user messages", () => {
    render(<MessageBubble role="user" content="Hello" />);
    expect(screen.queryByRole("img", { hidden: true })).not.toBeInTheDocument();
  });

  it("passes thinking state to GeometricStar when streaming", () => {
    const { container } = render(
      <MessageBubble role="assistant" content="..." isStreaming={true} />
    );
    const svg = container.querySelector("svg");
    expect(svg?.style.animation).toContain("star-spin");
  });

  it("passes idle state to GeometricStar when not streaming", () => {
    const { container } = render(
      <MessageBubble role="assistant" content="Done" isStreaming={false} />
    );
    const svg = container.querySelector("svg");
    expect(svg?.style.animation).toContain("star-pulse");
  });
});
