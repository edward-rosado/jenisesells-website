import type { MetadataRoute } from "next";

export default function sitemap(): MetadataRoute.Sitemap {
  const base = "https://platform.real-estate-star.com";
  return [
    { url: base, lastModified: new Date(), changeFrequency: "weekly", priority: 1.0 },
    { url: `${base}/privacy`, lastModified: new Date("2026-03-16"), changeFrequency: "yearly", priority: 0.5 },
    { url: `${base}/terms`, lastModified: new Date("2026-03-16"), changeFrequency: "yearly", priority: 0.5 },
    { url: `${base}/dmca`, lastModified: new Date("2026-03-11"), changeFrequency: "yearly", priority: 0.3 },
    { url: `${base}/accessibility`, lastModified: new Date("2026-03-11"), changeFrequency: "yearly", priority: 0.3 },
  ];
}
