import type { ServicesProps } from "@/components/sections/types";

export function ServicesHeart({ items, title, subtitle }: ServicesProps) {
  return (
    <section
      id="features"
      style={{
        background: "white",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "34px",
            fontWeight: 600,
            color: "var(--color-primary, #2d4a3e)",
            marginBottom: subtitle ? "8px" : "48px",
            fontFamily: "var(--font-family, Nunito), sans-serif",
          }}
        >
          {title ?? "How We Help"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "#5a7a6a",
              fontSize: "16px",
              marginBottom: "48px",
              lineHeight: 1.6,
            }}
          >
            {subtitle}
          </p>
        )}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
            gap: "24px",
          }}
        >
          {items.map((item) => (
            <article
              key={item.title}
              style={{
                background: "#f0f7f4",
                borderRadius: "16px",
                padding: "32px 28px",
                boxShadow: "0 2px 12px rgba(90,158,124,0.08)",
              }}
            >
              <h3
                style={{
                  fontSize: "18px",
                  fontWeight: 600,
                  color: "var(--color-primary, #2d4a3e)",
                  marginBottom: "12px",
                  fontFamily: "var(--font-family, Nunito), sans-serif",
                }}
              >
                {item.title}
              </h3>
              <p
                style={{
                  fontSize: "15px",
                  color: "#4a6b5a",
                  lineHeight: 1.7,
                  margin: 0,
                }}
              >
                {item.description}
              </p>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
