"use client";

import { useEffect, useState, useRef, useCallback } from "react";
import { getStateBounds } from "./stateBounds";

// ── Public types ──────────────────────────────────────────────────────

export interface PlaceResult {
  address: string;
  city: string;
  state: string;
  zip: string;
}

export interface PlaceSuggestion {
  text: string;
  placeId: string;
  index: number;
}

export interface UseGooglePlacesAutocompleteOptions {
  apiKey: string;
  enabled: boolean;
  stateCode: string;
  onPlaceSelected: (place: PlaceResult) => void;
}

export interface UseGooglePlacesAutocompleteReturn {
  loaded: boolean;
  suggestions: PlaceSuggestion[];
  query: string;
  setQuery: (value: string) => void;
  selectSuggestion: (index: number) => Promise<void>;
  clearSuggestions: () => void;
  highlightedIndex: number;
  setHighlightedIndex: (index: number) => void;
  fetchError: string | null;
}

// ── Internal types for Google Maps API ────────────────────────────────

interface AddressComponent {
  longText: string;
  shortText: string;
  types: string[];
}

// ── Module-level state ────────────────────────────────────────────────

let loadPromise: Promise<void> | null = null;
let placesLibrary: any = null;

/** Reset module-level state — for test isolation only. */
export function resetLoadState(): void {
  loadPromise = null;
  placesLibrary = null;
}

// ── Bootstrap loader ──────────────────────────────────────────────────

