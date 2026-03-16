// apps/agent-site/components/sections/services/ServicesClean.tsx
import type { ServicesProps } from "@/components/sections/types";

export function ServicesClean({ items, title, subtitle }: ServicesProps) {
  return (
    <section
      id="services"
      style={{
        padding: "80px 40px",
        maxWidth: "1000px",
        margin: "0 auto",
      }}
    >
      <h2 style={{
        textAlign: "center",
        fontSize: "32px",
        fontWeight: 600,
        color: "#1a1a1a",
        marginBottom: "8px",
        letterSpacing: "-0.3px",
      }}>
        {title ?? "What I Do for You"}
      </h2>
      {subtitle && (
        <p style={{
          textAlign: "center",
          color: "#888",
          fontSize: "16px",
          marginBottom: "50px",
        }}>
          {subtitle}
        </p>
      )}
      <div style={{
        display: "grid",
        gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
        gap: "30px",
      }}>
        {items.map((item) => (
          <div
            key={item.title}
            style={{
              padding: "28px",
              borderRadius: "8px",
              border: "1px solid #eee",
              transition: "box-shadow 0.3s",
            }}
          >
            <h3 style={{
              color: "#1a1a1a",
              fontSize: "18px",
              fontWeight: 600,
              marginBottom: "8px",
            }}>
              {item.title}
            </h3>
            <p style={{ color: "#666", fontSize: "15px", lineHeight: 1.6 }}>
              {item.description}
            </p>
          </div>
        ))}
      </div>
    </section>
  );
}
