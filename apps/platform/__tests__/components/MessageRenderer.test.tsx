import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { MessageRenderer } from "../../components/chat/MessageRenderer";

describe("MessageRenderer", () => {
  it("renders text messages as MessageBubble", () => {
    render(
      <MessageRenderer
        message={{ role: "assistant", content: "Hello!" }}
      />
    );
    expect(screen.getByText("Hello!")).toBeInTheDocument();
  });

  it("renders profile_card type as ProfileCard", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "profile_card",
          metadata: { name: "Jane Doe", brokerage: "RE/MAX" },
        }}
      />
    );
    expect(screen.getByText("Jane Doe")).toBeInTheDocument();
    expect(screen.getByText("RE/MAX")).toBeInTheDocument();
  });

  it("renders feature_checklist type", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "feature_checklist",
        }}
      />
    );
    expect(
      screen.getByText(/everything included with real estate star/i)
    ).toBeInTheDocument();
  });

  it("renders payment_card type", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "payment_card",
        }}
      />
    );
    expect(screen.getByText("$900")).toBeInTheDocument();
  });
});