function loadGoogleMapsBootstrap(apiKey: string): Promise<void> {
  if (loadPromise) {
    console.debug(LOG_PREFIX, "Bootstrap already loading, reusing promise");
    return loadPromise;
  }
  /* v8 ignore next -- SSR guard; tests run in jsdom which always has window */
  if (typeof window === "undefined") return Promise.reject(new Error("No window"));
  if ((window as any).google?.maps?.importLibrary) {
    console.debug(LOG_PREFIX, "Google Maps already loaded, skipping bootstrap");
    return Promise.resolve();
  }

  console.info(LOG_PREFIX, "Loading Google Maps bootstrap script");
  loadPromise = new Promise<void>((resolve, reject) => {
    // With loading=async, importLibrary may not be available at onload.
    // Use the callback parameter to know when the API is fully initialized.
    const callbackName = "__googleMapsInitialized";
    (window as any)[callbackName] = () => {
      delete (window as any)[callbackName];
      if ((window as any).google?.maps?.importLibrary) {
        console.info(LOG_PREFIX, "Bootstrap loaded successfully");
        resolve();
      } else {
        console.error(LOG_PREFIX, "Bootstrap loaded but importLibrary not available");
        reject(new Error("Google Maps loaded but importLibrary not available"));
      }
    };
    const script = document.createElement("script");
    script.async = true;
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&loading=async&libraries=places&callback=${callbackName}`;
    script.onerror = () => {
      delete (window as any)[callbackName];
      console.error(LOG_PREFIX, "Failed to load bootstrap script");
      loadPromise = null;
      reject(new Error("Failed to load Google Maps SDK"));
    };
    document.head.appendChild(script);
  });

  return loadPromise;
}

async function loadPlacesLibrary(apiKey: string): Promise<any> {
  if (placesLibrary) {
    console.debug(LOG_PREFIX, "Places library already cached, reusing");
    return placesLibrary;
  }
  await loadGoogleMapsBootstrap(apiKey);
  console.info(LOG_PREFIX, "Importing Places library via importLibrary()");
  placesLibrary = await (window as any).google.maps.importLibrary("places");
  console.info(LOG_PREFIX, "Places library loaded successfully");
  return placesLibrary;
}

// ── Address parsing ───────────────────────────────────────────────────

export function parseAddressComponents(components: AddressComponent[]): PlaceResult {
  let address = "";
  let city = "";
  let state = "";
  let zip = "";

  for (const comp of components) {
    const types = comp.types;
    if (types.includes("street_number")) {
      address = comp.longText + " " + address;
    } else if (types.includes("route")) {
      address = address + comp.longText;
    } else if (types.includes("locality")) {
      city = comp.longText;
    } else if (types.includes("sublocality_level_1") && !city) {
      city = comp.longText;
    } else if (types.includes("administrative_area_level_1")) {
      state = comp.shortText;
    } else if (types.includes("postal_code")) {
      zip = comp.longText;
    }
  }

  return { address: address.trim(), city, state, zip };
}

// ── Constants ─────────────────────────────────────────────────────────

const DEBOUNCE_MS = 300;
const MIN_QUERY_LENGTH = 3;
const LOG_PREFIX = "[GooglePlaces]";

// ── Billing tracker ───────────────────────────────────────────────────
// Tracks billable API calls in-memory for the current page session.
// View anytime in DevTools: window.__googlePlacesUsage()

interface UsageCounters {
  sessions: number;
  suggestionRequests: number;
  placeDetailRequests: number;
  errors: number;
}

const usage: UsageCounters = { sessions: 0, suggestionRequests: 0, placeDetailRequests: 0, errors: 0 };

function logBillable(event: string, detail: string) {
  console.info(LOG_PREFIX, `💲 BILLABLE: ${event} — ${detail}`, `| totals: ${usage.sessions} sessions, ${usage.suggestionRequests} suggestion reqs, ${usage.placeDetailRequests} detail reqs`);
}

/* v8 ignore next 5 -- browser-only debug helper */
if (typeof window !== "undefined") {
  (window as any).__googlePlacesUsage = () => {
    console.table(usage);
    return usage;
  };
}

// ── Hook ──────────────────────────────────────────────────────────────

export function useGooglePlacesAutocomplete({
  apiKey,
  enabled,
  stateCode,
  onPlaceSelected,
}: UseGooglePlacesAutocompleteOptions): UseGooglePlacesAutocompleteReturn {
  const [loaded, setLoaded] = useState(false);
  const [suggestions, setSuggestions] = useState<PlaceSuggestion[]>([]);
  const [query, setQueryState] = useState("");
  const [highlightedIndex, setHighlightedIndex] = useState(-1);
  const [fetchError, setFetchError] = useState<string | null>(null);

  const sessionTokenRef = useRef<any>(null);
  const debounceTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const cancelledRef = useRef(false);
  const placesRef = useRef<any>(null);
  const suggestionsDataRef = useRef<any[]>([]);
  const onPlaceSelectedRef = useRef(onPlaceSelected);
  onPlaceSelectedRef.current = onPlaceSelected;

  // Load the Places library
  useEffect(() => {
    if (!enabled || !apiKey || typeof window === "undefined") {
      console.debug(LOG_PREFIX, "Skipping load — enabled:", enabled, "apiKey:", !!apiKey);
      return;
    }
    cancelledRef.current = false;

    console.info(LOG_PREFIX, "Initializing Places autocomplete for state:", stateCode);
    loadPlacesLibrary(apiKey)
      .then((lib) => {
        if (cancelledRef.current) {
          console.debug(LOG_PREFIX, "Load completed but hook was unmounted, ignoring");
          return;
        }
        placesRef.current = lib;
        setLoaded(true);
        console.info(LOG_PREFIX, "Ready — autocomplete active for state:", stateCode);
      })
      .catch((err) => {
        console.error(LOG_PREFIX, "SDK load failed — autocomplete disabled:", err?.message);
      });

    return () => {
      cancelledRef.current = true;
    };
  }, [enabled, apiKey, stateCode]);

  // Fetch suggestions when query changes (debounced)
  useEffect(() => {
    /* v8 ignore next 3 -- belt-and-suspenders: React cleanup already clears the timer before re-running */
    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
      debounceTimerRef.current = null;
    }

    if (!loaded || !query || query.length < MIN_QUERY_LENGTH) {
      setSuggestions([]);
      suggestionsDataRef.current = [];
      return;
    }

    debounceTimerRef.current = setTimeout(async () => {
      const lib = placesRef.current;
      /* v8 ignore next -- lib is always set when loaded=true; defensive guard */
      if (!lib) return;

      // Create session token lazily
      if (!sessionTokenRef.current) {
        sessionTokenRef.current = new lib.AutocompleteSessionToken();
        usage.sessions++;
        logBillable("SESSION_START", `new autocomplete session #${usage.sessions}`);
      }

      const request: any = {
        input: query,
        includedRegionCodes: ["us"],
        includedPrimaryTypes: ["address"],
        sessionToken: sessionTokenRef.current,
      };

      const bounds = getStateBounds(stateCode);
      if (bounds) {
        request.locationRestriction = bounds;
        console.debug(LOG_PREFIX, "Fetching suggestions — query:", JSON.stringify(query), "state:", stateCode, "(bounded)");
      } else {
        console.debug(LOG_PREFIX, "Fetching suggestions — query:", JSON.stringify(query), "state:", stateCode, "(no bounds, US-wide)");
      }

      try {
        usage.suggestionRequests++;
        logBillable("SUGGESTION_FETCH", `query=${JSON.stringify(query)} state=${stateCode} (included in session pricing)`);
        const response = await lib.AutocompleteSuggestion.fetchAutocompleteSuggestions(request);
        const items = response.suggestions ?? [];
        suggestionsDataRef.current = items;
        const mapped = items.map((s: any, i: number) => ({
          text: s.placePrediction?.text?.toString() ?? "",
          placeId: s.placePrediction?.placeId ?? "",
          index: i,
        }));
        setSuggestions(mapped);
        setHighlightedIndex(-1);
        console.debug(LOG_PREFIX, "Received", items.length, "suggestions for", JSON.stringify(query));
      } catch (err) {
        usage.errors++;
        /* v8 ignore next -- logging branch: err shape varies */
        console.warn(LOG_PREFIX, "SUGGESTION_FETCH_FAILED:", (err as Error)?.message ?? err);
        setSuggestions([]);
        suggestionsDataRef.current = [];
      }
    }, DEBOUNCE_MS);

    return () => {
      /* v8 ignore next -- debounceTimerRef is always set when this cleanup path is reached */
      if (debounceTimerRef.current) {
        clearTimeout(debounceTimerRef.current);
        debounceTimerRef.current = null;
      }
    };
  }, [loaded, query, stateCode]);

  const setQuery = useCallback((value: string) => {
    setQueryState(value);
    if (value) {
      setFetchError(null);
    }
  }, []);

  const clearSuggestions = useCallback(() => {
    setSuggestions([]);
    suggestionsDataRef.current = [];
    setHighlightedIndex(-1);
    // Discard session token on blur without selection
    /* v8 ignore next 3 -- logging-only guard: no behavioral branch */
    if (sessionTokenRef.current) {
      console.debug(LOG_PREFIX, "SESSION_ABANDONED — user blurred without selecting (no PLACE_DETAIL charge, only SUGGESTION_FETCH charges apply)");
    }
    sessionTokenRef.current = null;
  }, []);

  const selectSuggestion = useCallback(async (index: number) => {
    const item = suggestionsDataRef.current[index];
    if (!item?.placePrediction) return;

    const prediction = item.placePrediction;
    const selectedText = prediction.text?.toString() ?? "";
    console.info(LOG_PREFIX, "Selection — index:", index, "text:", JSON.stringify(selectedText));

    setQueryState(selectedText);
    setSuggestions([]);
    suggestionsDataRef.current = [];
    setHighlightedIndex(-1);

    try {
      const place = prediction.toPlace();
      usage.placeDetailRequests++;
      logBillable("PLACE_DETAIL", `fetchFields(addressComponents) for ${JSON.stringify(selectedText)} — ends session`);
      await place.fetchFields({ fields: ["addressComponents"] });
      const components = place.addressComponents;
      if (components) {
        const parsed = parseAddressComponents(components);
        console.info(LOG_PREFIX, "Parsed address:", JSON.stringify(parsed));
        onPlaceSelectedRef.current(parsed);
      } else {
        console.warn(LOG_PREFIX, "PLACE_DETAIL returned no addressComponents");
      }
    } catch (err) {
      usage.errors++;
      /* v8 ignore next -- logging branch: err shape varies */
      console.error(LOG_PREFIX, "PLACE_DETAIL_FAILED:", (err as Error)?.message ?? err);
      setFetchError("Could not load address details. Please enter your address manually.");
    }

    // Session token is consumed by fetchFields (success or failure) — refresh
    console.debug(LOG_PREFIX, "SESSION_END — token consumed, ready for next session");
    sessionTokenRef.current = null;
  }, []);

  return {
    loaded,
    suggestions,
    query,
    setQuery,
    selectSuggestion,
    clearSuggestions,
    highlightedIndex,
    setHighlightedIndex,
    fetchError,
  };
}
