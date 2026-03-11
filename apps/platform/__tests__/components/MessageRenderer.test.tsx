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
          metadata: { siteUrl: "https://example.com", showCmaHighlight: true },
        }}
      />
    );
    expect(screen.getByText("Your CMA Form")).toBeInTheDocument();
    const iframe = screen.getByTitle("CMA form preview");
    expect(iframe).toHaveAttribute("src", "https://example.com#cma-form");
  });
});
