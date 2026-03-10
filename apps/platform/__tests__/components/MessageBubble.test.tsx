import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { MessageBubble } from "../../components/chat/MessageBubble";

describe("MessageBubble", () => {
  it("renders user message with right alignment", () => {
    render(<MessageBubble role="user" content="Hello!" />);
    expect(screen.getByText("Hello!")).toBeInTheDocument();
  });

  it("renders assistant message with left alignment", () => {
    render(<MessageBubble role="assistant" content="Hi there!" />);
    expect(screen.getByText("Hi there!")).toBeInTheDocument();
  });
});
