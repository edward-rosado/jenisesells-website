/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ProfilesClean } from "@/features/sections/profiles/ProfilesClean";

const ITEMS = [
  { id: "agent-a", name: "James W.", title: "Senior Associate" },
  { id: "agent-b", name: "Sarah C.", title: "Associate", headshot_url: "/headshot.jpg" },
];

describe("ProfilesClean", () => {
  it("renders all profile items", () => {
    render(<ProfilesClean items={ITEMS} title="Our Team" />);
    expect(screen.getByText("Our Team")).toBeInTheDocument();
    expect(screen.getByText("James W.")).toBeInTheDocument();
    expect(screen.getByText("Sarah C.")).toBeInTheDocument();
  });

  it("links each profile to /agents/{id}", () => {
    render(<ProfilesClean items={ITEMS} />);
    const links = screen.getAllByRole("link");
    expect(links[0]).toHaveAttribute("href", "/agents/agent-a");
    expect(links[1]).toHaveAttribute("href", "/agents/agent-b");
  });

  it("uses item.link when provided", () => {
    const items = [{ id: "x", name: "X", title: "T", link: "/custom" }];
    render(<ProfilesClean items={items} />);
    expect(screen.getByRole("link")).toHaveAttribute("href", "/custom");
  });

  it("renders headshot image when provided", () => {
    render(<ProfilesClean items={ITEMS} />);
    const img = screen.getByAltText("Sarah C.");
    expect(img).toBeInTheDocument();
  });

  it("renders placeholder when no headshot", () => {
    render(<ProfilesClean items={[ITEMS[0]]} />);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("renders initial letter as placeholder when no headshot", () => {
    render(<ProfilesClean items={[ITEMS[0]]} />);
    expect(screen.getByText("J")).toBeInTheDocument();
  });

  it("renders title and subtitle when provided", () => {
    render(<ProfilesClean items={ITEMS} title="Meet the Team" subtitle="Our experts" />);
    expect(screen.getByRole("heading", { level: 2, name: "Meet the Team" })).toBeInTheDocument();
    expect(screen.getByText("Our experts")).toBeInTheDocument();
  });

  it("does not render title heading when title is omitted", () => {
    render(<ProfilesClean items={ITEMS} />);
    expect(screen.queryByRole("heading", { level: 2 })).not.toBeInTheDocument();
  });

  it("uses id=profiles for anchor linking", () => {
    const { container } = render(<ProfilesClean items={ITEMS} />);
    expect(container.querySelector("#profiles")).toBeInTheDocument();
  });

  it("renders item titles", () => {
    render(<ProfilesClean items={ITEMS} />);
    expect(screen.getByText("Senior Associate")).toBeInTheDocument();
    expect(screen.getByText("Associate")).toBeInTheDocument();
  });

  it("applies hover lift on mouse enter", () => {
    render(<ProfilesClean items={ITEMS} />);
    const link = screen.getAllByRole("link")[0];
    const card = link.firstElementChild as HTMLElement;
    fireEvent.mouseEnter(card);
    expect(card.style.transform).toBe("translateY(-4px)");
    fireEvent.mouseLeave(card);
    expect(card.style.transform).toBe("none");
  });

  it("renders empty section when items is empty", () => {
    const { container } = render(<ProfilesClean items={[]} />);
    expect(container.querySelector("#profiles")).toBeInTheDocument();
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });
});
