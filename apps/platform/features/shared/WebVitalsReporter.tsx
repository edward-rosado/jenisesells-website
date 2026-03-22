"use client";

import { useEffect } from "react";
import { initWebVitals } from "@real-estate-star/analytics";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5135";

/**
 * Client component that initialises Core Web Vitals collection.
 * Must be rendered inside a Client boundary (layout is a Server Component).
 * Renders nothing visible — side-effects only.
 */
export function WebVitalsReporter() {
  useEffect(() => {
    initWebVitals(API_URL, "platform");
  }, []);

  return null;
}
