// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render } from "@testing-library/react";
import { ServicesPremium } from "@/components/sections/services/ServicesPremium";
import type { FeatureItem } from "@/lib/types";

// Mock hooks
vi.mock("@/hooks/useScrollReveal", () => ({
  useScrollReveal: vi.fn(() => true), // Always visible in tests
}));
vi.mock("@/hooks/useReducedMotion", () => ({
  useReducedMotion: vi.fn(() => false),
}));
import { useScrollReveal } from "@/hooks/useScrollReveal";
const mockUseScrollReveal = useScrollReveal as unknown as ReturnType<typeof vi.fn>;

const ITEMS: FeatureItem[] = [
  { title: "Market Analysis", description: "Deep market insights", category: "Expert" },
  { title: "Negotiation", description: "Data-driven offers", icon: "📊" },
  { title: "Digital Access", description: "Virtual tours" },
];

describe("ServicesPremium", () => {
  afterEach(() => { vi.restoreAllMocks(); });

  it("renders nothing when items is empty", () => {
    const { container } = render(<ServicesPremium items={[]} />);
    expect(container.querySelector("section")).toBeNull();
  });

  it("renders all feature items as full-bleed blocks", () => {
    const { getAllByRole } = render(<ServicesPremium items={ITEMS} />);
    // Each block has heading h3
    const headings = getAllByRole("heading", { level: 3 });
    expect(headings).toHaveLength(3);
    expect(headings[0].textContent).toBe("Market Analysis");
  });

  it("renders title and subtitle when provided", () => {
    const { getByText } = render(
      <ServicesPremium items={ITEMS} title="Our Services" subtitle="Full package" />,
    );
    expect(getByText("Our Services")).toBeTruthy();
    expect(getByText("Full package")).toBeTruthy();
  });

  it("renders title without subtitle (marginBottom 0)", () => {
    const { getByText, queryByText } = render(
      <ServicesPremium items={ITEMS} title="Services Only" />,
    );
    expect(getByText("Services Only")).toBeTruthy();
    expect(queryByText("Full package")).toBeNull();
  });

  it("renders category as label when available", () => {
    const { getByText } = render(<ServicesPremium items={ITEMS} />);
    expect(getByText("Expert")).toBeTruthy();
  });

  it("alternates layout direction (text-left/right)", () => {
    const { container } = render(<ServicesPremium items={ITEMS} />);
    const blocks = container.querySelectorAll("[data-feature-block]");
    // First block: normal (text left), second: reversed
    expect(blocks[0]?.getAttribute("data-direction")).toBe("normal");
    expect(blocks[1]?.getAttribute("data-direction")).toBe("reversed");
  });

  it("alternates background colors", () => {
    const { container } = render(<ServicesPremium items={ITEMS} />);
    const blocks = container.querySelectorAll("[data-feature-block]");
    // First: light, second: dark, third: light
    const bg0 = (blocks[0] as HTMLElement).style.background;
    const bg1 = (blocks[1] as HTMLElement).style.background;
    expect(bg0).not.toBe(bg1);
  });

  it("renders icon/emoji in visual area when provided", () => {
    const { getByText } = render(<ServicesPremium items={ITEMS} />);
    expect(getByText("📊")).toBeTruthy();
  });

  it("renders description text", () => {
    const { getByText } = render(<ServicesPremium items={ITEMS} />);
    expect(getByText("Deep market insights")).toBeTruthy();
  });

  it("renders category with dark mode color on odd-index blocks", () => {
    // Item at index 1 (odd) with category triggers isDark category color
    const itemsWithCategory: FeatureItem[] = [
      { title: "First", description: "desc1" },
      { title: "Second", description: "desc2", category: "Premium" },
    ];
    const { getByText } = render(<ServicesPremium items={itemsWithCategory} />);
    expect(getByText("Premium")).toBeTruthy();
  });

  it("applies dark background on odd-index blocks", () => {
    const { container } = render(<ServicesPremium items={ITEMS} />);
    const blocks = container.querySelectorAll("[data-feature-block]");
    // Index 1 (odd) should have dark bg
    const bg1 = (blocks[1] as HTMLElement).style.background;
    expect(bg1).toMatch(/26.*26.*46|1a1a2e/i);
  });

  it("uses custom background_color and #f0f7ff gradient branch", () => {
    const itemsWithBg: FeatureItem[] = [
      { title: "Blue BG", description: "desc", background_color: "#f0f7ff" },
    ];
    const { container } = render(<ServicesPremium items={itemsWithBg} />);
    const block = container.querySelector("[data-feature-block]") as HTMLElement;
    // bg should contain the color (may be hex or rgb)
    expect(block.style.background).toBeTruthy();
    // The visual shape inside should use the blue gradient
    const innerDivs = block.querySelectorAll("div");
    const shapeStyles = Array.from(innerDivs).map(d => d.style.background).join(" ");
    // jsdom converts hex to rgb: #e3f2fd -> rgb(227, 242, 253)
    expect(shapeStyles).toMatch(/227.*242.*253/);
  });

  it("uses non-f0f7ff custom background for green gradient branch", () => {
    const itemsWithBg: FeatureItem[] = [
      { title: "Custom BG", description: "desc", background_color: "#faf7f2" },
    ];
    const { container } = render(<ServicesPremium items={itemsWithBg} />);
    const block = container.querySelector("[data-feature-block]") as HTMLElement;
    expect(block.style.background).toBeTruthy();
    // The visual shape should use the green gradient: #e8f5e9 -> rgb(232, 245, 233)
    const innerDivs = block.querySelectorAll("div");
    const shapeStyles = Array.from(innerDivs).map(d => d.style.background).join(" ");
    expect(shapeStyles).toMatch(/232.*245.*233/);
  });

  it("applies dark mode gradient on odd-index visual shape", () => {
    // Two items: index 0 is light, index 1 is dark
    const twoItems: FeatureItem[] = [
      { title: "First", description: "desc1" },
      { title: "Dark", description: "desc2" },
    ];
    const { container } = render(<ServicesPremium items={twoItems} />);
    const blocks = container.querySelectorAll("[data-feature-block]");
    const darkBlock = blocks[1] as HTMLElement;
    const innerDivs = darkBlock.querySelectorAll("div");
    const shapeStyles = Array.from(innerDivs).map(d => d.style.background).join(" ");
    // rgba(255,255,255,0.06) may have spaces added by jsdom
    expect(shapeStyles).toMatch(/rgba\(255,\s*255,\s*255,\s*0\.06\)/);
  });

  it("renders image when image_url is provided", () => {
    const itemsWithImage: FeatureItem[] = [
      { title: "With Image", description: "Has photo", image_url: "/test/photo.jpg" },
    ];
    const { container } = render(<ServicesPremium items={itemsWithImage} />);
    const img = container.querySelector("img");
    expect(img).toBeTruthy();
    expect(img?.getAttribute("src")).toContain("photo.jpg");
    expect(img?.getAttribute("alt")).toBe("With Image");
  });

  it("renders image with dark shadow on odd-index block", () => {
    const itemsWithDarkImage: FeatureItem[] = [
      { title: "First", description: "light block" },
      { title: "Dark With Image", description: "dark block", image_url: "/test/dark-photo.jpg" },
    ];
    const { container } = render(<ServicesPremium items={itemsWithDarkImage} />);
    const imgs = container.querySelectorAll("img");
    expect(imgs.length).toBe(1);
    expect(imgs[0].getAttribute("alt")).toBe("Dark With Image");
  });

  it("renders placeholder shape when no image_url", () => {
    const itemsNoImage: FeatureItem[] = [
      { title: "No Image", description: "No photo" },
    ];
    const { container } = render(<ServicesPremium items={itemsNoImage} />);
    const img = container.querySelector("img");
    expect(img).toBeNull();
  });

  it("renders with opacity 0 and scale(0.92) when not visible", () => {
    mockUseScrollReveal.mockReturnValue(false);
    const { container } = render(<ServicesPremium items={[ITEMS[0]]} />);
    const block = container.querySelector("[data-feature-block]") as HTMLElement;
    // Find divs with opacity style set
    const allDivs = Array.from(block.querySelectorAll("div"));
    const hiddenDivs = allDivs.filter(d => d.style.opacity === "0");
    expect(hiddenDivs.length).toBeGreaterThanOrEqual(2);
    const scaledDiv = allDivs.find(d => d.style.transform === "scale(0.92)");
    expect(scaledDiv).toBeTruthy();
    mockUseScrollReveal.mockReturnValue(true);
  });

  it("marks visual area with data-feature-visual attribute", () => {
    const { container } = render(<ServicesPremium items={ITEMS} />);
    const visuals = container.querySelectorAll("[data-feature-visual]");
    expect(visuals.length).toBe(3);
  });

  it("marks image container with data-feature-image attribute", () => {
    const itemsWithImage: FeatureItem[] = [
      { title: "With Image", description: "Has photo", image_url: "/test/photo.jpg" },
    ];
    const { container } = render(<ServicesPremium items={itemsWithImage} />);
    const imageContainer = container.querySelector("[data-feature-image]");
    expect(imageContainer).toBeTruthy();
  });

  it("injects responsive CSS for mobile layout", () => {
    const { container } = render(<ServicesPremium items={ITEMS} />);
    const styleTag = container.querySelector("style");
    expect(styleTag?.textContent).toContain("data-feature-visual");
    expect(styleTag?.textContent).toContain("data-feature-image");
  });

  it("renders visual-shape className on placeholder (no image)", () => {
    const itemsNoImage: FeatureItem[] = [
      { title: "Shape", description: "placeholder" },
    ];
    const { container } = render(<ServicesPremium items={itemsNoImage} />);
    const shape = container.querySelector(".visual-shape");
    expect(shape).toBeTruthy();
  });

  it("uses smaller font size when icon is provided", () => {
    const itemsWithIcon: FeatureItem[] = [
      { title: "With Icon", description: "desc", icon: "🏠" },
    ];
    const { container } = render(<ServicesPremium items={itemsWithIcon} />);
    const shape = container.querySelector(".visual-shape") as HTMLElement;
    expect(shape.style.fontSize).toBe("72px");
  });

  it("uses default font size when no icon", () => {
    const itemsNoIcon: FeatureItem[] = [
      { title: "No Icon", description: "desc" },
    ];
    const { container } = render(<ServicesPremium items={itemsNoIcon} />);
    const shape = container.querySelector(".visual-shape") as HTMLElement;
    expect(shape.style.fontSize).toBe("80px");
  });
});
