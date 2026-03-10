import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { ChatWindow } from "../../components/chat/ChatWindow";

describe("ChatWindow", () => {
  it("renders input field", () => {
    render(<ChatWindow sessionId="test-123" initialMessages={[]} />);
    expect(screen.getByPlaceholderText(/Type a message/i)).toBeInTheDocument();
  });

  it("renders send button", () => {
    render(<ChatWindow sessionId="test-123" initialMessages={[]} />);
    expect(screen.getByRole("button", { name: /Send/i })).toBeInTheDocument();
  });

  it("renders initial messages", () => {
    render(
      <ChatWindow
        sessionId="test-123"
        initialMessages={[
          { role: "assistant", content: "Welcome!" },
        ]}
      />
    );
    expect(screen.getByText("Welcome!")).toBeInTheDocument();
  });
});
