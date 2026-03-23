import { render, screen, act } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi, afterEach } from "vitest";

// Mock next/link for static pages that use it
vi.mock("next/link", () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

vi.mock("@real-estate-star/analytics", () => ({
  reportError: vi.fn(),
  setErrorReporter: vi.fn(),
  initWebVitals: vi.fn(),
}));

describe("Accessibility page", () => {
  it("renders the heading and content", async () => {
    const mod = await import("../app/accessibility/page");
    const AccessibilityPage = mod.default;
    render(<AccessibilityPage />);
    expect(screen.getByRole("heading", { name: /accessibility statement/i })).toBeInTheDocument();
    expect(screen.getByText(/our commitment/i)).toBeInTheDocument();
  });
});

describe("DMCA page", () => {
  it("renders the heading and content", async () => {
    const mod = await import("../app/dmca/page");
    const DmcaPage = mod.default;
    render(<DmcaPage />);
    expect(screen.getByRole("heading", { name: /dmca policy/i })).toBeInTheDocument();
    expect(screen.getByText(/overview/i)).toBeInTheDocument();
  });
});

describe("Privacy page", () => {
  it("renders the heading and content", async () => {
    const mod = await import("../app/privacy/page");
    const PrivacyPage = mod.default;
    render(<PrivacyPage />);
    expect(screen.getByRole("heading", { name: /privacy policy/i })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: /1\. overview/i })).toBeInTheDocument();
  });
});

describe("Terms page", () => {
  it("renders the heading and content", async () => {
    const mod = await import("../app/terms/page");
    const TermsPage = mod.default;
    render(<TermsPage />);
    expect(screen.getByRole("heading", { name: /terms of service/i })).toBeInTheDocument();
    expect(screen.getByText(/acceptance of terms/i)).toBeInTheDocument();
  });
});

describe("Global Error page", () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it("renders error message and reset button", async () => {
    const mod = await import("../app/global-error");
    const GlobalError = mod.default;
    const mockReset = vi.fn();
    await act(async () => {
      render(
        <GlobalError error={new Error("Test error")} reset={mockReset} />
      );
    });
    expect(screen.getByText("Something went wrong!")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));
    expect(mockReset).toHaveBeenCalledOnce();
  });

  it("calls reportError with the error on mount", async () => {
    const { reportError } = await import("@real-estate-star/analytics");
    const mod = await import("../app/global-error");
    const GlobalError = mod.default;
    const err = new Error("boom");

    await act(async () => {
      render(<GlobalError error={err} reset={vi.fn()} />);
    });

    expect(reportError).toHaveBeenCalledWith(err, undefined);
  });

  it("calls reportError with digest context when error.digest is set", async () => {
    const { reportError } = await import("@real-estate-star/analytics");
    const mod = await import("../app/global-error");
    const GlobalError = mod.default;
    const err = Object.assign(new Error("with digest"), { digest: "abc123" });

    await act(async () => {
      render(<GlobalError error={err} reset={vi.fn()} />);
    });

    expect(reportError).toHaveBeenCalledWith(err, { digest: "abc123" });
  });
});
