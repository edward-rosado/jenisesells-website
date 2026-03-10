import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ChatWindow } from "../../components/chat/ChatWindow";

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

  it("sends a message when clicking Send", async () => {
    mockFetchJsonOk({ response: "Hello back!" });
    const user = userEvent.setup();

    render(<ChatWindow sessionId="test-123" initialMessages={[]} />);

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

    render(<ChatWindow sessionId="test-123" initialMessages={[]} />);

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

    render(<ChatWindow sessionId="test-123" initialMessages={[]} />);

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

    render(<ChatWindow sessionId="test-123" initialMessages={[]} />);

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

    render(<ChatWindow sessionId="test-123" initialMessages={[]} />);

    await user.click(screen.getByRole("button", { name: /Send/i }));

    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it("clears input after sending", async () => {
    mockFetchJsonOk({ response: "OK" });
    const user = userEvent.setup();

    render(<ChatWindow sessionId="test-123" initialMessages={[]} />);

    const input = screen.getByPlaceholderText(/Type a message/i);
    await user.type(input, "Hello");
    await user.click(screen.getByRole("button", { name: /Send/i }));

    expect(input).toHaveValue("");
  });

  it("shows Thinking indicator while sending", async () => {
    let resolveResponse!: (value: Response) => void;
    fetchSpy.mockReturnValueOnce(
      new Promise<Response>((resolve) => {
        resolveResponse = resolve;
      })
    );
    const user = userEvent.setup();

    render(<ChatWindow sessionId="test-123" initialMessages={[]} />);

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
});
