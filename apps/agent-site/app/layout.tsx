import "./globals.css";
import { WebVitalsReporter } from "./WebVitalsReporter";
import { getUiStrings } from "@/features/i18n";

interface RootLayoutProps {
  children: React.ReactNode;
  params?: Promise<Record<string, string>>;
  searchParams?: Promise<{ locale?: string }>;
}

export default async function RootLayout({
  children,
  searchParams,
}: Readonly<RootLayoutProps>) {
  const sp = await searchParams;
  const locale = sp?.locale ?? "en";
  const ui = getUiStrings(locale);

  const structuredData = {
    "@context": "https://schema.org",
    "@type": "RealEstateAgent",
    url: process.env.SITE_URL || "https://real-estate-star.com",
  };

  return (
    <html lang={locale}>
      <head>
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(structuredData).replace(/<\/script>/gi, "<\\/script>") }}
        />
      </head>
      <body>
        <WebVitalsReporter />
        <a href="#main-content" className="skip-nav">
          {ui.skipToContent}
        </a>
        <main>
          {children}
        </main>
      </body>
    </html>
  );
}
