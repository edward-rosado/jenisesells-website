import type { TestimonialsProps } from "@/components/sections/types";

export function TestimonialsBubble({ items, title }: TestimonialsProps) {
  return (
    <section
      id="testimonials"
      style={{
        background: "#FFF8F0",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 700,
          color: "#4A3728",
          marginBottom: "8px",
        }}>
          {title ?? "What My Clients Say"}
        </h2>
        <p style={{
          textAlign: "center",
          color: "#B0A090",
          fontSize: "12px",
          marginBottom: "45px",
        }}>
          Real reviews from real clients. Unedited excerpts from verified reviews on Zillow.
          No compensation was provided. Individual results may vary.
        </p>
        <div style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
          gap: "28px",
        }}>
          {items.map((item) => (
            <div key={item.reviewer}>
              {/* Speech bubble */}
              <article style={{
                background: "white",
                borderRadius: "20px",
                padding: "24px",
                position: "relative",
                boxShadow: "0 2px 12px rgba(0,0,0,0.06)",
                marginBottom: "16px",
              }}>
                <span
                  role="img"
                  aria-label={`${item.rating} out of 5 stars`}
                  style={{
                    display: "block",
                    color: "var(--color-accent)",
                    fontSize: "16px",
                    marginBottom: "10px",
                  }}
                >
                  {"★".repeat(item.rating)}{"☆".repeat(5 - item.rating)}
                </span>
                <p style={{
                  fontStyle: "italic",
                  color: "#6B5A4A",
                  fontSize: "14px",
                  lineHeight: 1.7,
                  margin: 0,
                }}>
                  {item.text}
                </p>
                {/* Bubble tail */}
                <div style={{
                  position: "absolute",
                  bottom: "-8px",
                  left: "24px",
                  width: "16px",
                  height: "16px",
                  background: "white",
                  transform: "rotate(45deg)",
                  boxShadow: "2px 2px 4px rgba(0,0,0,0.04)",
                }} />
              </article>
              {/* Reviewer info below bubble */}
              <div style={{
                display: "flex",
                alignItems: "center",
                gap: "12px",
                paddingLeft: "12px",
              }}>
                <div style={{
                  width: "36px",
                  height: "36px",
                  borderRadius: "50%",
                  background: "var(--color-accent)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  color: "white",
                  fontWeight: 700,
                  fontSize: "14px",
                }}>
                  {item.reviewer.charAt(0)}
                </div>
                <div>
                  <div style={{
                    fontWeight: 700,
                    color: "#4A3728",
                    fontSize: "14px",
                  }}>
                    {item.reviewer}
                  </div>
                  {item.source && (
                    <div style={{ color: "#B0A090", fontSize: "12px" }}>
                      via {item.source}
                    </div>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
