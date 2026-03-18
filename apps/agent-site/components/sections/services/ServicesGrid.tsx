import type { FeaturesProps } from "@/components/sections/types";

export function ServicesGrid({ items, title, subtitle }: FeaturesProps) {
  return (
    <section
      id="services"
      style={{
        padding: "70px 40px",
        maxWidth: "1100px",
        margin: "0 auto",
      }}
    >
      <h2
        style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 700,
          color: "var(--color-primary)",
          marginBottom: "10px",
        }}
      >
        {title ?? "What I Do for You"}
      </h2>
      {subtitle && (
        <p
          style={{
            textAlign: "center",
            color: "#666",
            fontSize: "16px",
            marginBottom: "45px",
          }}
        >
          {subtitle}
        </p>
      )}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))",
          gap: "25px",
        }}
      >
        {items.map((item) => (
          <div
            key={item.title}
            style={{
              background: "#f9f9f9",
              borderRadius: "12px",
              padding: "30px",
              borderLeft: "4px solid var(--color-secondary)",
              transition: "transform 0.3s, box-shadow 0.3s",
            }}
          >
            <h3
              style={{
                color: "var(--color-primary)",
                fontSize: "19px",
                marginBottom: "10px",
              }}
            >
              {item.title}
            </h3>
            <p style={{ color: "#555", fontSize: "15px" }}>{item.description}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
