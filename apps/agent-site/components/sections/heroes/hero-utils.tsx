import type { ReactNode } from "react";

export function safeHref(href: string): string {
  if (href.startsWith("#") || href.startsWith("/")) return href;
  try {
    const url = new URL(href);
    if (url.protocol === "https:" || url.protocol === "http:") return href;
  } catch { /* invalid URL */ }
  return "#";
}

export function renderHeadline(headline: string, highlightWord?: string): ReactNode {
  if (!highlightWord) return headline;
  const idx = headline.lastIndexOf(highlightWord);
  if (idx === -1) return headline;
  return (
    <>
      {headline.slice(0, idx)}
      <span style={{ color: "var(--color-accent)" }}>{highlightWord}</span>
      {headline.slice(idx + highlightWord.length)}
    </>
  );
}
