/**
 * Fires a conversion event to Google Tag Manager's gtag dataLayer.
 * No-ops in SSR or when gtag is not loaded.
 */
export function trackConversion(label: string): void {
  if (typeof window !== "undefined" && (window as any).gtag) {
    (window as any).gtag("event", "conversion", { send_to: label });
  }
}
