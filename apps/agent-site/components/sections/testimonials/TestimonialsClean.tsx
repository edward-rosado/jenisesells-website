// apps/agent-site/components/sections/testimonials/TestimonialsClean.tsx
import type { TestimonialsProps } from "@/components/sections/types";

export function TestimonialsClean({ items, title }: TestimonialsProps) {
  return (
    <section
      id="testimonials"
      style={{
        background: "#fafafa",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 600,
          color: "#1a1a1a",
          marginBottom: "8px",
          letterSpacing: "-0.3px",
        }}>
          {title ?? "What My Clients Say"}
        </h2>
        <p style={{
          textAlign: "center",
          color: "#aaa",
          fontSize: "12px",
          marginBottom: "50px",
        }}>
          Real reviews from real clients. Unedited excerpts from verified reviews on Zillow.
          No compensation was provided. Individual results may vary.
        </p>
        <div style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
          gap: "24px",
        }}>
          {items.map((item) => (
            <article
              key={item.reviewer}
              style={{
                background: "white",
                borderRadius: "8px",
                padding: "24px",
                border: "1px solid #eee",
              }}
            >
              <span
                role="img"
                aria-label={`${item.rating} out of 5 stars`}
                style={{
                  display: "block",
                  color: "var(--color-accent)",
                  fontSize: "16px",
                  marginBottom: "12px",
                }}
              >
                {"★".repeat(item.rating)}{"☆".repeat(5 - item.rating)}
              </span>
              <p style={{
                color: "#555",
                fontSize: "14px",
                lineHeight: 1.7,
                fontStyle: "italic",
              }}>
                {item.text}
              </p>
              <div style={{
                marginTop: "16px",
                fontWeight: 600,
                color: "#1a1a1a",
                fontSize: "14px",
              }}>
                — {item.reviewer}
                {item.source && (
                  <span style={{ fontWeight: "normal", color: "#aaa" }}>
                    {" "}via {item.source}
                  </span>
                )}
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
