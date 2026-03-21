import { renderHook, act, cleanup } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  useGoogleMapsAutocomplete,
  parseAddressComponents,
  resetLoadPromise,
} from "./useGoogleMapsAutocomplete";
import type { RefObject } from "react";

// --- Google Maps mock setup ---

interface MockAutocomplete {
  addListener: ReturnType<typeof vi.fn>;
  getPlace: ReturnType<typeof vi.fn>;
}

let mockAutocompleteInstance: MockAutocomplete;
let placeChangedCallback: (() => void) | null = null;
let mockRemoveListener: ReturnType<typeof vi.fn>;
let listenerHandle: object;

function setupGoogleMapsMock() {
  mockRemoveListener = vi.fn();
  listenerHandle = { remove: vi.fn() };
  mockAutocompleteInstance = {
    addListener: vi.fn(function addListener(event: string, cb: () => void) {
      if (event === "place_changed") {
        placeChangedCallback = cb;
      }
      return listenerHandle;
    }),
    getPlace: vi.fn(),
  };

  function MockAutocompleteClass() {
    return mockAutocompleteInstance;
  }

  (window as any).google = {
    maps: {
      places: {
        Autocomplete: MockAutocompleteClass,
      },
      event: {
        removeListener: mockRemoveListener,
      },
    },
  };
}

function clearGoogleMapsMock() {
  delete (window as any).google;
  placeChangedCallback = null;
}

function makeInputRef(el: HTMLInputElement | null = document.createElement("input")): RefObject<HTMLInputElement> {
  return { current: el } as RefObject<HTMLInputElement>;
}

describe("parseAddressComponents", () => {
  it("parses full address components", () => {
    const components = [
      { long_name: "123", short_name: "123", types: ["street_number"] },
      { long_name: "Main St", short_name: "Main St", types: ["route"] },
      { long_name: "Springfield", short_name: "Springfield", types: ["locality"] },
      { long_name: "Illinois", short_name: "IL", types: ["administrative_area_level_1"] },
      { long_name: "62704", short_name: "62704", types: ["postal_code"] },
    ];

    const result = parseAddressComponents(components);
    expect(result).toEqual({
      address: "123 Main St",
      city: "Springfield",
      state: "IL",
      zip: "62704",
    });
  });

  it("returns empty string for missing components (no zip)", () => {
    const components = [
      { long_name: "456", short_name: "456", types: ["street_number"] },
      { long_name: "Oak Ave", short_name: "Oak Ave", types: ["route"] },
      { long_name: "Denver", short_name: "Denver", types: ["locality"] },
      { long_name: "Colorado", short_name: "CO", types: ["administrative_area_level_1"] },
    ];

    const result = parseAddressComponents(components);
    expect(result).toEqual({
      address: "456 Oak Ave",
      city: "Denver",
      state: "CO",
      zip: "",
    });
  });

  it("falls back to sublocality_level_1 for city when locality is missing", () => {
    const components = [
      { long_name: "Brooklyn", short_name: "Brooklyn", types: ["sublocality_level_1"] },
      { long_name: "New York", short_name: "NY", types: ["administrative_area_level_1"] },
    ];

    const result = parseAddressComponents(components);
    expect(result.city).toBe("Brooklyn");
  });

  it("prefers locality over sublocality_level_1 for city", () => {
    const components = [
      { long_name: "New York", short_name: "New York", types: ["locality"] },
      { long_name: "Manhattan", short_name: "Manhattan", types: ["sublocality_level_1"] },
    ];

    const result = parseAddressComponents(components);
    expect(result.city).toBe("New York");
  });

  it("returns all empty strings for empty components array", () => {
    const result = parseAddressComponents([]);
    expect(result).toEqual({ address: "", city: "", state: "", zip: "" });
  });
});

