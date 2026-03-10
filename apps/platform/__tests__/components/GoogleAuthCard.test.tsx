import { render, screen, fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { GoogleAuthCard } from "../../components/chat/GoogleAuthCard";

describe("GoogleAuthCard", () => {
  const mockOnConnected = vi.fn();
  const mockOnError = vi.fn();
  const oauthUrl = "https://accounts.google.com/o/oauth2/v2/auth?test=true";

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders connect button", () => {
    render(
      <GoogleAuthCard
        oauthUrl={oauthUrl}
        onConnected={mockOnConnected}
      />
    );
    expect(screen.getByRole("button", { name: /connect with google/i })).toBeInTheDocument();
  });

  it("renders heading text", () => {
    render(
      <GoogleAuthCard
        oauthUrl={oauthUrl}
        onConnected={mockOnConnected}
      />
    );
    expect(screen.getByText("Connect Google Account")).toBeInTheDocument();
  });

  it("renders scope description", () => {
    render(
      <GoogleAuthCard
        oauthUrl={oauthUrl}
        onConnected={mockOnConnected}
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
      />
    );
    await userEvent.click(screen.getByRole("button", { name: /connect with google/i }));
    expect(openSpy).toHaveBeenCalledOnce();
    expect(openSpy.mock.calls[0][0]).toBe(oauthUrl);
    expect(openSpy.mock.calls[0][1]).toBe("google-oauth");
    openSpy.mockRestore();
  });

  it("calls onConnected when postMessage with success received", () => {
    render(
      <GoogleAuthCard
        oauthUrl={oauthUrl}
        onConnected={mockOnConnected}
      />
    );

    fireEvent(
      window,
      new MessageEvent("message", {
        data: {
          type: "google_oauth_callback",
          success: true,
          message: "Connected as Jane Doe (jane@gmail.com)",
        },
      })
    );

    expect(mockOnConnected).toHaveBeenCalledWith("Connected as Jane Doe (jane@gmail.com)");
  });

  it("calls onError when postMessage with failure received", () => {
    render(
      <GoogleAuthCard
        oauthUrl={oauthUrl}
        onConnected={mockOnConnected}
        onError={mockOnError}
      />
    );

    fireEvent(
      window,
      new MessageEvent("message", {
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
      />
    );

    fireEvent(
      window,
      new MessageEvent("message", {
        data: { type: "some_other_event" },
      })
    );

    expect(mockOnConnected).not.toHaveBeenCalled();
    expect(mockOnError).not.toHaveBeenCalled();
  });
});
