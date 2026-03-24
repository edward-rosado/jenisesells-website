import { renderHook, act, cleanup } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  useGooglePlacesAutocomplete,
  parseAddressComponents,
  resetLoadState,
} from "./useGooglePlacesAutocomplete";

// ── Google Maps mock setup ────────────────────────────────────────────

let mockFetchAutocompleteSuggestions: ReturnType<typeof vi.fn>;
let mockToPlace: ReturnType<typeof vi.fn>;
let mockFetchFields: ReturnType<typeof vi.fn>;
let mockSessionTokenConstructor: ReturnType<typeof vi.fn>;
let mockImportLibrary: ReturnType<typeof vi.fn>;

function makeMockPlace(addressComponents?: any[]) {
  return {
    addressComponents,
    fetchFields: mockFetchFields,
  };
}

function setupGoogleMapsMock(opts?: { importLibraryRejects?: boolean }) {
  mockFetchAutocompleteSuggestions = vi.fn().mockResolvedValue({ suggestions: [] });
  mockFetchFields = vi.fn().mockResolvedValue(undefined);
  mockToPlace = vi.fn(() => makeMockPlace());
  mockSessionTokenConstructor = vi.fn();

  const placesLib = {
    AutocompleteSuggestion: {
      fetchAutocompleteSuggestions: mockFetchAutocompleteSuggestions,
    },
    AutocompleteSessionToken: mockSessionTokenConstructor,
  };

  mockImportLibrary = opts?.importLibraryRejects
    ? vi.fn().mockRejectedValue(new Error("importLibrary failed"))
    : vi.fn().mockResolvedValue(placesLib);

  (window as any).google = {
    maps: {
      importLibrary: mockImportLibrary,
    },
  };

  return placesLib;
}

function clearGoogleMapsMock() {
  delete (window as any).google;
}

function defaultOptions(overrides?: Partial<Parameters<typeof useGooglePlacesAutocomplete>[0]>) {
  return {
    apiKey: "test-key",
    enabled: true,
    stateCode: "NJ",
    onPlaceSelected: vi.fn(),
    ...overrides,
  };
}

// ── Tests ─────────────────────────────────────────────────────────────

describe("parseAddressComponents", () => {
  it("parses full address with new longText/shortText property names", () => {
    const components = [
      { longText: "123", shortText: "123", types: ["street_number"] },
      { longText: "Main St", shortText: "Main St", types: ["route"] },
      { longText: "Springfield", shortText: "Springfield", types: ["locality"] },
      { longText: "New Jersey", shortText: "NJ", types: ["administrative_area_level_1"] },
      { longText: "07081", shortText: "07081", types: ["postal_code"] },
    ];

    expect(parseAddressComponents(components)).toEqual({
      address: "123 Main St",
      city: "Springfield",
      state: "NJ",
      zip: "07081",
    });
  });

  it("returns empty strings for missing components", () => {
    const components = [
      { longText: "456", shortText: "456", types: ["street_number"] },
      { longText: "Oak Ave", shortText: "Oak Ave", types: ["route"] },
      { longText: "Denver", shortText: "Denver", types: ["locality"] },
      { longText: "Colorado", shortText: "CO", types: ["administrative_area_level_1"] },
    ];

    expect(parseAddressComponents(components)).toEqual({
      address: "456 Oak Ave",
      city: "Denver",
      state: "CO",
      zip: "",
    });
  });

  it("falls back to sublocality_level_1 when locality is missing", () => {
    const components = [
      { longText: "Brooklyn", shortText: "Brooklyn", types: ["sublocality_level_1"] },
      { longText: "New York", shortText: "NY", types: ["administrative_area_level_1"] },
    ];

    expect(parseAddressComponents(components).city).toBe("Brooklyn");
  });

  it("prefers locality over sublocality_level_1", () => {
    const components = [
      { longText: "New York", shortText: "New York", types: ["locality"] },
      { longText: "Manhattan", shortText: "Manhattan", types: ["sublocality_level_1"] },
    ];

    expect(parseAddressComponents(components).city).toBe("New York");
  });

  it("returns all empty strings for empty array", () => {
    expect(parseAddressComponents([])).toEqual({ address: "", city: "", state: "", zip: "" });
  });
});

