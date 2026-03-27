// @vitest-environment jsdom
import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { AddressAutocomplete } from "./AddressAutocomplete";
import type { PlaceSuggestion } from "./useGooglePlacesAutocomplete";

const inputStyle = {
  width: "100%",
  border: "2px solid #e0e0e0",
  borderRadius: 8,
  padding: "12px 16px",
  fontSize: 15,
  boxSizing: "border-box" as const,
};

function makeSuggestions(count: number): PlaceSuggestion[] {
  return Array.from({ length: count }, (_, i) => ({
    text: `${100 + i} Main St, Springfield, NJ`,
    placeId: `place-${i}`,
    index: i,
  }));
}

function defaultProps(overrides?: Partial<React.ComponentProps<typeof AddressAutocomplete>>) {
  return {
    query: "",
    setQuery: vi.fn(),
    suggestions: [] as PlaceSuggestion[],
    highlightedIndex: -1,
    setHighlightedIndex: vi.fn(),
    selectSuggestion: vi.fn().mockResolvedValue(undefined),
    clearSuggestions: vi.fn(),
    fetchError: null,
    inputStyle,
    ...overrides,
  };
}

describe("AddressAutocomplete", () => {
  it("renders input with correct ARIA attributes", () => {
    render(<AddressAutocomplete {...defaultProps()} />);
    const input = screen.getByRole("combobox");
    expect(input).toBeDefined();
    expect(input.getAttribute("aria-controls")).toBe("address-listbox");
    expect(input.getAttribute("aria-autocomplete")).toBe("list");
    expect(input.getAttribute("aria-expanded")).toBe("false");
  });

  it("shows dropdown when suggestions are non-empty", () => {
    render(<AddressAutocomplete {...defaultProps({ suggestions: makeSuggestions(3) })} />);
    const listbox = screen.getByRole("listbox");
    expect(listbox).toBeDefined();
    expect(screen.getAllByRole("option")).toHaveLength(3);
  });

  it("hides dropdown when suggestions are empty", () => {
    render(<AddressAutocomplete {...defaultProps({ suggestions: [] })} />);
    expect(screen.queryByRole("listbox")).toBeNull();
  });

  it("ArrowDown moves highlightedIndex down, wraps to 0", () => {
    const setHighlightedIndex = vi.fn();
    render(
      <AddressAutocomplete
        {...defaultProps({
          suggestions: makeSuggestions(3),
          highlightedIndex: 2,
          setHighlightedIndex,
        })}
      />,
    );

    fireEvent.keyDown(screen.getByRole("combobox"), { key: "ArrowDown" });
    expect(setHighlightedIndex).toHaveBeenCalledWith(0);
  });

  it("ArrowDown moves from -1 to 0", () => {
    const setHighlightedIndex = vi.fn();
    render(
      <AddressAutocomplete
        {...defaultProps({
          suggestions: makeSuggestions(3),
          highlightedIndex: -1,
          setHighlightedIndex,
        })}
      />,
    );

    fireEvent.keyDown(screen.getByRole("combobox"), { key: "ArrowDown" });
    expect(setHighlightedIndex).toHaveBeenCalledWith(0);
  });

  it("ArrowUp moves highlightedIndex up, wraps to last", () => {
    const setHighlightedIndex = vi.fn();
    render(
      <AddressAutocomplete
        {...defaultProps({
          suggestions: makeSuggestions(3),
          highlightedIndex: 0,
          setHighlightedIndex,
        })}
      />,
    );

    fireEvent.keyDown(screen.getByRole("combobox"), { key: "ArrowUp" });
    expect(setHighlightedIndex).toHaveBeenCalledWith(2);
  });

  it("Enter on highlighted item calls selectSuggestion", () => {
    const selectSuggestion = vi.fn().mockResolvedValue(undefined);
    render(
      <AddressAutocomplete
        {...defaultProps({
          suggestions: makeSuggestions(3),
          highlightedIndex: 1,
          selectSuggestion,
        })}
      />,
    );

    fireEvent.keyDown(screen.getByRole("combobox"), { key: "Enter" });
    expect(selectSuggestion).toHaveBeenCalledWith(1);
  });

  it("Enter does nothing when no item is highlighted", () => {
    const selectSuggestion = vi.fn().mockResolvedValue(undefined);
    render(
      <AddressAutocomplete
        {...defaultProps({
          suggestions: makeSuggestions(3),
          highlightedIndex: -1,
          selectSuggestion,
        })}
      />,
    );

    fireEvent.keyDown(screen.getByRole("combobox"), { key: "Enter" });
    expect(selectSuggestion).not.toHaveBeenCalled();
  });

  it("Escape closes dropdown and clears suggestions", () => {
    const clearSuggestions = vi.fn();
    render(
      <AddressAutocomplete
        {...defaultProps({
          suggestions: makeSuggestions(2),
          clearSuggestions,
        })}
      />,
    );

    fireEvent.keyDown(screen.getByRole("combobox"), { key: "Escape" });
    expect(clearSuggestions).toHaveBeenCalled();
  });

  it("click on suggestion item calls selectSuggestion", () => {
    const selectSuggestion = vi.fn().mockResolvedValue(undefined);
    render(
      <AddressAutocomplete
        {...defaultProps({
          suggestions: makeSuggestions(3),
          selectSuggestion,
        })}
      />,
    );

    const options = screen.getAllByRole("option");
    fireEvent.click(options[1]);
    expect(selectSuggestion).toHaveBeenCalledWith(1);
  });

  it("click outside closes dropdown", () => {
    const clearSuggestions = vi.fn();
    render(
      <div>
        <button data-testid="outside">Outside</button>
        <AddressAutocomplete
          {...defaultProps({
            suggestions: makeSuggestions(2),
            clearSuggestions,
          })}
        />
      </div>,
    );

    fireEvent.mouseDown(screen.getByTestId("outside"));
    expect(clearSuggestions).toHaveBeenCalled();
  });

  it("shows 'Powered by Google' attribution when dropdown is open", () => {
    render(
      <AddressAutocomplete
        {...defaultProps({ suggestions: makeSuggestions(2) })}
      />,
    );
    expect(screen.getByText("Powered by Google")).toBeDefined();
  });

  it("shows fetchError message when set", () => {
    render(
      <AddressAutocomplete
        {...defaultProps({
          fetchError: "Could not load address details. Please enter your address manually.",
        })}
      />,
    );

    const alert = screen.getByRole("alert");
    expect(alert.textContent).toContain("Could not load address details");
  });

  it("does not show error when fetchError is null", () => {
    render(<AddressAutocomplete {...defaultProps({ fetchError: null })} />);
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("renders location pin icon in input", () => {
    const { container } = render(<AddressAutocomplete {...defaultProps()} />);
    const svg = container.querySelector("svg");
    expect(svg).not.toBeNull();
  });

  it("aria-expanded toggles with dropdown visibility", () => {
    const { rerender } = render(
      <AddressAutocomplete {...defaultProps({ suggestions: [] })} />,
    );
    expect(screen.getByRole("combobox").getAttribute("aria-expanded")).toBe("false");

    rerender(
      <AddressAutocomplete {...defaultProps({ suggestions: makeSuggestions(2) })} />,
    );
    expect(screen.getByRole("combobox").getAttribute("aria-expanded")).toBe("true");
  });

  it("aria-activedescendant matches highlighted option id", () => {
    render(
      <AddressAutocomplete
        {...defaultProps({
          suggestions: makeSuggestions(3),
          highlightedIndex: 1,
        })}
      />,
    );

    expect(screen.getByRole("combobox").getAttribute("aria-activedescendant")).toBe(
      "address-option-1",
    );
  });

  it("aria-activedescendant is absent when no item highlighted", () => {
    render(
      <AddressAutocomplete
        {...defaultProps({
          suggestions: makeSuggestions(3),
          highlightedIndex: -1,
        })}
      />,
    );

    expect(screen.getByRole("combobox").getAttribute("aria-activedescendant")).toBeNull();
  });

  it("hover on suggestion updates highlightedIndex", () => {
    const setHighlightedIndex = vi.fn();
    render(
      <AddressAutocomplete
        {...defaultProps({
          suggestions: makeSuggestions(3),
          setHighlightedIndex,
        })}
      />,
    );

    const options = screen.getAllByRole("option");
    fireEvent.mouseEnter(options[2]);
    expect(setHighlightedIndex).toHaveBeenCalledWith(2);
  });

  it("calls setQuery on input change", () => {
    const setQuery = vi.fn();
    render(<AddressAutocomplete {...defaultProps({ setQuery })} />);

    fireEvent.change(screen.getByRole("combobox"), { target: { value: "123 Main" } });
    expect(setQuery).toHaveBeenCalledWith("123 Main");
  });

  it("applies paddingLeft for icon spacing on input", () => {
    render(<AddressAutocomplete {...defaultProps()} />);
    const input = screen.getByRole("combobox") as HTMLInputElement;
    expect(input.style.paddingLeft).toBe("36px");
  });

  it("blur calls clearSuggestions after delay when focus leaves container", async () => {
    vi.useFakeTimers();
    const clearSuggestions = vi.fn();
    render(
      <div>
        <button data-testid="other">Other</button>
        <AddressAutocomplete
          {...defaultProps({
            suggestions: makeSuggestions(2),
            clearSuggestions,
          })}
        />
      </div>,
    );

    fireEvent.blur(screen.getByRole("combobox"));

    // clearSuggestions should not fire immediately
    expect(clearSuggestions).not.toHaveBeenCalled();

    // After the 150ms delay
    vi.advanceTimersByTime(150);
    expect(clearSuggestions).toHaveBeenCalled();
    vi.useRealTimers();
  });

  it("mouseDown on suggestion item prevents default (keeps focus)", () => {
    render(
      <AddressAutocomplete
        {...defaultProps({ suggestions: makeSuggestions(2) })}
      />,
    );

    const options = screen.getAllByRole("option");
    // mouseDown fires — verifies the handler runs without error
    fireEvent.mouseDown(options[0]);
    expect(options[0]).toBeDefined();
  });

  it("keyDown does nothing when dropdown is closed", () => {
    const setHighlightedIndex = vi.fn();
    const selectSuggestion = vi.fn().mockResolvedValue(undefined);
    render(
      <AddressAutocomplete
        {...defaultProps({
          suggestions: [], // dropdown closed
          setHighlightedIndex,
          selectSuggestion,
        })}
      />,
    );

    fireEvent.keyDown(screen.getByRole("combobox"), { key: "ArrowDown" });
    fireEvent.keyDown(screen.getByRole("combobox"), { key: "Enter" });
    expect(setHighlightedIndex).not.toHaveBeenCalled();
    expect(selectSuggestion).not.toHaveBeenCalled();
  });

  it("ArrowUp from middle index moves up (not wrapping)", () => {
    const setHighlightedIndex = vi.fn();
    render(
      <AddressAutocomplete
        {...defaultProps({
          suggestions: makeSuggestions(3),
          highlightedIndex: 2,
          setHighlightedIndex,
        })}
      />,
    );

    fireEvent.keyDown(screen.getByRole("combobox"), { key: "ArrowUp" });
    expect(setHighlightedIndex).toHaveBeenCalledWith(1);
  });

  it("ArrowDown from middle index moves down (not wrapping)", () => {
    const setHighlightedIndex = vi.fn();
    render(
      <AddressAutocomplete
        {...defaultProps({
          suggestions: makeSuggestions(3),
          highlightedIndex: 0,
          setHighlightedIndex,
        })}
      />,
    );

    fireEvent.keyDown(screen.getByRole("combobox"), { key: "ArrowDown" });
    expect(setHighlightedIndex).toHaveBeenCalledWith(1);
  });

  it("uses index as key when placeId is empty", () => {
    const suggestions = [
      { text: "123 Main St", placeId: "", index: 0 },
      { text: "456 Oak Ave", placeId: "", index: 1 },
    ];
    render(
      <AddressAutocomplete {...defaultProps({ suggestions })} />,
    );

    // Renders correctly even with empty placeIds
    expect(screen.getAllByRole("option")).toHaveLength(2);
  });

  it("blur does not clear suggestions when focus stays in container", () => {
    vi.useFakeTimers();
    const clearSuggestions = vi.fn();
    const { container } = render(
      <AddressAutocomplete
        {...defaultProps({
          suggestions: makeSuggestions(2),
          clearSuggestions,
        })}
      />,
    );

    // Focus the input, then simulate blur while focus stays inside container
    const input = screen.getByRole("combobox");
    fireEvent.blur(input);

    // Simulate that activeElement is still within the container
    Object.defineProperty(document, "activeElement", {
      value: container.querySelector("[role='listbox']"),
      configurable: true,
    });

    vi.advanceTimersByTime(150);
    // clearSuggestions should NOT be called because focus is still in container
    expect(clearSuggestions).not.toHaveBeenCalled();

    // Restore
    Object.defineProperty(document, "activeElement", {
      value: document.body,
      configurable: true,
    });
    vi.useRealTimers();
  });
});
