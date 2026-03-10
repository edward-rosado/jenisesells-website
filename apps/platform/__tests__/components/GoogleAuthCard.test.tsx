import { render, screen, fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { GoogleAuthCard } from "../../components/chat/GoogleAuthCard";

describe("GoogleAuthCard", () => {
  const mockOnConnected = vi.fn();
  const mockOnError = vi.fn();
  const oauthUrl = "https://accounts.google.com/o/oauth2/v2/auth?test=true";
  const apiOrigin = "http://localhost:5000";

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders connect button", () => {
    render(
      <GoogleAuthCard
        oauthUrl={oauthUrl}
        onConnected={mockOnConnected}
        apiOrigin={apiOrigin}
      />
    );
    expect(screen.getByRole("button", { name: /connect with google/i })).toBeInTheDocument();
  });

  it("renders heading text", () => {
    render(
      <GoogleAuthCard
        oauthUrl={oauthUrl}
        onConnected={mockOnConnected}
        apiOrigin={apiOrigin}
      />
    );
    expect(screen.getByText("Connect Google Account")).toBeInTheDocument();
  });

  it("renders scope description", () => {
    render(
      <GoogleAuthCard
        oauthUrl={oauthUrl}
        onConnected={mockOnConnected}
        apiOrigin={apiOrigin}
      />
    );
    expect(screen.getByText(/gmail, drive, docs, sheets/i)).toBeInTheDocument();
  });

  it("opens popup when button clicked", async () => {
    const openSpy = vi.spyOn(window, "open").mockReturnValue(null);
    render(
      <GoogleAuthCard
        oauthUrl={oauthUrl}
        onConnected={mockOnConnected}
        apiOrigin={apiOrigin}
      />
    );
    await userEvent.click(screen.getByRole("button", { name: /connect with google/i }));
    expect(openSpy).toHaveBeenCalledOnce();
    expect(openSpy.mock.calls[0][0]).toBe(oauthUrl);
    expect(openSpy.mock.calls[0][1]).toBe("google-oauth");
    openSpy.mockRestore();
  });

  it("calls onConnected when postMessage with success received from trusted origin", () => {
    render(
      <GoogleAuthCard
        oauthUrl={oauthUrl}
        onConnected={mockOnConnected}
        apiOrigin={apiOrigin}
      />
    );

    fireEvent(
      window,
      new MessageEvent("message", {
        origin: apiOrigin,
        data: {
          type: "google_oauth_callback",
          success: true,
          message: "Connected as Jane Doe (jane@gmail.com)",
        },
      })
    );

    expect(mockOnConnected).toHaveBeenCalledWith("Connected as Jane Doe (jane@gmail.com)");
  });

  it("calls onError when postMessage with failure received from trusted origin", () => {
    render(
      <GoogleAuthCard
        oauthUrl={oauthUrl}
        onConnected={mockOnConnected}
        onError={mockOnError}
        apiOrigin={apiOrigin}
      />
    );

    fireEvent(
      window,
      new MessageEvent("message", {
        origin: apiOrigin,
        data: {
          type: "google_oauth_callback",
          success: false,
          message: "Access denied",
        },
      })
    );

    expect(mockOnError).toHaveBeenCalledWith("Access denied");
    expect(mockOnConnected).not.toHaveBeenCalled();
  });

  it("ignores unrelated postMessage events", () => {
    render(
      <GoogleAuthCard
        oauthUrl={oauthUrl}
        onConnected={mockOnConnected}
        onError={mockOnError}
        apiOrigin={apiOrigin}
      />
    );

    fireEvent(
      window,
      new MessageEvent("message", {
        origin: apiOrigin,
        data: { type: "some_other_event" },
      })
    );

    expect(mockOnConnected).not.toHaveBeenCalled();
    expect(mockOnError).not.toHaveBeenCalled();
  });

  it("ignores postMessage from untrusted origin", () => {
    render(
      <GoogleAuthCard
        oauthUrl={oauthUrl}
        onConnected={mockOnConnected}
        onError={mockOnError}
        apiOrigin={apiOrigin}
      />
    );

    fireEvent(
      window,
      new MessageEvent("message", {
        origin: "https://evil.example.com",
        data: {
          type: "google_oauth_callback",
          success: true,
          message: "Hijacked!",
        },
      })
    );

    expect(mockOnConnected).not.toHaveBeenCalled();
    expect(mockOnError).not.toHaveBeenCalled();
  });
});
