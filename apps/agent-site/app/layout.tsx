import "./globals.css";

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const structuredData = {
    "@context": "https://schema.org",
    "@type": "RealEstateAgent",
    url: process.env.SITE_URL || "https://real-estate-star.com",
  };

  // Escape </script> to prevent early tag termination in JSON-LD blocks
  const jsonLdHtml = JSON.stringify(structuredData).replace(/<\/script>/gi, "<\\/script>");

  return (
    <html lang="en">
      <head>
        <script
          type="application/ld+json"
          // JSON.stringify on controlled data — safe, not user HTML
          dangerouslySetInnerHTML={{ __html: jsonLdHtml }}
        />
      </head>
      <body>
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
