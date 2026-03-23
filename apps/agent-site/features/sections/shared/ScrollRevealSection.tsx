"use client";

import { useRef, type ReactNode } from "react";
import { useScrollReveal } from "@/features/shared/useScrollReveal";

interface ScrollRevealSectionProps {
  children: ReactNode;
  delay?: number;
}

export function ScrollRevealSection({ children, delay = 0 }: ScrollRevealSectionProps) {
  const ref = useRef<HTMLDivElement>(null);
  const isVisible = useScrollReveal(ref);

  return (
    <div
      ref={ref}
      style={{
        opacity: isVisible ? 1 : 0,
        transform: isVisible ? "translateY(0)" : "translateY(24px)",
        transition: `opacity 0.6s ease ${delay}ms, transform 0.6s ease ${delay}ms`,
      }}
    >
      {children}
    </div>
  );
}
