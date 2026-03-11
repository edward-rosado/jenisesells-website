import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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
});
