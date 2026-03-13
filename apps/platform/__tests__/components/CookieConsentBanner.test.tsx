import { render, screen, cleanup, act } from "@testing-library/react";
import { renderToString } from "react-dom/server";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import { CookieConsentBanner } from "@/components/legal/CookieConsentBanner";

describe("CookieConsentBanner", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("renders when no consent has been given", () => {
    render(<CookieConsentBanner />);
    expect(
      screen.getByRole("dialog", { name: /cookie consent/i })
    ).toBeInTheDocument();
  });

  it("does not render when consent has been accepted", () => {
    localStorage.setItem("res-cookie-consent", "accepted");
    render(<CookieConsentBanner />);
    expect(
      screen.queryByRole("dialog", { name: /cookie consent/i })
    ).not.toBeInTheDocument();
  });

  it("does not render when consent has been declined", () => {
    localStorage.setItem("res-cookie-consent", "declined");
    render(<CookieConsentBanner />);
    expect(
      screen.queryByRole("dialog", { name: /cookie consent/i })
    ).not.toBeInTheDocument();
  });

  it("hides banner and stores 'accepted' when Accept is clicked", async () => {
    const user = userEvent.setup();
    render(<CookieConsentBanner />);

    await user.click(screen.getByRole("button", { name: /accept cookies/i }));

    expect(
      screen.queryByRole("dialog", { name: /cookie consent/i })
    ).not.toBeInTheDocument();
    expect(localStorage.getItem("res-cookie-consent")).toBe("accepted");
  });

  it("hides banner and stores 'declined' when Decline is clicked", async () => {
    const user = userEvent.setup();
    render(<CookieConsentBanner />);

    await user.click(screen.getByRole("button", { name: /decline cookies/i }));

    expect(
      screen.queryByRole("dialog", { name: /cookie consent/i })
    ).not.toBeInTheDocument();
    expect(localStorage.getItem("res-cookie-consent")).toBe("declined");
  });

  it("contains a link to the privacy policy", () => {
    render(<CookieConsentBanner />);
    const link = screen.getByRole("link", { name: /privacy policy/i });
    expect(link).toHaveAttribute("href", "/privacy");
  });

  it("hides banner when storage event updates consent externally", () => {
    render(<CookieConsentBanner />);
    expect(
      screen.getByRole("dialog", { name: /cookie consent/i })
    ).toBeInTheDocument();

    // Simulate another tab setting consent via storage event
    act(() => {
      localStorage.setItem("res-cookie-consent", "accepted");
      window.dispatchEvent(new StorageEvent("storage"));
    });

    expect(
      screen.queryByRole("dialog", { name: /cookie consent/i })
    ).not.toBeInTheDocument();
  });

  it("renders nothing during SSR because getServerSnapshot returns a truthy value", () => {
    const html = renderToString(<CookieConsentBanner />);
    expect(html).toBe("");
  });

  it("cleans up storage event listener on unmount", () => {
    const removeSpy = vi.spyOn(window, "removeEventListener");
    render(<CookieConsentBanner />);

    cleanup();

    expect(removeSpy).toHaveBeenCalledWith(
      "storage",
      expect.any(Function)
    );
    removeSpy.mockRestore();
  });
});
