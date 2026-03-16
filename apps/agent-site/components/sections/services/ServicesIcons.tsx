import type { ServicesProps } from "@/components/sections/types";

export function ServicesIcons({ items, title, subtitle }: ServicesProps) {
  return (
    <section
      id="services"
      style={{
        padding: "70px 40px",
        background: "#FFF8F0",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 700,
          color: "#4A3728",
          marginBottom: subtitle ? "8px" : "40px",
        }}>
          {title ?? "How I Help"}
        </h2>
        {subtitle && (
          <p style={{
            textAlign: "center",
            color: "#8B7355",
            fontSize: "16px",
            marginBottom: "40px",
          }}>
            {subtitle}
          </p>
        )}
        <div style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(250px, 1fr))",
          gap: "24px",
        }}>
          {items.map((item) => (
            <article
              key={item.title}
              style={{
                background: "white",
                borderRadius: "16px",
                padding: "32px 24px",
                textAlign: "center",
                boxShadow: "0 2px 12px rgba(0,0,0,0.06)",
              }}
            >
              <div style={{
                width: "56px",
                height: "56px",
                borderRadius: "50%",
                background: "var(--color-accent)",
                margin: "0 auto 16px",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                opacity: 0.2,
              }} />
              <h3 style={{
                fontSize: "18px",
                fontWeight: 700,
                color: "#4A3728",
                marginBottom: "8px",
              }}>
                {item.title}
              </h3>
              <p style={{
                fontSize: "14px",
                color: "#8B7355",
                lineHeight: 1.6,
              }}>
                {item.description}
              </p>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
