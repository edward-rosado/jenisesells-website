import type { MetadataRoute } from "next";

export default function sitemap(): MetadataRoute.Sitemap {
  const baseUrl = process.env.SITE_URL || "https://real-estate-star.com";

  return [
    { url: baseUrl, lastModified: new Date(), changeFrequency: "weekly", priority: 1.0 },
    { url: `${baseUrl}/privacy`, lastModified: new Date("2026-03-16"), changeFrequency: "yearly", priority: 0.5 },
    { url: `${baseUrl}/terms`, lastModified: new Date("2026-03-16"), changeFrequency: "yearly", priority: 0.5 },
    { url: `${baseUrl}/accessibility`, lastModified: new Date("2026-03-11"), changeFrequency: "yearly", priority: 0.3 },
  ];
}
