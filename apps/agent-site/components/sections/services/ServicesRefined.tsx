import type { ServicesProps } from "@/components/sections/types";

export function ServicesRefined({ items, title, subtitle }: ServicesProps) {
  return (
    <section
      id="features"
      style={{
        padding: "80px 0",
      }}
    >
      <div style={{ maxWidth: "800px", margin: "0 auto", paddingLeft: "40px", paddingRight: "40px" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 300,
            color: "var(--color-primary, #3d3028)",
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
              color: "var(--color-secondary, #5a4a3a)",
              fontSize: "16px",
              marginBottom: "50px",
            }}
          >
            {subtitle}
          </p>
        )}
      </div>
      <div style={{ maxWidth: "800px", margin: "0 auto" }}>
        {items.map((item, index) => (
          <article
            key={item.title}
            style={{
              background: index % 2 === 0 ? "#ffffff" : "#f8f6f3",
              borderLeft: "2px solid var(--color-accent, #b8926a)",
              padding: "28px 40px",
            }}
          >
            <h3
              style={{
                fontSize: "20px",
                fontWeight: 400,
                color: "var(--color-primary, #3d3028)",
                marginBottom: "8px",
                fontFamily: "var(--font-family, Georgia), serif",
              }}
            >
              {item.title}
            </h3>
            <p
              style={{
                fontSize: "15px",
                color: "var(--color-secondary, #5a4a3a)",
                lineHeight: 1.7,
                margin: 0,
              }}
            >
              {item.description}
            </p>
          </article>
        ))}
      </div>
    </section>
  );
}
