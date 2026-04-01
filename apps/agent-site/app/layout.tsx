import "./globals.css";
import { WebVitalsReporter } from "./WebVitalsReporter";
import { HtmlLangSetter } from "./HtmlLangSetter";

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const structuredData = {
    "@context": "https://schema.org",
    "@type": "RealEstateAgent",
    url: process.env.SITE_URL || "https://real-estate-star.com",
  };

  return (
    <html lang="en">
      <head>
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(structuredData).replace(/<\/script>/gi, "<\\/script>") }}
        />
      </head>
      <body>
        <HtmlLangSetter />
        <WebVitalsReporter />
        <a href="#main-content" className="skip-nav">
          Skip to main content
        </a>
        <main>
          {children}
        </main>
      </body>
    </html>
  );
}
