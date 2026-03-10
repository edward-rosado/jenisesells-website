import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import OnboardPage from "../app/onboard/page";

global.fetch = vi.fn().mockResolvedValue({
  ok: true,
  json: () => Promise.resolve({ sessionId: "abc123" }),
});

vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams("profileUrl=https://zillow.com/profile/test"),
}));

describe("OnboardPage", () => {
  it("creates a session on mount", async () => {
    render(<OnboardPage />);
    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining("/onboard"),
        expect.objectContaining({ method: "POST" })
      );
    });
  });

  it("shows loading state initially", () => {
    render(<OnboardPage />);
    expect(screen.getByText(/Starting your onboarding/i)).toBeInTheDocument();
  });
});
