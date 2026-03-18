import { clampRating, FTC_DISCLAIMER, type TestimonialsProps } from "@/components/sections/types";

export function TestimonialsGrid({ items, title }: TestimonialsProps) {
  return (
    <section
      id="testimonials"
      style={{
        background: "#f5f5f5",
        maxWidth: "100%",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "var(--color-primary)",
            marginBottom: "10px",
          }}
        >
          {title ?? "What My Clients Say"}
        </h2>
        <p
          style={{
            textAlign: "center",
            color: "#767676",
            fontSize: "13px",
            marginBottom: "45px",
          }}
        >
          {FTC_DISCLAIMER}
        </p>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
            gap: "25px",
          }}
        >
          {items.map((item) => (
            <article
              key={item.reviewer}
              style={{
                background: "#f9f9f9",
                borderRadius: "12px",
                padding: "28px",
                position: "relative",
              }}
            >
              <span
                role="img"
                aria-label={`${clampRating(item.rating)} out of 5 stars`}
                style={{
                  display: "block",
                  color: "var(--color-accent)",
                  fontSize: "18px",
                  marginBottom: "10px",
                }}
              >
                {"★".repeat(clampRating(item.rating))}{"☆".repeat(5 - clampRating(item.rating))}
              </span>
              <p
                style={{
                  fontStyle: "italic",
                  color: "#555",
                  fontSize: "14px",
                  lineHeight: 1.7,
                }}
              >
                {item.text}
              </p>
              <div
                style={{
                  marginTop: "15px",
                  fontWeight: 700,
                  color: "var(--color-primary)",
                  fontSize: "14px",
                }}
              >
                — {item.reviewer}
                {item.source && (
                  <span style={{ fontWeight: "normal", color: "#767676" }}>
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