describe("useGoogleMapsAutocomplete", () => {
  beforeEach(() => {
    resetLoadPromise();
    clearGoogleMapsMock();
    document.head.querySelectorAll("script").forEach((s) => s.remove());
  });

  afterEach(() => {
    cleanup();
    clearGoogleMapsMock();
  });

  it("does not inject script tag when enabled=false", () => {
    const inputRef = makeInputRef();
    const onPlaceSelected = vi.fn();

    renderHook(() =>
      useGoogleMapsAutocomplete({
        apiKey: "test-key",
        inputRef,
        onPlaceSelected,
        enabled: false,
      }),
    );

    const scripts = document.head.querySelectorAll("script");
    expect(scripts.length).toBe(0);
  });

  it("returns { loaded: false } initially when enabled=false", () => {
    const inputRef = makeInputRef();
    const onPlaceSelected = vi.fn();

    const { result } = renderHook(() =>
      useGoogleMapsAutocomplete({
        apiKey: "test-key",
        inputRef,
        onPlaceSelected,
        enabled: false,
      }),
    );

    expect(result.current.loaded).toBe(false);
  });

  it("injects script tag when enabled=true", () => {
    const inputRef = makeInputRef();
    const onPlaceSelected = vi.fn();

    renderHook(() =>
      useGoogleMapsAutocomplete({
        apiKey: "test-key",
        inputRef,
        onPlaceSelected,
        enabled: true,
      }),
    );

    const scripts = document.head.querySelectorAll("script");
    expect(scripts.length).toBe(1);
    expect(scripts[0].src).toContain("maps.googleapis.com/maps/api/js");
    expect(scripts[0].src).toContain("key=test-key");
    expect(scripts[0].src).toContain("libraries=places");
  });

  it("returns { loaded: true } after SDK loads", async () => {
    // Set up mock BEFORE rendering so loadGoogleMapsScript detects
    // window.google.maps.places and resolves immediately
    setupGoogleMapsMock();
    const inputRef = makeInputRef();
    const onPlaceSelected = vi.fn();

    const { result } = renderHook(() =>
      useGoogleMapsAutocomplete({
        apiKey: "test-key",
        inputRef,
        onPlaceSelected,
        enabled: true,
      }),
    );

    // The promise resolves on next microtask; flush with act
    await act(async () => {
      // Allow the resolved promise's .then() to fire
    });

    expect(result.current.loaded).toBe(true);
  });

  it("calls onPlaceSelected with parsed address components", async () => {
    setupGoogleMapsMock();
    const inputRef = makeInputRef();
    const onPlaceSelected = vi.fn();

    mockAutocompleteInstance.getPlace.mockReturnValue({
      address_components: [
        { long_name: "789", short_name: "789", types: ["street_number"] },
        { long_name: "Elm St", short_name: "Elm St", types: ["route"] },
        { long_name: "Austin", short_name: "Austin", types: ["locality"] },
        { long_name: "Texas", short_name: "TX", types: ["administrative_area_level_1"] },
        { long_name: "73301", short_name: "73301", types: ["postal_code"] },
      ],
    });

    renderHook(() =>
      useGoogleMapsAutocomplete({
        apiKey: "test-key",
        inputRef,
        onPlaceSelected,
        enabled: true,
      }),
    );

    // Wait for the async loadGoogleMapsScript to resolve and set up the listener
    await act(async () => {});

    expect(placeChangedCallback).not.toBeNull();

    act(() => {
      placeChangedCallback!();
    });

    expect(onPlaceSelected).toHaveBeenCalledWith({
      address: "789 Elm St",
      city: "Austin",
      state: "TX",
      zip: "73301",
    });
  });

  it("does not call onPlaceSelected when place has no address_components", async () => {
    setupGoogleMapsMock();
    const inputRef = makeInputRef();
    const onPlaceSelected = vi.fn();

    mockAutocompleteInstance.getPlace.mockReturnValue({});

    renderHook(() =>
      useGoogleMapsAutocomplete({
        apiKey: "test-key",
        inputRef,
        onPlaceSelected,
        enabled: true,
      }),
    );

    await act(async () => {});

    expect(placeChangedCallback).not.toBeNull();

    act(() => {
      placeChangedCallback!();
    });

    expect(onPlaceSelected).not.toHaveBeenCalled();
  });

  it("deduplicates script loading (two hooks, one script tag)", () => {
    const inputRef1 = makeInputRef();
    const inputRef2 = makeInputRef();
    const onPlaceSelected = vi.fn();

    renderHook(() =>
      useGoogleMapsAutocomplete({
        apiKey: "test-key",
        inputRef: inputRef1,
        onPlaceSelected,
        enabled: true,
      }),
    );

    renderHook(() =>
      useGoogleMapsAutocomplete({
        apiKey: "test-key",
        inputRef: inputRef2,
        onPlaceSelected,
        enabled: true,
      }),
    );

    const scripts = document.head.querySelectorAll("script");
    expect(scripts.length).toBe(1);
  });

  it("cleans up on unmount", async () => {
    setupGoogleMapsMock();
    const inputRef = makeInputRef();
    const onPlaceSelected = vi.fn();

    const { result, unmount } = renderHook(() =>
      useGoogleMapsAutocomplete({
        apiKey: "test-key",
        inputRef,
        onPlaceSelected,
        enabled: true,
      }),
    );

    await act(async () => {});

    expect(result.current.loaded).toBe(true);

    unmount();

    expect(mockRemoveListener).toHaveBeenCalledWith(listenerHandle);
  });

  it("does not create autocomplete if inputRef.current is null", async () => {
    setupGoogleMapsMock();
    const inputRef = makeInputRef(null);
    const onPlaceSelected = vi.fn();

    const { result } = renderHook(() =>
      useGoogleMapsAutocomplete({
        apiKey: "test-key",
        inputRef,
        onPlaceSelected,
        enabled: true,
      }),
    );

    await act(async () => {});

    expect(result.current.loaded).toBe(false);
    // Autocomplete constructor should not have been called with null input
    expect(placeChangedCallback).toBeNull();
  });

  it("cancels setup when unmounted before script loads", async () => {
    // Don't set up google mock — the script tag will be injected but won't load
    const inputRef = makeInputRef();
    const onPlaceSelected = vi.fn();

    const { unmount } = renderHook(() =>
      useGoogleMapsAutocomplete({
        apiKey: "test-key",
        inputRef,
        onPlaceSelected,
        enabled: true,
      }),
    );

    // Unmount immediately — the cleanup sets cancelled = true
    unmount();

    // Now simulate script load completing after unmount
    setupGoogleMapsMock();
    const script = document.head.querySelector("script")!;
    await act(async () => {
      script.onload!(new Event("load"));
    });

    // Autocomplete should NOT have been created because cancelled was set
    expect(placeChangedCallback).toBeNull();
  });

  it("handles script load error gracefully", async () => {
    const inputRef = makeInputRef();
    const onPlaceSelected = vi.fn();

    renderHook(() =>
      useGoogleMapsAutocomplete({
        apiKey: "test-key",
        inputRef,
        onPlaceSelected,
        enabled: true,
      }),
    );

    // Simulate script load error
    const script = document.head.querySelector("script")!;
    await act(async () => {
      script.onerror!(new Event("error"));
    });

    // Should not throw, hook degrades gracefully
  });

  it("reverts loaded to false when gm_authFailure fires (API key rejected)", async () => {
    setupGoogleMapsMock();
    const inputRef = makeInputRef();
    const onPlaceSelected = vi.fn();

    const { result } = renderHook(() =>
      useGoogleMapsAutocomplete({
        apiKey: "test-key",
        inputRef,
        onPlaceSelected,
        enabled: true,
      }),
    );

    // google.maps.places exists so loadGoogleMapsScript resolves immediately
    await act(async () => {});

    // loaded should be true after SDK resolves
    expect(result.current.loaded).toBe(true);

    // Simulate Google calling gm_authFailure (domain not authorized)
    await act(async () => {
      (window as any).gm_authFailure();
    });

    expect(result.current.loaded).toBe(false);
  });

  it("restores previous gm_authFailure handler on unmount", async () => {
    const prevHandler = vi.fn();
    (window as any).gm_authFailure = prevHandler;

    setupGoogleMapsMock();
    const inputRef = makeInputRef();
    const { unmount } = renderHook(() =>
      useGoogleMapsAutocomplete({
        apiKey: "test-key",
        inputRef,
        onPlaceSelected: vi.fn(),
        enabled: true,
      }),
    );

    await act(async () => {});

    // gm_authFailure should chain to previous handler
    await act(async () => {
      (window as any).gm_authFailure();
    });
    expect(prevHandler).toHaveBeenCalled();

    unmount();
    // After unmount, the original handler should be restored
    expect((window as any).gm_authFailure).toBe(prevHandler);
  });
});
