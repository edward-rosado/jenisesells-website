// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render } from "@testing-library/react";
import { ScrollRevealSection } from "@/components/sections/shared/ScrollRevealSection";

vi.mock("@/hooks/useScrollReveal", () => ({
  useScrollReveal: vi.fn(() => false),
}));
vi.mock("@/hooks/useReducedMotion", () => ({
  useReducedMotion: vi.fn(() => false),
}));
import { useScrollReveal } from "@/hooks/useScrollReveal";
const mockUseScrollReveal = useScrollReveal as unknown as ReturnType<typeof vi.fn>;

describe("ScrollRevealSection", () => {
  afterEach(() => { vi.restoreAllMocks(); });

  it("renders children", () => {
    mockUseScrollReveal.mockReturnValue(true);
    const { getByText } = render(
      <ScrollRevealSection><div>Test Content</div></ScrollRevealSection>,
    );
    expect(getByText("Test Content")).toBeTruthy();
  });

  it("applies hidden styles when not visible", () => {
    mockUseScrollReveal.mockReturnValue(false);
    const { container } = render(
      <ScrollRevealSection><div>Test</div></ScrollRevealSection>,
    );
    const wrapper = container.firstChild as HTMLElement;
    expect(wrapper.style.opacity).toBe("0");
  });

  it("applies visible styles when visible", () => {
    mockUseScrollReveal.mockReturnValue(true);
    const { container } = render(
      <ScrollRevealSection><div>Test</div></ScrollRevealSection>,
    );
    const wrapper = container.firstChild as HTMLElement;
    expect(wrapper.style.opacity).toBe("1");
  });
});