describe("useGooglePlacesAutocomplete", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    resetLoadState();
    clearGoogleMapsMock();
    document.head.querySelectorAll("script").forEach((s) => s.remove());
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
    clearGoogleMapsMock();
  });

  it("does not inject script tag when enabled=false", () => {
    renderHook(() => useGooglePlacesAutocomplete(defaultOptions({ enabled: false })));
    const scripts = document.head.querySelectorAll("script");
    expect(scripts.length).toBe(0);
  });

  it("returns { loaded: false } initially when enabled=false", () => {
    const { result } = renderHook(() =>
      useGooglePlacesAutocomplete(defaultOptions({ enabled: false })),
    );
    expect(result.current.loaded).toBe(false);
  });

  it("returns { loaded: true } after bootstrap + importLibrary resolves", async () => {
    setupGoogleMapsMock();
    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));

    await act(async () => {
      // Allow promises to resolve
    });

    expect(result.current.loaded).toBe(true);
  });

  it("calls fetchAutocompleteSuggestions with correct params", async () => {
    setupGoogleMapsMock();
    mockFetchAutocompleteSuggestions.mockResolvedValue({
      suggestions: [
        {
          placePrediction: {
            text: { toString: () => "123 Main St, Springfield, NJ 07081" },
            placeId: "abc123",
          },
        },
      ],
    });

    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));

    await act(async () => {});

    act(() => result.current.setQuery("123 Main"));

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    expect(mockFetchAutocompleteSuggestions).toHaveBeenCalledWith(
      expect.objectContaining({
        input: "123 Main",
        includedRegionCodes: ["us"],
        includedPrimaryTypes: ["address"],
        locationRestriction: expect.objectContaining({ north: 41.3574 }),
        sessionToken: expect.anything(),
      }),
    );
  });

  it("debounce: rapid typing fires only one request after 300ms", async () => {
    setupGoogleMapsMock();
    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));

    await act(async () => {});

    act(() => result.current.setQuery("123"));
    act(() => result.current.setQuery("123 M"));
    act(() => result.current.setQuery("123 Ma"));
    act(() => result.current.setQuery("123 Mai"));

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    expect(mockFetchAutocompleteSuggestions).toHaveBeenCalledTimes(1);
    expect(mockFetchAutocompleteSuggestions).toHaveBeenCalledWith(
      expect.objectContaining({ input: "123 Mai" }),
    );
  });

  it("does not fetch when query is < 3 characters", async () => {
    setupGoogleMapsMock();
    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));

    await act(async () => {});

    act(() => result.current.setQuery("12"));

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    expect(mockFetchAutocompleteSuggestions).not.toHaveBeenCalled();
  });

  it("returns suggestions array from API response", async () => {
    setupGoogleMapsMock();
    mockFetchAutocompleteSuggestions.mockResolvedValue({
      suggestions: [
        {
          placePrediction: {
            text: { toString: () => "123 Main St, Springfield, NJ" },
            placeId: "abc",
          },
        },
        {
          placePrediction: {
            text: { toString: () => "456 Oak Ave, Newark, NJ" },
            placeId: "def",
          },
        },
      ],
    });

    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    await act(async () => {});

    act(() => result.current.setQuery("123 Main"));

    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    expect(result.current.suggestions).toHaveLength(2);
    expect(result.current.suggestions[0].text).toBe("123 Main St, Springfield, NJ");
    expect(result.current.suggestions[1].text).toBe("456 Oak Ave, Newark, NJ");
  });

  it("selectSuggestion calls toPlace → fetchFields → onPlaceSelected", async () => {
    setupGoogleMapsMock();
    const onPlaceSelected = vi.fn();
    const addressComponents = [
      { longText: "789", shortText: "789", types: ["street_number"] },
      { longText: "Elm St", shortText: "Elm St", types: ["route"] },
      { longText: "Austin", shortText: "Austin", types: ["locality"] },
      { longText: "Texas", shortText: "TX", types: ["administrative_area_level_1"] },
      { longText: "73301", shortText: "73301", types: ["postal_code"] },
    ];
    const mockPlace = makeMockPlace(addressComponents);
    mockToPlace.mockReturnValue(mockPlace);
    mockFetchFields.mockResolvedValue(undefined);

    mockFetchAutocompleteSuggestions.mockResolvedValue({
      suggestions: [
        {
          placePrediction: {
            text: { toString: () => "789 Elm St, Austin, TX 73301" },
            placeId: "xyz",
            toPlace: mockToPlace,
          },
        },
      ],
    });

    const { result } = renderHook(() =>
      useGooglePlacesAutocomplete(defaultOptions({ onPlaceSelected })),
    );
    await act(async () => {});

    act(() => result.current.setQuery("789 Elm"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await act(async () => {
      await result.current.selectSuggestion(0);
    });

    expect(mockToPlace).toHaveBeenCalled();
    expect(mockFetchFields).toHaveBeenCalledWith({ fields: ["addressComponents"] });
    expect(onPlaceSelected).toHaveBeenCalledWith({
      address: "789 Elm St",
      city: "Austin",
      state: "TX",
      zip: "73301",
    });
  });

  it("selectSuggestion sets fetchError when fetchFields rejects", async () => {
    setupGoogleMapsMock();
    const mockPlace = makeMockPlace();
    mockToPlace.mockReturnValue(mockPlace);
    mockFetchFields.mockRejectedValue(new Error("Network error"));

    mockFetchAutocompleteSuggestions.mockResolvedValue({
      suggestions: [
        {
          placePrediction: {
            text: { toString: () => "123 Main St" },
            placeId: "abc",
            toPlace: mockToPlace,
          },
        },
      ],
    });

    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    await act(async () => {});

    act(() => result.current.setQuery("123 Main"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await act(async () => {
      await result.current.selectSuggestion(0);
    });

    expect(result.current.fetchError).toBe(
      "Could not load address details. Please enter your address manually.",
    );
  });

  it("fetchError clears when user starts typing again", async () => {
    setupGoogleMapsMock();
    const mockPlace = makeMockPlace();
    mockToPlace.mockReturnValue(mockPlace);
    mockFetchFields.mockRejectedValue(new Error("fail"));

    mockFetchAutocompleteSuggestions.mockResolvedValue({
      suggestions: [
        {
          placePrediction: {
            text: { toString: () => "123 Main St" },
            placeId: "abc",
            toPlace: mockToPlace,
          },
        },
      ],
    });

    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    await act(async () => {});

    act(() => result.current.setQuery("123 Main"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await act(async () => {
      await result.current.selectSuggestion(0);
    });

    expect(result.current.fetchError).not.toBeNull();

    act(() => result.current.setQuery("456 Oak"));

    expect(result.current.fetchError).toBeNull();
  });

  it("session token created on first fetch, refreshed after selection", async () => {
    setupGoogleMapsMock();
    const mockPlace = makeMockPlace([]);
    mockToPlace.mockReturnValue(mockPlace);
    mockFetchFields.mockResolvedValue(undefined);

    mockFetchAutocompleteSuggestions.mockResolvedValue({
      suggestions: [
        {
          placePrediction: {
            text: { toString: () => "123 Main St" },
            placeId: "abc",
            toPlace: mockToPlace,
          },
        },
      ],
    });

    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    await act(async () => {});

    // First fetch creates a token
    act(() => result.current.setQuery("123 Main"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    expect(mockSessionTokenConstructor).toHaveBeenCalledTimes(1);

    // Selection consumes token, next fetch creates a new one
    await act(async () => {
      await result.current.selectSuggestion(0);
    });

    act(() => result.current.setQuery("456 Oak"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    expect(mockSessionTokenConstructor).toHaveBeenCalledTimes(2);
  });

  it("unknown state code omits locationRestriction", async () => {
    setupGoogleMapsMock();
    const { result } = renderHook(() =>
      useGooglePlacesAutocomplete(defaultOptions({ stateCode: "ZZ" })),
    );

    await act(async () => {});

    act(() => result.current.setQuery("123 Main"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    const callArgs = mockFetchAutocompleteSuggestions.mock.calls[0][0];
    expect(callArgs.locationRestriction).toBeUndefined();
    expect(callArgs.includedRegionCodes).toEqual(["us"]);
  });

  it("empty suggestions response: returns empty array, no crash", async () => {
    setupGoogleMapsMock();
    mockFetchAutocompleteSuggestions.mockResolvedValue({ suggestions: [] });

    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    await act(async () => {});

    act(() => result.current.setQuery("zzz no results"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    expect(result.current.suggestions).toEqual([]);
  });

  it("bootstrap load failure degrades gracefully (loaded stays false)", async () => {
    // Don't set up google mock — script will fail
    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));

    // Simulate script load
    const script = document.head.querySelector("script");
    if (script) {
      await act(async () => {
        script.onerror!(new Event("error"));
      });
    }

    expect(result.current.loaded).toBe(false);
  });

  it("cancels setup when unmounted before SDK loads", async () => {
    // Don't set up google mock
    const { result, unmount } = renderHook(() =>
      useGooglePlacesAutocomplete(defaultOptions()),
    );

    unmount();

    // Now set up mock and simulate load
    setupGoogleMapsMock();
    const script = document.head.querySelector("script");
    if (script) {
      await act(async () => {
        (window as any).__googleMapsInitialized();
      });
    }

    // loaded should still be false since component unmounted
    expect(result.current.loaded).toBe(false);
  });

  it("clearSuggestions discards session token", async () => {
    setupGoogleMapsMock();
    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    await act(async () => {});

    // Trigger a fetch to create a session token
    act(() => result.current.setQuery("123 Main"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    expect(mockSessionTokenConstructor).toHaveBeenCalledTimes(1);

    // Clear suggestions (simulates blur)
    act(() => result.current.clearSuggestions());

    // Next fetch should create a new token
    act(() => result.current.setQuery("456 Oak"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    expect(mockSessionTokenConstructor).toHaveBeenCalledTimes(2);
  });

  it("resetLoadState isolates tests (no cross-test leakage)", () => {
    setupGoogleMapsMock();
    // After resetLoadState, a new render should attempt to load again
    resetLoadState();
    clearGoogleMapsMock();

    renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));

    // Should inject a new script since state was reset
    const scripts = document.head.querySelectorAll("script");
    expect(scripts.length).toBe(1);
  });

  it("does not fetch when apiKey is empty", () => {
    renderHook(() => useGooglePlacesAutocomplete(defaultOptions({ apiKey: "" })));
    const scripts = document.head.querySelectorAll("script");
    expect(scripts.length).toBe(0);
  });

  it("fetchAutocompleteSuggestions failure clears suggestions silently", async () => {
    setupGoogleMapsMock();
    mockFetchAutocompleteSuggestions.mockRejectedValue(new Error("Quota exceeded"));

    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    await act(async () => {});

    act(() => result.current.setQuery("123 Main"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    expect(result.current.suggestions).toEqual([]);
    // fetchError should NOT be set for suggestion fetch failures (only fetchFields)
    expect(result.current.fetchError).toBeNull();
  });

  it("script loads but importLibrary not available degrades gracefully", async () => {
    // Set up a partial mock: script loads but google.maps.importLibrary is missing
    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));

    const script = document.head.querySelector("script");
    expect(script).not.toBeNull();

    // Simulate callback WITHOUT setting up google.maps.importLibrary
    (window as any).google = { maps: {} };
    await act(async () => {
      (window as any).__googleMapsInitialized();
    });

    expect(result.current.loaded).toBe(false);
  });

  it("debounce cleanup cancels pending timer when query changes rapidly", async () => {
    setupGoogleMapsMock();
    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    await act(async () => {});

    // Type, then change query before debounce fires
    act(() => result.current.setQuery("123 Main"));

    // Advance partially (not enough for debounce to fire)
    act(() => {
      vi.advanceTimersByTime(100);
    });

    // Change query — should cancel previous timer
    act(() => result.current.setQuery("456 Oak"));

    // Advance the remaining 200ms from original timer — should NOT fire
    act(() => {
      vi.advanceTimersByTime(200);
    });

    // Only the new timer should fire after its full 300ms
    expect(mockFetchAutocompleteSuggestions).not.toHaveBeenCalled();

    await act(async () => {
      vi.advanceTimersByTime(100); // 300ms total for new query
    });

    expect(mockFetchAutocompleteSuggestions).toHaveBeenCalledTimes(1);
    expect(mockFetchAutocompleteSuggestions).toHaveBeenCalledWith(
      expect.objectContaining({ input: "456 Oak" }),
    );
  });

  it("selectSuggestion does nothing for invalid index", async () => {
    setupGoogleMapsMock();
    const onPlaceSelected = vi.fn();
    const { result } = renderHook(() =>
      useGooglePlacesAutocomplete(defaultOptions({ onPlaceSelected })),
    );
    await act(async () => {});

    // No suggestions loaded, trying to select index 0 should be a no-op
    await act(async () => {
      await result.current.selectSuggestion(0);
    });

    expect(onPlaceSelected).not.toHaveBeenCalled();
  });

  it("selectSuggestion handles place with no addressComponents", async () => {
    setupGoogleMapsMock();
    const onPlaceSelected = vi.fn();
    const mockPlace = makeMockPlace(undefined); // no addressComponents
    mockToPlace.mockReturnValue(mockPlace);
    mockFetchFields.mockResolvedValue(undefined);

    mockFetchAutocompleteSuggestions.mockResolvedValue({
      suggestions: [
        {
          placePrediction: {
            text: { toString: () => "Some Place" },
            placeId: "abc",
            toPlace: mockToPlace,
          },
        },
      ],
    });

    const { result } = renderHook(() =>
      useGooglePlacesAutocomplete(defaultOptions({ onPlaceSelected })),
    );
    await act(async () => {});

    act(() => result.current.setQuery("Some Place"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await act(async () => {
      await result.current.selectSuggestion(0);
    });

    // Should not call onPlaceSelected when addressComponents is undefined
    expect(onPlaceSelected).not.toHaveBeenCalled();
    // Should not set fetchError either — this is a valid but incomplete result
    expect(result.current.fetchError).toBeNull();
  });

  it("second hook instance reuses cached loadPromise (concurrent load)", async () => {
    // Do NOT set up google mock yet — both hooks will race to load
    const { result: result1 } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    const { result: result2 } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));

    // Now set up the mock and fire the script load
    setupGoogleMapsMock();
    const script = document.head.querySelector("script");
    expect(script).not.toBeNull();

    // Only one script tag should be injected (shared loadPromise)
    const scripts = document.head.querySelectorAll("script");
    expect(scripts.length).toBe(1);

    await act(async () => {
      (window as any).__googleMapsInitialized();
    });

    // Both hooks should end up loaded
    expect(result1.current.loaded).toBe(true);
    expect(result2.current.loaded).toBe(true);
  });

  it("second hook instance reuses cached placesLibrary after first fully loads", async () => {
    setupGoogleMapsMock();

    // First hook loads the library
    const { result: result1 } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    await act(async () => {});
    expect(result1.current.loaded).toBe(true);

    // Second hook (same apiKey, google.maps already set) — placesLibrary is cached
    const { result: result2 } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    await act(async () => {});

    expect(result2.current.loaded).toBe(true);
    // importLibrary called only once (second hook used cached placesLibrary)
    expect(mockImportLibrary).toHaveBeenCalledTimes(1);
  });

  it("setQuery with empty string does not clear fetchError", async () => {
    setupGoogleMapsMock();
    const mockPlace = makeMockPlace();
    mockToPlace.mockReturnValue(mockPlace);
    mockFetchFields.mockRejectedValue(new Error("fail"));

    mockFetchAutocompleteSuggestions.mockResolvedValue({
      suggestions: [
        {
          placePrediction: {
            text: { toString: () => "123 Main St" },
            placeId: "abc",
            toPlace: mockToPlace,
          },
        },
      ],
    });

    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    await act(async () => {});

    act(() => result.current.setQuery("123 Main"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await act(async () => {
      await result.current.selectSuggestion(0);
    });

    // fetchError is set
    expect(result.current.fetchError).not.toBeNull();

    // Setting query to empty string should NOT clear the error
    act(() => result.current.setQuery(""));
    expect(result.current.fetchError).not.toBeNull();
  });

  it("reuses existing session token on second fetch without clearSuggestions", async () => {
    setupGoogleMapsMock();
    mockFetchAutocompleteSuggestions.mockResolvedValue({ suggestions: [] });

    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    await act(async () => {});

    // First fetch creates a token
    act(() => result.current.setQuery("123 Main"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });
    expect(mockSessionTokenConstructor).toHaveBeenCalledTimes(1);

    // Second fetch reuses the same token (no clearSuggestions in between)
    act(() => result.current.setQuery("123 Main St"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    // Token constructor called only once — reused on second fetch
    expect(mockSessionTokenConstructor).toHaveBeenCalledTimes(1);
    expect(mockFetchAutocompleteSuggestions).toHaveBeenCalledTimes(2);
  });

  it("fetchAutocompleteSuggestions response with undefined suggestions falls back to empty array", async () => {
    setupGoogleMapsMock();
    // Return a response where suggestions is undefined (missing key)
    mockFetchAutocompleteSuggestions.mockResolvedValue({});

    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    await act(async () => {});

    act(() => result.current.setQuery("123 Main"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    expect(result.current.suggestions).toEqual([]);
  });

  it("maps suggestion with undefined text and placeId to empty strings", async () => {
    setupGoogleMapsMock();
    mockFetchAutocompleteSuggestions.mockResolvedValue({
      suggestions: [
        {
          placePrediction: {
            // text and placeId intentionally omitted
            toPlace: mockToPlace,
          },
        },
      ],
    });

    const { result } = renderHook(() => useGooglePlacesAutocomplete(defaultOptions()));
    await act(async () => {});

    act(() => result.current.setQuery("123 Main"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    expect(result.current.suggestions).toHaveLength(1);
    expect(result.current.suggestions[0].text).toBe("");
    expect(result.current.suggestions[0].placeId).toBe("");
  });

  it("selectSuggestion with undefined prediction.text falls back to empty string for query", async () => {
    setupGoogleMapsMock();
    const onPlaceSelected = vi.fn();
    const mockPlace = makeMockPlace([]);
    mockToPlace.mockReturnValue(mockPlace);
    mockFetchFields.mockResolvedValue(undefined);

    mockFetchAutocompleteSuggestions.mockResolvedValue({
      suggestions: [
        {
          placePrediction: {
            // text intentionally omitted — falls back to "" via ?? ""
            placeId: "abc",
            toPlace: mockToPlace,
          },
        },
      ],
    });

    const { result } = renderHook(() =>
      useGooglePlacesAutocomplete(defaultOptions({ onPlaceSelected })),
    );
    await act(async () => {});

    act(() => result.current.setQuery("123 Main"));
    await act(async () => {
      vi.advanceTimersByTime(300);
    });

    await act(async () => {
      await result.current.selectSuggestion(0);
    });

    // query should be set to "" (the fallback)
    expect(result.current.query).toBe("");
  });
});
