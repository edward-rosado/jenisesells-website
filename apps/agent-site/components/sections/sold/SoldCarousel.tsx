"use client";

import { useState, useEffect, useRef, useCallback, useSyncExternalStore } from "react";
import Image from "next/image";
import type { SoldHomesProps } from "@/components/sections/types";

export function SoldCarousel({ items, title, subtitle }: SoldHomesProps) {
  const [current, setCurrent] = useState(0);
  const [paused, setPaused] = useState(false);
  const reducedMotion = useSyncExternalStore(
    (cb) => {
      const mq = window.matchMedia("(prefers-reduced-motion: reduce)");
      mq.addEventListener("change", cb);
      return () => mq.removeEventListener("change", cb);
    },
    () => window.matchMedia("(prefers-reduced-motion: reduce)").matches,
    () => false,
  );
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const trackRef = useRef<HTMLDivElement>(null);

  const goTo = useCallback((index: number) => {
    setCurrent(((index % items.length) + items.length) % items.length);
  }, [items.length]);

  const goNext = useCallback(() => goTo(current + 1), [current, goTo]);
  const goPrev = useCallback(() => goTo(current - 1), [current, goTo]);

  // Scroll track to current slide
  useEffect(() => {
    const track = trackRef.current;
    if (!track) return;
    const slide = track.children[current] as HTMLElement | undefined;
    if (slide && typeof track.scrollTo === "function") {
      track.scrollTo({ left: slide.offsetLeft, behavior: "smooth" });
    }
  }, [current]);

  // Auto-advance every 5s, pause on hover/focus
  useEffect(() => {
    if (paused || reducedMotion || items.length <= 1) return;
    intervalRef.current = setInterval(goNext, 5000);
    return () => { clearInterval(intervalRef.current!); };
  }, [paused, reducedMotion, goNext, items.length]);

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowRight") goNext();
    if (e.key === "ArrowLeft") goPrev();
  };

  if (reducedMotion) {
    // Fallback: vertical stack for reduced motion
    return (
      <section
        id="sold"
        style={{ background: "var(--color-primary, #0a0a0a)", padding: "80px 40px" }}
      >
        <div style={{ maxWidth: "900px", margin: "0 auto" }}>
          <h2
            style={{
              textAlign: "center",
              fontSize: "34px",
              fontWeight: 300,
              color: "white",
              marginBottom: subtitle ? "10px" : "50px",
              fontFamily: "var(--font-family, Georgia), serif",
              letterSpacing: "1px",
            }}
          >
            {title ?? "Portfolio"}
          </h2>
          {subtitle && (
            <p style={{ textAlign: "center", color: "rgba(255,255,255,0.6)", fontSize: "16px", marginBottom: "50px" }}>
              {subtitle}
            </p>
          )}
          <div style={{ display: "flex", flexDirection: "column", gap: "24px" }}>
            {items.map((item) => (
              <article
                key={`${item.address}-${item.city}`}
                style={{ background: "rgba(255,255,255,0.05)", padding: "20px" }}
              >
                <span style={{ background: "var(--color-accent, #d4af37)", color: "#0a0a0a", padding: "3px 10px", fontSize: "11px", fontWeight: 700 }}>SOLD</span>
                <div style={{ color: "white", fontSize: "22px", fontWeight: 300, marginTop: "10px" }}>{item.price}</div>
                <div style={{ color: "rgba(255,255,255,0.6)", fontSize: "13px" }}>{item.address}, {item.city}, {item.state}</div>
              </article>
            ))}
          </div>
        </div>
      </section>
    );
  }

  return (
    <section
      id="sold"
      style={{ background: "var(--color-primary, #0a0a0a)", padding: "80px 0" }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto", paddingLeft: "40px", paddingRight: "40px" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "34px",
            fontWeight: 300,
            color: "white",
            marginBottom: subtitle ? "10px" : "50px",
            fontFamily: "var(--font-family, Georgia), serif",
            letterSpacing: "1px",
          }}
        >
          {title ?? "Portfolio"}
        </h2>
        {subtitle && (
          <p style={{ textAlign: "center", color: "rgba(255,255,255,0.6)", fontSize: "16px", marginBottom: "50px" }}>
            {subtitle}
          </p>
        )}
      </div>

      {/* Carousel region */}
      <div
        role="region"
        aria-roledescription="carousel"
        aria-label={title ?? "Portfolio"}
        tabIndex={0}
        onKeyDown={handleKeyDown}
        onMouseEnter={() => setPaused(true)}
        onMouseLeave={() => setPaused(false)}
        onFocus={() => setPaused(true)}
        onBlur={() => setPaused(false)}
        style={{ position: "relative", outline: "none" }}
      >
        {/* Slide track */}
        <div
          ref={trackRef}
          style={{
            display: "flex",
            overflowX: "scroll",
            scrollSnapType: "x mandatory",
            scrollbarWidth: "none",
            gap: "0",
          }}
        >
          {items.map((item, idx) => (
            <div
              key={`${item.address}-${item.city}`}
              role="group"
              aria-roledescription="slide"
              aria-label={`Slide ${idx + 1} of ${items.length}: ${item.address}`}
              style={{
                minWidth: "100%",
                scrollSnapAlign: "start",
                position: "relative",
                flexShrink: 0,
              }}
            >
              {item.image_url && (
                <div style={{ position: "relative", height: "400px", width: "100%" }}>
                  <Image
                    src={item.image_url}
                    alt={`${item.address}, ${item.city}`}
                    fill
                    style={{ objectFit: "cover" }}
                    sizes="900px"
                    priority={idx === 0}
                  />
                  {/* Overlay */}
                  <div
                    style={{
                      position: "absolute",
                      inset: 0,
                      background: "linear-gradient(to top, rgba(0,0,0,0.8) 0%, transparent 60%)",
                    }}
                  />
                </div>
              )}
              <div
                style={{
                  padding: item.image_url ? "0" : "40px",
                  position: item.image_url ? "absolute" : "relative",
                  bottom: item.image_url ? "30px" : undefined,
                  left: item.image_url ? "40px" : undefined,
                  right: item.image_url ? "40px" : undefined,
                }}
              >
                <span
                  style={{
                    display: "inline-block",
                    background: "var(--color-accent, #d4af37)",
                    color: "#0a0a0a",
                    padding: "3px 12px",
                    fontSize: "11px",
                    fontWeight: 700,
                    letterSpacing: "1px",
                    marginBottom: "8px",
                  }}
                >
                  SOLD
                </span>
                <div
                  aria-label={`Sold for ${item.price}`}
                  style={{
                    fontSize: "28px",
                    fontWeight: 300,
                    color: "white",
                    fontFamily: "var(--font-family, Georgia), serif",
                    marginBottom: "4px",
                  }}
                >
                  {item.price}
                </div>
                <div style={{ fontSize: "14px", color: "rgba(255,255,255,0.7)" }}>
                  {item.address}, {item.city}, {item.state}
                </div>
              </div>
            </div>
          ))}
        </div>

        {/* Prev/Next buttons */}
        <button
          aria-label="Previous slide"
          onClick={goPrev}
          style={{
            position: "absolute",
            top: "50%",
            left: "16px",
            transform: "translateY(-50%)",
            background: "rgba(0,0,0,0.5)",
            border: "1px solid var(--color-accent, #d4af37)",
            color: "var(--color-accent, #d4af37)",
            width: "44px",
            height: "44px",
            borderRadius: "50%",
            cursor: "pointer",
            fontSize: "20px",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 10,
          }}
        >
          ‹
        </button>
        <button
          aria-label="Next slide"
          onClick={goNext}
          style={{
            position: "absolute",
            top: "50%",
            right: "16px",
            transform: "translateY(-50%)",
            background: "rgba(0,0,0,0.5)",
            border: "1px solid var(--color-accent, #d4af37)",
            color: "var(--color-accent, #d4af37)",
            width: "44px",
            height: "44px",
            borderRadius: "50%",
            cursor: "pointer",
            fontSize: "20px",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 10,
          }}
        >
          ›
        </button>

        {/* Dot indicators */}
        <div
          role="tablist"
          aria-label="Slide indicators"
          style={{
            display: "flex",
            justifyContent: "center",
            gap: "8px",
            marginTop: "20px",
          }}
        >
          {items.map((_, idx) => (
            <button
              key={idx}
              role="tab"
              aria-selected={current === idx}
              aria-label={`Go to slide ${idx + 1}`}
              onClick={() => goTo(idx)}
              style={{
                width: current === idx ? "24px" : "8px",
                height: "8px",
                borderRadius: "4px",
                background: current === idx ? "var(--color-accent, #d4af37)" : "rgba(255,255,255,0.3)",
                border: "none",
                cursor: "pointer",
                padding: 0,
                transition: "all 0.3s",
              }}
            />
          ))}
        </div>
      </div>
    </section>
  );
}
