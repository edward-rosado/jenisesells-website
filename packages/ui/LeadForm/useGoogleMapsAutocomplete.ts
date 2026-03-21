"use client";

import { useEffect, useState, type RefObject } from "react";

export interface PlaceResult {
  address: string;
  city: string;
  state: string;
  zip: string;
}

interface AddressComponent {
  long_name: string;
  short_name: string;
  types: string[];
}

export interface UseGoogleMapsAutocompleteOptions {
  apiKey: string;
  inputRef: RefObject<HTMLInputElement | null>;
  onPlaceSelected: (place: PlaceResult) => void;
  enabled: boolean;
}

export interface UseGoogleMapsAutocompleteReturn {
  loaded: boolean;
}

// Module-level promise for script deduplication (client-only)
let loadPromise: Promise<void> | null = null;

/** Reset the module-level dedup promise — for test isolation only. */
export function resetLoadPromise(): void {
  loadPromise = null;
}

function loadGoogleMapsScript(apiKey: string): Promise<void> {
  if (loadPromise) return loadPromise;
  if (typeof window !== "undefined" && (window as any).google?.maps?.places) {
    return Promise.resolve();
  }
  loadPromise = new Promise<void>((resolve, reject) => {
    const script = document.createElement("script");
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&libraries=places`;
    script.async = true;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error("Failed to load Google Maps SDK"));
    document.head.appendChild(script);
  });
  return loadPromise;
}

export function parseAddressComponents(components: AddressComponent[]): PlaceResult {
  let address = "";
  let city = "";
  let state = "";
  let zip = "";

  for (const comp of components) {
    const types = comp.types;
    if (types.includes("street_number")) {
      address = comp.long_name + " " + address;
    } else if (types.includes("route")) {
      address = address + comp.long_name;
    } else if (types.includes("locality")) {
      city = comp.long_name;
    } else if (types.includes("sublocality_level_1") && !city) {
      city = comp.long_name;
    } else if (types.includes("administrative_area_level_1")) {
      state = comp.short_name;
    } else if (types.includes("postal_code")) {
      zip = comp.long_name;
    }
  }

  return { address: address.trim(), city, state, zip };
}

export function useGoogleMapsAutocomplete({
  apiKey,
  inputRef,
  onPlaceSelected,
  enabled,
}: UseGoogleMapsAutocompleteOptions): UseGoogleMapsAutocompleteReturn {
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    if (!enabled || !apiKey || typeof window === "undefined") return;

    let listener: any = null;
    let autocompleteInstance: any = null;
    let cancelled = false;

    // Google calls window.gm_authFailure when API key is rejected (wrong domain, expired, etc.)
    const prevAuthFailure = (window as any).gm_authFailure;
    (window as any).gm_authFailure = () => {
      if (!cancelled) {
        setLoaded(false);
        // Detach the autocomplete widget so it stops intercepting the input
        if (autocompleteInstance) {
          autocompleteInstance.unbindAll?.();
          autocompleteInstance = null;
        }
        if (listener && (window as any).google?.maps?.event) {
          (window as any).google.maps.event.removeListener(listener);
          listener = null;
        }
        // Remove the Google-injected dropdown overlay
        document.querySelectorAll(".pac-container").forEach((el) => el.remove());
        // Strip Google-added attributes from the input
        if (inputRef.current) {
          inputRef.current.removeAttribute("autocomplete");
          inputRef.current.removeAttribute("role");
          inputRef.current.removeAttribute("aria-autocomplete");
          inputRef.current.removeAttribute("aria-haspopup");
        }
      }
      prevAuthFailure?.();
    };

    loadGoogleMapsScript(apiKey)
      .then(() => {
        if (cancelled) return;
        if (!inputRef.current) return;
        autocompleteInstance = new (window as any).google.maps.places.Autocomplete(
          inputRef.current,
          {
            componentRestrictions: { country: "us" },
            fields: ["address_components"],
            types: ["address"],
          },
        );
        listener = autocompleteInstance.addListener("place_changed", () => {
          const place = autocompleteInstance.getPlace();
          if (place.address_components) {
            onPlaceSelected(parseAddressComponents(place.address_components));
          }
        });
        setLoaded(true);
      })
      .catch(() => {
        // SDK load failed — form still works, just no autocomplete
      });

    return () => {
      cancelled = true;
      (window as any).gm_authFailure = prevAuthFailure;
      if (listener && (window as any).google?.maps?.event) {
        (window as any).google.maps.event.removeListener(listener);
      }
    };
  }, [enabled, apiKey, inputRef, onPlaceSelected]);

  return { loaded };
}
