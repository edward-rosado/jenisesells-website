import {
  clampRating,
  FTC_DISCLAIMER,
  type TestimonialsProps,
} from "@/components/sections/types";

export function TestimonialsRustic({ items, title }: TestimonialsProps) {
  return (
    <section
      id="testimonials"
      style={{
        background: "var(--color-cream, #faf6f0)",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "1000px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "var(--color-primary, #2d4a3e)",
            marginBottom: "12px",
            fontFamily: "Georgia, serif",
          }}
        >
          {title ?? "What Our Clients Say"}
        </h2>
        <p
          style={{
            textAlign: "center",
            color: "#9a8c7c",
            fontSize: "11px",
            marginBottom: "50px",
            fontFamily: "sans-serif",
            maxWidth: "600px",
            margin: "0 auto 50px",
          }}
        >
          {FTC_DISCLAIMER}
        </p>

        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
            gap: "24px",
          }}
        >
          {items.map((item) => {
            const rating = clampRating(item.rating);
            return (
              <article
                key={item.reviewer}
                style={{
                  background: "var(--color-bg, #faf6f0)",
                  borderRadius: "8px",
                  padding: "28px",
                  border: "1px solid #e8e2d8",
                }}
              >
                <span
                  role="img"
                  aria-label={`${rating} out of 5 stars`}
                  style={{
                    display: "block",
                    color: "var(--color-accent, #c8a84b)",
                    fontSize: "16px",
                    marginBottom: "14px",
                    letterSpacing: "2px",
                  }}
                >
                  {"★".repeat(rating)}
                  {"☆".repeat(5 - rating)}
                </span>

                <p
                  style={{
                    color: "#5a5040",
                    fontSize: "15px",
                    lineHeight: 1.75,
                    fontStyle: "italic",
                    fontFamily: "Georgia, serif",
                    marginBottom: "16px",
                  }}
                >
                  {item.text}
                </p>

                <div
                  style={{
                    fontWeight: 600,
                    color: "var(--color-primary, #2d4a3e)",
                    fontSize: "14px",
                    fontFamily: "sans-serif",
                  }}
                >
                  — {item.reviewer}
                  {item.source && (
                    <span
                      style={{
                        fontWeight: "normal",
                        color: "#9a8c7c",
                        fontSize: "13px",
                      }}
                    >
                      {" "}
                      via {item.source}
                    </span>
                  )}
                </div>
              </article>
            );
          })}
        </div>
      </div>
    </section>
  );
}
