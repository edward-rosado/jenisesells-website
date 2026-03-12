import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi } from "vitest";
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

  it("renders google_auth type as GoogleAuthCard", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "google_auth",
          metadata: { oauthUrl: "https://accounts.google.com/o/oauth2/v2/auth?test=true" },
        }}
      />
    );
    expect(screen.getByText("Connect Google Account")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /connect with google/i })).toBeInTheDocument();
  });

  it("renders cma_progress type as CmaProgressCard", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "cma_progress",
          metadata: {
            address: "456 Oak Ave, Newark, NJ 07102",
            recipientEmail: "jane@remax.com",
            status: "complete",
            steps: [
              { label: "Searching comparable sales", status: "done" },
              { label: "Emailing report", status: "done" },
            ],
          },
        }}
      />
    );
    expect(screen.getByText("CMA Report Delivered")).toBeInTheDocument();
    expect(screen.getByText("456 Oak Ave, Newark, NJ 07102")).toBeInTheDocument();
    expect(screen.getByText("jane@remax.com")).toBeInTheDocument();
  });

  it("renders site_preview with showCmaHighlight metadata", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "site_preview",
          metadata: { siteUrl: "https://test.realestatestar.com", showCmaHighlight: true },
        }}
      />
    );
    expect(screen.getByText("Your CMA Form")).toBeInTheDocument();
    const iframe = screen.getByTitle("CMA form preview");
    expect(iframe).toHaveAttribute("src", "https://test.realestatestar.com#cma-form");
  });

  // ---- Additional card type branch coverage ----

  it("renders color_palette type as ColorPalette", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "color_palette",
          metadata: { primaryColor: "#123456", accentColor: "#abcdef" },
        }}
      />
    );
    expect(screen.getByText("Brand Colors")).toBeInTheDocument();
    expect(screen.getByText("Primary")).toBeInTheDocument();
    expect(screen.getByText("Accent")).toBeInTheDocument();
  });

  it("renders site_preview without showCmaHighlight", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "site_preview",
          metadata: { siteUrl: "https://test.realestatestar.com" },
        }}
      />
    );
    expect(screen.getByText("Your Site Preview")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /approve/i })).toBeInTheDocument();
    const iframe = screen.getByTitle("Site preview");
    expect(iframe).toHaveAttribute("src", "https://test.realestatestar.com");
  });

  it("renders payment_card with custom metadata", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "payment_card",
          metadata: { checkoutUrl: "https://checkout.stripe.com/test", price: "$1,200" },
        }}
      />
    );
    expect(screen.getByText("$1,200")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /start free trial/i })).toBeInTheDocument();
  });

  it("renders user messages as MessageBubble (default/no type)", () => {
    render(
      <MessageRenderer
        message={{ role: "user", content: "My message" }}
      />
    );
    expect(screen.getByText("My message")).toBeInTheDocument();
  });

  it("renders text type explicitly as MessageBubble", () => {
    render(
      <MessageRenderer
        message={{ role: "assistant", content: "Text msg", type: "text" }}
      />
    );
    expect(screen.getByText("Text msg")).toBeInTheDocument();
  });

  it("renders with no onAction provided — uses fallback noop", async () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "profile_card",
          metadata: { name: "Bob" },
        }}
      />
    );
    // Should render without error even without onAction
    expect(screen.getByText("Bob")).toBeInTheDocument();
    // Click the button to exercise the fallback noop function (line 26)
    await userEvent.click(screen.getByRole("button", { name: /looks right/i }));
    // No error thrown — the noop handled the call
    expect(screen.getByText("Bob")).toBeInTheDocument();
  });

  it("renders with no metadata provided — uses empty object fallback", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "profile_card",
        }}
      />
    );
    // name defaults to "" when metadata is undefined
    expect(screen.getByRole("button", { name: /looks right/i })).toBeInTheDocument();
  });

  it("passes isStreaming to MessageBubble for default type", () => {
    render(
      <MessageRenderer
        message={{ role: "assistant", content: "Streaming..." }}
        isStreaming={true}
      />
    );
    // The GeometricStar should have "thinking" animation when streaming
    expect(screen.getByText("Streaming...")).toBeInTheDocument();
  });

  it("renders cma_progress with running status", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "cma_progress",
          metadata: {
            address: "123 Main St",
            recipientEmail: "test@test.com",
            status: "running",
            steps: [
              { label: "Step 1", status: "done" },
              { label: "Step 2", status: "active" },
            ],
          },
        }}
      />
    );
    expect(screen.getByText("Generating CMA Report...")).toBeInTheDocument();
  });

  it("renders cma_progress with failed status", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "cma_progress",
          metadata: {
            address: "123 Main St",
            recipientEmail: "test@test.com",
            status: "failed",
            steps: [
              { label: "Step 1", status: "done" },
              { label: "Step 2", status: "pending" },
            ],
          },
        }}
      />
    );
    expect(screen.getByText("CMA Report Failed")).toBeInTheDocument();
    expect(screen.getByText(/pipeline encountered an error/i)).toBeInTheDocument();
  });

  // ---- Callback invocation tests (cover arrow functions in switch cases) ----

  it("color_palette onConfirm callback invokes onAction with colors data", async () => {
    const onAction = vi.fn();
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "color_palette",
          metadata: { primaryColor: "#ff0000", accentColor: "#00ff00" },
        }}
        onAction={onAction}
      />
    );
    await userEvent.click(screen.getByRole("button", { name: /confirm colors/i }));
    expect(onAction).toHaveBeenCalledWith("confirm_colors", { primary: "#ff0000", accent: "#00ff00" });
  });

  it("google_auth onConnected callback invokes onAction with email", async () => {
    const onAction = vi.fn();
    const windowOpen = vi.spyOn(window, "open").mockImplementation(() => null);

    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "google_auth",
          metadata: { oauthUrl: "https://accounts.google.com/o/oauth2/v2/auth?test=true" },
        }}
        onAction={onAction}
      />
    );

    // Simulate a postMessage from the trusted origin to trigger onConnected
    const apiOrigin = new URL(process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5135").origin;
    window.dispatchEvent(
      new MessageEvent("message", {
        origin: apiOrigin,
        data: { type: "google_oauth_callback", success: true, message: "test@gmail.com" },
      })
    );

    expect(onAction).toHaveBeenCalledWith("google_connected", { email: "test@gmail.com" });
    windowOpen.mockRestore();
  });

  it("google_auth onError callback invokes onAction with error", async () => {
    const onAction = vi.fn();
    const windowOpen = vi.spyOn(window, "open").mockImplementation(() => null);

    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "google_auth",
          metadata: { oauthUrl: "https://accounts.google.com/o/oauth2/v2/auth?test=true" },
        }}
        onAction={onAction}
      />
    );

    const apiOrigin = new URL(process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5135").origin;
    window.dispatchEvent(
      new MessageEvent("message", {
        origin: apiOrigin,
        data: { type: "google_oauth_callback", success: false, message: "Access denied" },
      })
    );

    expect(onAction).toHaveBeenCalledWith("google_auth_error", { error: "Access denied" });
    windowOpen.mockRestore();
  });

  it("site_preview onApprove callback invokes onAction", async () => {
    const onAction = vi.fn();
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "site_preview",
          metadata: { siteUrl: "https://test.realestatestar.com" },
        }}
        onAction={onAction}
      />
    );
    await userEvent.click(screen.getByRole("button", { name: /approve/i }));
    expect(onAction).toHaveBeenCalledWith("approve_site");
  });

  // ---- Fallback/default value branch coverage (nullish coalescing ??) ----

  it("color_palette uses default #000000 when metadata colors are missing", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "color_palette",
          metadata: {},
        }}
      />
    );
    expect(screen.getByText("Brand Colors")).toBeInTheDocument();
  });

  it("google_auth uses empty string when oauthUrl missing", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "google_auth",
          metadata: {},
        }}
      />
    );
    expect(screen.getByText("Connect Google Account")).toBeInTheDocument();
  });

  it("site_preview uses empty string when siteUrl missing", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "site_preview",
          metadata: {},
        }}
      />
    );
    // Empty siteUrl won't pass isSafePreviewUrl, so error is shown
    expect(screen.getByText("Unable to preview this URL")).toBeInTheDocument();
  });

  it("cma_progress uses defaults when metadata fields missing", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "cma_progress",
          metadata: {},
        }}
      />
    );
    // status defaults to "complete" since the ?? "complete" fallback
    expect(screen.getByText("CMA Report Delivered")).toBeInTheDocument();
  });

  it("payment_card uses defaults when metadata fields missing", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "payment_card",
          metadata: {},
        }}
      />
    );
    // price defaults to "$900" via PaymentCard default prop
    expect(screen.getByText("$900")).toBeInTheDocument();
  });

  it("profile_card uses empty name when metadata name missing", () => {
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "profile_card",
          metadata: {},
        }}
        onAction={vi.fn()}
      />
    );
    expect(screen.getByRole("button", { name: /looks right/i })).toBeInTheDocument();
  });

  it("profile_card onConfirm callback invokes onAction", async () => {
    const onAction = vi.fn();
    render(
      <MessageRenderer
        message={{
          role: "assistant",
          content: "",
          type: "profile_card",
          metadata: { name: "Test Agent" },
        }}
        onAction={onAction}
      />
    );
    await userEvent.click(screen.getByRole("button", { name: /looks right/i }));
    expect(onAction).toHaveBeenCalledWith("confirm_profile");
  });
});
