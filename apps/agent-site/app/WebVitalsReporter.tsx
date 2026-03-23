"use client";

import { useEffect } from "react";
import { initWebVitals } from "@real-estate-star/analytics";

export function WebVitalsReporter() {
  useEffect(() => {
    const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "";
    initWebVitals(apiUrl, "");
  }, []);

  return null;
}
