import type { ServicesProps } from "@/components/sections/types";

export function ServicesElegant({ items, title, subtitle }: ServicesProps) {
  return (
    <section
      id="services"
      style={{
        background: "var(--color-primary, #0a0a0a)",
        padding: "80px 40px",
      }}
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
          {title ?? "Our Services"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "rgba(255,255,255,0.6)",
              fontSize: "16px",
              marginBottom: "50px",
            }}
          >
            {subtitle}
          </p>
        )}
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            gap: "24px",
          }}
        >
          {items.map((item) => (
            <article
              key={item.title}
              style={{
                borderLeft: `3px solid var(--color-accent, #d4af37)`,
                paddingLeft: "24px",
                paddingTop: "8px",
                paddingBottom: "8px",
              }}
            >
              <h3
                style={{
                  fontSize: "20px",
                  fontWeight: 400,
                  color: "white",
                  marginBottom: "8px",
                  fontFamily: "var(--font-family, Georgia), serif",
                }}
              >
                {item.title}
              </h3>
              <p
                style={{
                  fontSize: "15px",
                  color: "rgba(255,255,255,0.65)",
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
