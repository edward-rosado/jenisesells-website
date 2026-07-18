"use client";

import { useState, useEffect, type RefObject } from "react";
import { useReducedMotion } from "./useReducedMotion";

interface ScrollRevealOptions {
  /** Fraction of the element that must be visible to trigger the reveal. */
  threshold?: number;
  once?: boolean;
}

export function useScrollReveal(
  ref: RefObject<HTMLElement | null>,
  options?: ScrollRevealOptions,
): boolean {
  const { threshold = 0.15, once = true } = options ?? {};
  const reducedMotion = useReducedMotion();
  const [isVisible, setIsVisible] = useState(reducedMotion);

  useEffect(() => {
    if (reducedMotion) return;

    const el = ref.current;
    if (!el) return;

    // Reveal-on-scroll fires when the element first crosses into the viewport.
    // A percentage `threshold` is unreliable here: a section taller than
    // (viewport / threshold) — e.g. a 9000px team grid — can never have 15% of
    // itself on screen at once, so it would stay at opacity 0 forever. Section
    // height also grows after mount (images, fonts) while the observer is only
    // created once, so a height-based threshold clamp computed on mount is
    // stale.
    //
    // `reveal()` checks the element's geometry directly: it reveals once the
    // top edge has scrolled `threshold × viewport` into the viewport, OR once
    // the element is already partly/fully above the fold. The second condition
    // is essential — IntersectionObserver only fires on intersection *changes*,
    // so an element scrolled fully past before the observer samples it (fast
    // scroll, deep-link jump, restored scroll position) would never reveal.
    const viewportH = typeof window !== "undefined" ? window.innerHeight : 800;
    const revealMargin = viewportH * Math.min(threshold, 0.5);

    let revealed = false;
    const reveal = () => {
      if (revealed) return;
      const rect = el.getBoundingClientRect();
      // top edge scrolled far enough in, or element extends above viewport top
      if (rect.top <= viewportH - revealMargin) {
        revealed = true;
        setIsVisible(true);
        return true;
      }
      return false;
    };

    if (reveal()) return;

    const observer = new IntersectionObserver(
      () => {
        if (reveal() && once) observer.disconnect();
      },
      { threshold: 0, rootMargin: `0px 0px ${-Math.round(revealMargin)}px 0px` },
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, [ref, threshold, once, reducedMotion]);

  return isVisible;
}
