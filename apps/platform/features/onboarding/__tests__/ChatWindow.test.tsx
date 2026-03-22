import { render, screen, waitFor, act } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ChatWindow } from "../ChatWindow";

describe("ChatWindow", () => {
  let fetchSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    fetchSpy = vi.spyOn(globalThis, "fetch");
  });

  afterEach(() => {
    fetchSpy.mockRestore();
  });

  function mockFetchJsonOk(response: Record<string, unknown>) {
    fetchSpy.mockResolvedValueOnce(
      new Response(JSON.stringify(response), {
        status: 200,
        headers: { "content-type": "application/json" },
      })
    );
  }

  function mockFetchError() {
    fetchSpy.mockResolvedValueOnce(
      new Response(null, { status: 500 })
    );
  }

  /** Build a ReadableStream that emits SSE-formatted lines. */
  function makeSSEStream(events: string[]): ReadableStream<Uint8Array> {
    const encoder = new TextEncoder();
    const joined = events.join("\n") + "\n";
    return new ReadableStream({
      start(controller) {
        controller.enqueue(encoder.encode(joined));
        controller.close();
      },
    });
  }

  function mockFetchSSE(events: string[]) {
    const stream = makeSSEStream(events);
    fetchSpy.mockResolvedValueOnce(
      new Response(stream, {
        status: 200,
        headers: { "content-type": "text/event-stream" },
      })
    );
  }

  it("renders input field", () => {
    render(<ChatWindow sessionId="test-123" token="test-token" initialMessages={[]} />);
    expect(screen.getByPlaceholderText(/Type a message/i)).toBeInTheDocument();
  });

  it("renders send button", () => {
    render(<ChatWindow sessionId="test-123" token="test-token" initialMessages={[]} />);
    expect(screen.getByRole("button", { name: /Send/i })).toBeInTheDocument();
  });

  it("renders initial messages", () => {
    render(
      <ChatWindow
        sessionId="test-123"
        token="test-token"
        initialMessages={[
          { role: "assistant", content: "Welcome!" },
        ]}
      />
    );
    expect(screen.getByText("Welcome!")).toBeInTheDocument();
  });

  it("sends a message when clicking Send", async () => {
    mockFetchJsonOk({ response: "Hello back!" });
    const user = userEvent.setup();

    render(<ChatWindow sessionId="test-123" token="test-token" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "Hello");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    expect(fetchSpy).toHaveBeenCalledOnce();
    expect(fetchSpy).toHaveBeenCalledWith(
      expect.stringContaining("/onboard/test-123/chat"),
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ message: "Hello" }),
      })
    );

    await waitFor(() => {
      expect(screen.getByText("Hello back!")).toBeInTheDocument();
    });
  });

  it("sends a message when pressing Enter", async () => {
    mockFetchJsonOk({ response: "Got it!" });
    const user = userEvent.setup();

    render(<ChatWindow sessionId="test-123" token="test-token" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "Test message{enter}");

    expect(fetchSpy).toHaveBeenCalledOnce();
    expect(fetchSpy).toHaveBeenCalledWith(
      expect.stringContaining("/onboard/test-123/chat"),
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ message: "Test message" }),
      })
    );

    await waitFor(() => {
      expect(screen.getByText("Got it!")).toBeInTheDocument();
    });
  });

  it("disables Send button while sending", async () => {
    let resolveResponse!: (value: Response) => void;
    fetchSpy.mockReturnValueOnce(
      new Promise<Response>((resolve) => {
        resolveResponse = resolve;
      })
    );
    const user = userEvent.setup();

    render(<ChatWindow sessionId="test-123" token="test-token" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "Hello");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    // Button should be disabled while waiting for response
    expect(screen.getByRole("button", { name: /Send/i })).toBeDisabled();

    // Resolve the fetch to clean up
    resolveResponse(
      new Response(JSON.stringify({ response: "Done" }), {
        status: 200,
        headers: { "content-type": "application/json" },
      })
    );

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Send/i })).not.toBeDisabled();
    });
  });

  it("shows error message when fetch fails", async () => {
    mockFetchError();
    const user = userEvent.setup();

    render(<ChatWindow sessionId="test-123" token="test-token" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "Hello");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(
        screen.getByText("Something went wrong. Please try again.")
      ).toBeInTheDocument();
    });
  });

  it("does not send empty messages", async () => {
    const user = userEvent.setup();

    render(<ChatWindow sessionId="test-123" token="test-token" initialMessages={[]} />);

    await user.click(screen.getByRole("button", { name: /Send/i }));

    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it("clears input after sending", async () => {
    mockFetchJsonOk({ response: "OK" });
    const user = userEvent.setup();

    render(<ChatWindow sessionId="test-123" token="test-token" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "Hello");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    expect(input).toHaveValue("");
  });

  it("renders GeometricStar avatar next to assistant messages", () => {
    render(
      <ChatWindow
        sessionId="test-123"
        token="test-token"
        initialMessages={[
          { role: "assistant", content: "Hello!" },
        ]}
      />
    );
    const avatars = screen.getAllByRole("img", { hidden: true });
    expect(avatars.length).toBeGreaterThanOrEqual(1);
    // The avatar SVG should have the star logo aria-label
    expect(avatars.some((el) => el.getAttribute("aria-label")?.includes("logo"))).toBe(true);
  });

  it("does not render avatar next to user messages", () => {
    render(
      <ChatWindow
        sessionId="test-123"
        token="test-token"
        initialMessages={[
          { role: "user", content: "Hi there" },
        ]}
      />
    );
    const avatars = screen.queryAllByRole("img", { hidden: true });
    // No star avatar for user messages
    expect(avatars.every((el) => !el.getAttribute("aria-label")?.includes("logo"))).toBe(true);
  });

  it("shows Thinking indicator while sending", async () => {
    let resolveResponse!: (value: Response) => void;
    fetchSpy.mockReturnValueOnce(
      new Promise<Response>((resolve) => {
        resolveResponse = resolve;
      })
    );
    const user = userEvent.setup();

    render(<ChatWindow sessionId="test-123" token="test-token" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "Hello");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    expect(screen.getByText("Thinking...")).toBeInTheDocument();

    resolveResponse(
      new Response(JSON.stringify({ response: "Done" }), {
        status: 200,
        headers: { "content-type": "application/json" },
      })
    );

    await waitFor(() => {
      expect(screen.queryByText("Thinking...")).not.toBeInTheDocument();
    });
  });

  // ---- SSE streaming tests ----

  it("handles SSE text/event-stream with JSON-encoded tokens", async () => {
    mockFetchSSE([
      'data: "Hello "',
      "",  // empty line separator — typical in SSE, hits the !startsWith("data: ") continue
      'data: "world"',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "Hi");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByText("Hello world")).toBeInTheDocument();
    });
  });

  it("handles SSE with non-JSON data tokens (fallback decode)", async () => {
    mockFetchSSE([
      "data: plain text chunk",
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "Hi");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByText("plain text chunk")).toBeInTheDocument();
    });
  });

  it("handles SSE with event: card type and card markers", async () => {
    mockFetchSSE([
      'data: "Looking good!"',
      "event: card",
      'data: [CARD:payment_card]{"checkoutUrl":"https://stripe.com/pay","price":"$900"}',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "pay");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      // The card should be rendered and text before it
      expect(screen.getByText("Looking good!")).toBeInTheDocument();
      expect(screen.getByText("$900")).toBeInTheDocument();
    });
  });

  it("handles SSE with event: card containing no valid JSON start", async () => {
    // Card marker without JSON (no opening brace after marker)
    mockFetchSSE([
      'data: "text"',
      "event: card",
      "data: [CARD:unknown_type]no-json-here",
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByText("text")).toBeInTheDocument();
    });
  });

  it("handles SSE with event: card containing malformed JSON", async () => {
    mockFetchSSE([
      'data: "Hey"',
      "event: card",
      'data: [CARD:payment_card]{bad json',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByText("Hey")).toBeInTheDocument();
    });
  });

  it("handles SSE with event: card without CARD marker match", async () => {
    mockFetchSSE([
      'data: "before"',
      "event: card",
      "data: not-a-card-marker",
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByText("before")).toBeInTheDocument();
    });
  });

  it("handles SSE with inline [CARD:...] markers in content text", async () => {
    mockFetchSSE([
      'data: "Before card [CARD:profile_card]{\\"name\\":\\"Jane\\"} After card"',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      // The text should be split: "Before card" as text, card rendered, "After card" as text
      expect(screen.getByText("Before card")).toBeInTheDocument();
      expect(screen.getByText("Jane")).toBeInTheDocument();
      expect(screen.getByText("After card")).toBeInTheDocument();
    });
  });

  it("handles SSE with inline [CARD:...] marker where JSON parse fails", async () => {
    mockFetchSSE([
      'data: "[CARD:profile_card]{invalid json}"',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      // Falls back to showing the raw marker as text
      expect(screen.getByText("[CARD:profile_card]")).toBeInTheDocument();
    });
  });

  it("handles SSE stream with no body reader", async () => {
    // Response with body = null (reader unavailable)
    fetchSpy.mockResolvedValueOnce({
      ok: true,
      headers: new Headers({ "content-type": "text/event-stream" }),
      body: null,
    } as unknown as Response);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "Hi");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    // Should not crash — sending state should reset
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Send/i })).not.toBeDisabled();
    });
  });

  it("handles SSE event: line that resets after non-card data", async () => {
    // event: type followed by regular data (not card) — tests the currentEventType = "" reset
    mockFetchSSE([
      "event: someother",
      'data: "test content"',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "Hi");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByText("test content")).toBeInTheDocument();
    });
  });

  it("strips [Tool: ...] lines from assistant content", async () => {
    mockFetchSSE([
      'data: "Working on it\\n[Tool: scrape_url] running\\nDone!"',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByText(/Working on it/)).toBeInTheDocument();
      expect(screen.getByText(/Done!/)).toBeInTheDocument();
    });
  });

  // ---- Auto-send on mount ----

  it("auto-sends autoMessage on mount as silent (no user bubble)", async () => {
    mockFetchJsonOk({ response: "Scraped your profile!" });

    render(
      <ChatWindow
        sessionId="s1"
        token="t1"
        initialMessages={[]}
        autoMessage="https://zillow.com/profile/test"
      />
    );

    await waitFor(() => {
      expect(fetchSpy).toHaveBeenCalledWith(
        expect.stringContaining("/onboard/s1/chat"),
        expect.objectContaining({
          body: JSON.stringify({ message: "https://zillow.com/profile/test" }),
        })
      );
    });

    // No user bubble should appear for the autoMessage
    await waitFor(() => {
      expect(screen.getByText("Scraped your profile!")).toBeInTheDocument();
    });
    expect(screen.queryByText("https://zillow.com/profile/test")).not.toBeInTheDocument();
  });

  it("does not auto-send when autoMessage is not provided", async () => {
    render(
      <ChatWindow sessionId="s1" token="t1" initialMessages={[]} />
    );

    // Give it a tick to see if anything fires
    await act(async () => {
      await new Promise((r) => setTimeout(r, 50));
    });

    expect(fetchSpy).not.toHaveBeenCalled();
  });

  // ---- handleAction ----

  it("handleAction sends [Action: ...] text silently via onAction callback", async () => {
    // First mock for the action message
    mockFetchJsonOk({ response: "Profile confirmed!" });

    render(
      <ChatWindow
        sessionId="s1"
        token="t1"
        initialMessages={[
          {
            role: "assistant",
            content: "",
            type: "profile_card",
            metadata: { name: "Jane" },
          },
        ]}
      />
    );

    // Click the "Looks right" button on the profile card to trigger onAction("confirm_profile")
    await userEvent.click(screen.getByRole("button", { name: /looks right/i }));

    await waitFor(() => {
      expect(fetchSpy).toHaveBeenCalledWith(
        expect.stringContaining("/onboard/s1/chat"),
        expect.objectContaining({
          body: JSON.stringify({ message: "[Action: confirm_profile]" }),
        })
      );
    });

    // The action text should NOT appear as a user bubble
    expect(screen.queryByText(/\[Action:/)).not.toBeInTheDocument();
  });

  it("handleAction with data sends [Action: ...] with JSON payload silently", async () => {
    mockFetchJsonOk({ response: "Colors saved!" });

    render(
      <ChatWindow
        sessionId="s1"
        token="t1"
        initialMessages={[
          {
            role: "assistant",
            content: "",
            type: "color_palette",
            metadata: { primaryColor: "#ff0000", accentColor: "#00ff00" },
          },
        ]}
      />
    );

    // Click "Confirm colors" to trigger handleAction with data
    await userEvent.click(screen.getByRole("button", { name: /confirm colors/i }));

    await waitFor(() => {
      expect(fetchSpy).toHaveBeenCalledWith(
        expect.stringContaining("/onboard/s1/chat"),
        expect.objectContaining({
          body: expect.stringContaining("[Action: confirm_colors]"),
        })
      );
    });
  });

  it("does not send a second message while first is still sending", async () => {
    let resolveResponse!: (value: Response) => void;
    fetchSpy.mockReturnValueOnce(
      new Promise<Response>((resolve) => {
        resolveResponse = resolve;
      })
    );
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "First");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    // Try to send again while still pending
    await user.type(input, "Second");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    // Only one fetch call should have been made
    expect(fetchSpy).toHaveBeenCalledTimes(1);

    resolveResponse(
      new Response(JSON.stringify({ response: "Done" }), {
        status: 200,
        headers: { "content-type": "application/json" },
      })
    );

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Send/i })).not.toBeDisabled();
    });
  });

  it("includes Authorization header with Bearer token", async () => {
    mockFetchJsonOk({ response: "ok" });
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="my-token" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "Hello");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    expect(fetchSpy).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: "Bearer my-token",
        }),
      })
    );
  });

  it("sets aria-busy on the message log while sending", async () => {
    let resolveResponse!: (value: Response) => void;
    fetchSpy.mockReturnValueOnce(
      new Promise<Response>((resolve) => {
        resolveResponse = resolve;
      })
    );
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const log = screen.getByRole("log");
    expect(log).toHaveAttribute("aria-busy", "false");

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "Hello");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    expect(log).toHaveAttribute("aria-busy", "true");

    resolveResponse(
      new Response(JSON.stringify({ response: "Done" }), {
        status: 200,
        headers: { "content-type": "application/json" },
      })
    );

    await waitFor(() => {
      expect(log).toHaveAttribute("aria-busy", "false");
    });
  });

  it("SSE stream with inline card marker where extractJson returns null", async () => {
    // [CARD:payment_card] followed by non-{ character — extractJson returns null
    mockFetchSSE([
      'data: "[CARD:payment_card]not-json rest of text"',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      // When extractJson returns null, the lastIndex advances past marker only
      // "not-json rest of text" should appear as remaining text
      expect(screen.getByText("not-json rest of text")).toBeInTheDocument();
    });
  });

  it("handles empty SSE stream gracefully", async () => {
    mockFetchSSE(["data: [DONE]"]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    // Should complete without crashing, button re-enabled
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Send/i })).not.toBeDisabled();
    });
  });

  it("handles SSE content that is only [Tool: ...] lines (no visible text)", async () => {
    mockFetchSSE([
      'data: "[Tool: scrape_url] fetching"',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Send/i })).not.toBeDisabled();
    });
  });

  it("handles SSE content with Tool lines and actual text (no cards found path)", async () => {
    mockFetchSSE([
      'data: "[Tool: scrape_url] fetching\\nHere is your data"',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByText("Here is your data")).toBeInTheDocument();
    });
  });

  it("handles content with CARD marker only (no JSON, no trailing text) — hits fallback path", async () => {
    mockFetchSSE([
      'data: "[CARD:unknown]"',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByText("[CARD:unknown]")).toBeInTheDocument();
    });
  });

  it("handles SSE with nested JSON in card markers (exercises extractJson depth tracking)", async () => {
    mockFetchSSE([
      'data: "[CARD:payment_card]{\\"checkoutUrl\\":\\"https://stripe.com\\",\\"nested\\":{\\"key\\":\\"val\\"}}"',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByText("$900")).toBeInTheDocument();
    });
  });

  it("handles SSE with string containing braces in JSON extraction", async () => {
    mockFetchSSE([
      'data: "[CARD:payment_card]{\\"price\\":\\"$900 {tax included}\\"}"',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByText(/\$900/)).toBeInTheDocument();
    });
  });

  it("handles SSE with backslash escape sequences in JSON parse", async () => {
    mockFetchSSE([
      'data: "escape \\\\ and \\" chars"',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByText(/escape/)).toBeInTheDocument();
    });
  });

  it("handles card JSON with backslash-escaped quotes in extractJson", async () => {
    mockFetchSSE([
      'data: "[CARD:payment_card]{\\"note\\":\\"it\\\\\\"s great\\"}"',
      "data: [DONE]",
    ]);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Send/i })).not.toBeDisabled();
    });
  });

  it("handles response with null content-type header (fallback to empty string)", async () => {
    fetchSpy.mockResolvedValueOnce({
      ok: true,
      headers: { get: (name: string) => name === "content-type" ? null : null } as unknown as Headers,
      json: () => Promise.resolve({ response: "No content type" }),
    } as unknown as Response);
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByText("No content type")).toBeInTheDocument();
    });
  });

  it("renders initial messages without explicit msgId (uses index fallback)", () => {
    render(
      <ChatWindow
        sessionId="s1"
        token="t1"
        initialMessages={[
          { role: "assistant", content: "First" },
          { role: "user", content: "Second" },
        ]}
      />
    );
    expect(screen.getByText("First")).toBeInTheDocument();
    expect(screen.getByText("Second")).toBeInTheDocument();
  });

  it("handles multi-chunk SSE stream delivered in separate enqueue calls", async () => {
    const encoder = new TextEncoder();
    const stream = new ReadableStream<Uint8Array>({
      start(controller) {
        // Deliver partial lines across chunks to test buffer reassembly
        controller.enqueue(encoder.encode('data: "Hel'));
        controller.enqueue(encoder.encode('lo"\ndata: [DONE]\n'));
        controller.close();
      },
    });
    fetchSpy.mockResolvedValueOnce(
      new Response(stream, {
        status: 200,
        headers: { "content-type": "text/event-stream" },
      })
    );
    const user = userEvent.setup();

    render(<ChatWindow sessionId="s1" token="t1" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "test");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    await waitFor(() => {
      expect(screen.getByText("Hello")).toBeInTheDocument();
    });
  });
});
