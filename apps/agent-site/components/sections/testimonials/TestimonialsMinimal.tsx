import { clampRating, FTC_DISCLAIMER, type TestimonialsProps } from "@/components/sections/types";

export function TestimonialsMinimal({ items, title }: TestimonialsProps) {
  return (
    <section
      id="testimonials"
      style={{
        background: "var(--color-primary, #0a0a0a)",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "800px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "34px",
            fontWeight: 300,
            color: "white",
            marginBottom: "60px",
            fontFamily: "var(--font-family, Georgia), serif",
            letterSpacing: "1px",
          }}
        >
          {title ?? "Client Testimonials"}
        </h2>

        <div
          style={{
            display: "flex",
            flexDirection: "column",
            gap: "50px",
          }}
        >
          {items.map((item) => (
            <div
              key={item.reviewer}
              style={{
                textAlign: "center",
              }}
            >
              <span
                role="img"
                aria-label={`${clampRating(item.rating)} out of 5 stars`}
                style={{
                  display: "block",
                  color: "var(--color-accent, #d4af37)",
                  fontSize: "20px",
                  marginBottom: "16px",
                  letterSpacing: "3px",
                }}
              >
                {"★".repeat(clampRating(item.rating))}{"☆".repeat(5 - clampRating(item.rating))}
              </span>
              <p
                style={{
                  fontStyle: "italic",
                  fontSize: "18px",
                  lineHeight: 1.8,
                  color: "rgba(255,255,255,0.85)",
                  fontFamily: "var(--font-family, Georgia), serif",
                  fontWeight: 300,
                  marginBottom: "20px",
                }}
              >
                &ldquo;{item.text}&rdquo;
              </p>
              <div
                style={{
                  fontSize: "12px",
                  letterSpacing: "2px",
                  textTransform: "uppercase" as const,
                  color: "var(--color-accent, #d4af37)",
                  fontVariant: "small-caps",
                }}
              >
                {item.reviewer}
                {item.source && (
                  <span style={{ color: "rgba(255,255,255,0.4)", fontVariant: "normal", textTransform: "none" as const }}>
                    {" "}via {item.source}
                  </span>
                )}
              </div>
            </div>
          ))}
        </div>

        <p
          style={{
            textAlign: "center",
            color: "rgba(255,255,255,0.3)",
            fontSize: "11px",
            marginTop: "60px",
            lineHeight: 1.6,
          }}
        >
          {FTC_DISCLAIMER}
        </p>
      </div>
    </section>
  );
}
