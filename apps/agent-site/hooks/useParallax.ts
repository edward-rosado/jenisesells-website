"use client";

import { useEffect, type RefObject } from "react";
import { useReducedMotion } from "./useReducedMotion";

interface ParallaxOptions {
  maxScale?: number;
  maxTranslateY?: number;
}

export function useParallax(
  ref: RefObject<HTMLElement | null>,
  bgRef: RefObject<HTMLElement | null>,
  options?: ParallaxOptions,
): void {
  const { maxScale = 1.25, maxTranslateY = -40 } = options ?? {};
  const reducedMotion = useReducedMotion();

  useEffect(() => {
    if (reducedMotion) return;

    const el = ref.current;
    const bg = bgRef.current;
    if (!el || !bg) return;

    let ticking = false;

    function onScroll() {
      if (ticking) return;
      ticking = true;
      requestAnimationFrame(() => {
        const rect = el!.getBoundingClientRect();
        // Progress: 0 when element top is at viewport top, 1 when element is fully scrolled out
        const progress = Math.max(0, Math.min(1, -rect.top / rect.height));
        const scale = 1 + progress * (maxScale - 1);
        const translateY = progress * maxTranslateY;
        bg!.style.transform = `scale(${scale}) translateY(${translateY}px)`;
        ticking = false;
      });
    }

    window.addEventListener("scroll", onScroll, { passive: true });
    onScroll();
    return () => window.removeEventListener("scroll", onScroll);
  }, [ref, bgRef, maxScale, maxTranslateY, reducedMotion]);
}
