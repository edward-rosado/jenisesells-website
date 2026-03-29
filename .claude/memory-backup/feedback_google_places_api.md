---
name: google-places-data-api-requirements
description: Complete requirements for the Google Places Data API (AutocompleteSuggestion) to work on agent-site — CSP, script loading, domains, and billing
type: feedback
---

The Google Places Data API (replacing the deprecated google.maps.places.Autocomplete) has specific requirements that have caused multiple production incidents. Follow ALL of these:

**Why:** Three separate deploys were needed to get this working. Each time, a different requirement was missed.

**How to apply — EVERY requirement must be met:**

### 1. Script Loading — NO `loading=async`
The Google Maps script must use the `callback` parameter, NOT `loading=async`. They are incompatible.
```
CORRECT:  ...api/js?key={key}&libraries=places&callback=__googleMapsInitialized
WRONG:    ...api/js?key={key}&loading=async&libraries=places&callback=__googleMapsInitialized
WRONG:    ...api/js?key={key}&loading=async&libraries=places  (no callback — importLibrary not ready at onload)
```
File: `packages/forms/src/LeadForm/useGooglePlacesAutocomplete.ts`

### 2. CSP — TWO Google domains required
The new Places Data API uses `places.googleapis.com` (NOT `maps.googleapis.com`) for `fetchAutocompleteSuggestions`. Both domains must be in `connect-src`:
```
connect-src ... https://maps.googleapis.com https://places.googleapis.com https://maps.gstatic.com ...
```
File: `apps/agent-site/middleware.ts` line 15

### 3. Google Cloud Console — API key restrictions
The API key must have "Places API (New)" enabled. Restrict to:
- HTTP referrers: `*.real-estate-star.com/*`, `localhost:*`
- API restriction: Places API (New) only

### 4. Billing — Session tokens
`AutocompleteSessionToken` groups suggestion requests + the final `fetchFields` into one billing session (~$0.017). Without tokens, each suggestion request is billed individually (~$0.003/each).
Billing logging: `[GooglePlaces] 💲 BILLABLE:` prefix in console. Check `window.__googlePlacesUsage()`.

### 5. Place types — NOT the same as legacy API
The new API uses different type names. `"address"` is INVALID. Use `"street_address"`.
```
CORRECT:  includedPrimaryTypes: ["street_address"]
WRONG:    includedPrimaryTypes: ["address"]  ← legacy API only, returns 400 Bad Request
```
See: https://developers.google.com/maps/documentation/places/web-service/place-types

### 6. Google Maps API Key — HTTP Referrer Restrictions (Google Cloud Console)
The API key MUST allow ALL domains where the site is served. Without this, `fetchAutocompleteSuggestions` returns 403 Forbidden.

**Current required referrers** (set in Google Cloud Console → APIs & Services → Credentials → the Maps key):
- `*.real-estate-star.com/*` — production agent sites
- `*.workers.dev/*` — Cloudflare Workers preview deploys
- `localhost:*` — local development

**This is NOT a code change.** It's a manual setting in Google Cloud Console. Claude cannot change it.
If previews show `403 (Forbidden)` from `places.googleapis.com` and the console shows `Requests from referrer ... are blocked`, the referrer is not in the Google Cloud Console allowlist.

### 7. AddressAutocomplete dropdown styling
The seller card in LeadForm has `overflow: hidden` for collapse animation. This CLIPS the dropdown. Fix: `overflow: isSelling ? "visible" : "hidden"` on the seller card div.
- Dropdown uses `position: absolute` with `top: 100%`, `left: 0`, `right: 0` inside a `position: relative` container
- `maxHeight: 200` with `overflowY: auto` for scrolling
- "Powered by Google" is a separate div below the `<ul>`, outside the scroll area
- Items use `listStyle: none`, `padding: 0`, `margin: 0` on the `<ul>`

### 8. State bounding box filtering
`locationRestriction` filters suggestions to the agent's state. If state not in `STATE_BOUNDING_BOXES`, falls back to US-wide.
File: `packages/forms/src/LeadForm/stateBounds.ts`

### 6. Turnstile + CORS for preview deploys
- Turnstile widget allowed hostnames: must include `misteredr.workers.dev` (Cloudflare dashboard)
- CORS: must include `*.workers.dev` in `SetIsOriginAllowed` (Program.cs)
- See also: `feedback_turnstile_csp_cors.md`
