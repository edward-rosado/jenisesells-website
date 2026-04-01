"use client";

import { useEffect } from "react";

/**
 * Reads the `locale` cookie (set by middleware) and updates `<html lang>` accordingly.
 * Root layouts in Next.js App Router cannot access searchParams, so this client component
 * bridges the gap to set the correct lang attribute for accessibility and SEO.
 */
export function HtmlLangSetter() {
  useEffect(() => {
    const match = document.cookie.match(/(?:^|;\s*)locale=([a-z]{2}(?:-[a-z]{2})?)/i);
    if (match) {
      document.documentElement.lang = match[1];
    }
  }, []);

  return null;
}
