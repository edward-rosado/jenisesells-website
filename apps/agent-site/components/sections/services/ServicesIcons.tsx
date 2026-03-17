import type { ServicesProps } from "@/components/sections/types";

type SvgIcon = React.ReactNode;

const SVG_PROPS = {
  width: "28",
  height: "28",
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "1.8",
  strokeLinecap: "round" as const,
  strokeLinejoin: "round" as const,
};

/** Named icon registry — agents can override via `icon` field in content.json. */
const ICON_REGISTRY: Record<string, SvgIcon> = {
  home: (
    <svg {...SVG_PROPS}>
      <path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2V9z" />
      <path d="M9 22V12h6v10" />
    </svg>
  ),
  camera: (
    <svg {...SVG_PROPS}>
      <path d="M23 19a2 2 0 01-2 2H3a2 2 0 01-2-2V8a2 2 0 012-2h4l2-3h6l2 3h4a2 2 0 012 2v11z" />
      <circle cx="12" cy="13" r="4" />
    </svg>
  ),
  dollar: (
    <svg {...SVG_PROPS}>
      <line x1="12" y1="1" x2="12" y2="23" />
      <path d="M17 5H9.5a3.5 3.5 0 000 7h5a3.5 3.5 0 010 7H6" />
    </svg>
  ),
  wrench: (
    <svg {...SVG_PROPS}>
      <path d="M14.7 6.3a1 1 0 000 1.4l1.6 1.6a1 1 0 001.4 0l3.77-3.77a6 6 0 01-7.94 7.94l-6.91 6.91a2.12 2.12 0 01-3-3l6.91-6.91a6 6 0 017.94-7.94l-3.76 3.76z" />
    </svg>
  ),
  globe: (
    <svg {...SVG_PROPS}>
      <circle cx="12" cy="12" r="10" />
      <line x1="2" y1="12" x2="22" y2="12" />
      <path d="M12 2a15.3 15.3 0 014 10 15.3 15.3 0 01-4 10 15.3 15.3 0 01-4-10 15.3 15.3 0 014-10z" />
    </svg>
  ),
  search: (
    <svg {...SVG_PROPS}>
      <circle cx="11" cy="11" r="8" />
      <line x1="21" y1="21" x2="16.65" y2="16.65" />
    </svg>
  ),
  chat: (
    <svg {...SVG_PROPS}>
      <path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2v10z" />
    </svg>
  ),
  handshake: (
    <svg {...SVG_PROPS}>
      <path d="M6 2L3 6v14a2 2 0 002 2h14a2 2 0 002-2V6l-3-4H6z" />
      <line x1="3" y1="6" x2="21" y2="6" />
      <path d="M16 10a4 4 0 01-8 0" />
    </svg>
  ),
  checkCircle: (
    <svg {...SVG_PROPS}>
      <path d="M22 11.08V12a10 10 0 11-5.93-9.14" />
      <polyline points="22 4 12 14.01 9 11.01" />
    </svg>
  ),
  barChart: (
    <svg {...SVG_PROPS}>
      <line x1="18" y1="20" x2="18" y2="10" />
      <line x1="12" y1="20" x2="12" y2="4" />
      <line x1="6" y1="20" x2="6" y2="14" />
    </svg>
  ),
  truck: (
    <svg {...SVG_PROPS}>
      <rect x="1" y="3" width="15" height="13" rx="2" />
      <polygon points="16 8 20 8 23 11 23 16 16 16 16 8" />
      <circle cx="5.5" cy="18.5" r="2.5" />
      <circle cx="18.5" cy="18.5" r="2.5" />
    </svg>
  ),
  star: (
    <svg {...SVG_PROPS}>
      <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" />
    </svg>
  ),
  shield: (
    <svg {...SVG_PROPS}>
      <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
    </svg>
  ),
  key: (
    <svg {...SVG_PROPS}>
      <path d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 11-7.778 7.778 5.5 5.5 0 017.777-7.777zm0 0L15.5 7.5m0 0l3 3L22 7l-3-3m-3.5 3.5L19 4" />
    </svg>
  ),
  heart: (
    <svg {...SVG_PROPS}>
      <path d="M20.84 4.61a5.5 5.5 0 00-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 00-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 000-7.78z" />
    </svg>
  ),
};

/** Keyword-to-icon-key mapping — used as fallback when no `icon` is specified. */
const KEYWORD_MAP: [string[], string][] = [
  [["value", "valuation", "cma", "market analysis"], "home"],
  [["photo", "staging"], "camera"],
  [["pricing", "strategic"], "dollar"],
  [["prep", "renovation"], "wrench"],
  [["exposure", "marketing", "listing", "mls", "ads"], "globe"],
  [["search", "buyer", "find", "looking"], "search"],
  [["habla", "bilingual", "language"], "chat"],
  [["negotiat", "deal"], "handshake"],
  [["closing", "full-service", "coordinated"], "checkCircle"],
  [["invest", "roi", "portfolio"], "barChart"],
  [["relocation", "moving"], "truck"],
];

/** Resolve icon: explicit `icon` key from content.json > keyword match > default star. */
export function resolveServiceIcon(title: string, icon?: string): SvgIcon {
  // 1. Explicit override from content.json
  if (icon && ICON_REGISTRY[icon]) return ICON_REGISTRY[icon];

  // 2. Keyword match from title
  const t = title.toLowerCase();
  for (const [keywords, key] of KEYWORD_MAP) {
    if (keywords.some((kw) => t.includes(kw))) return ICON_REGISTRY[key];
  }

  // 3. Fallback
  return ICON_REGISTRY.star;
}

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
                opacity: 0.85,
                color: "white",
              }}>
                {resolveServiceIcon(item.title, item.icon)}
              </div>
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
