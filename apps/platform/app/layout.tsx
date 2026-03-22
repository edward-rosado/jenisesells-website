import type { Metadata } from "next";
import Link from "next/link";
import { GeometricStar } from "@/features/shared/GeometricStar";
import { EqualHousingOpportunity } from "@/components/legal/EqualHousingOpportunity";
import { CookieConsentBanner } from "@/components/legal/CookieConsentBanner";
import "./globals.css";

export const metadata: Metadata = {
  title: "Real Estate Star",
  description: "14 days free, then $14.99/mo. Your business, automated by AI.",
  openGraph: {
    title: "Real Estate Star — AI-Powered Real Estate Automation",
    description: "Deploy your website, generate CMAs, and manage leads. 14 days free.",
    url: "https://platform.real-estate-star.com",
    type: "website",
    siteName: "Real Estate Star",
  },
  twitter: {
    card: "summary",
    title: "Real Estate Star",
    description: "AI-Powered Real Estate Automation for Agents",
  },
  alternates: {
    canonical: "https://platform.real-estate-star.com",
  },
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body className="bg-gray-950 text-white antialiased">
        <a href="#main-content" className="skip-link">
          Skip to main content
        </a>
        <header className="fixed top-0 w-full z-50 flex items-center justify-between px-6 py-4 backdrop-blur-md bg-gray-950/80 border-b border-gray-800/50">
          <Link
            href="/"
            className="flex items-center gap-2 text-lg font-bold tracking-tight text-white"
            aria-label="Real Estate Star home"
          >
            <GeometricStar size={24} />
            <span>Real Estate Star</span>
          </Link>
        </header>
        <div id="main-content" tabIndex={-1}>
          {children}
        </div>
        <footer
          role="contentinfo"
          className="border-t border-gray-800/50 px-6 py-8 text-center text-sm text-gray-500"
        >
          <nav aria-label="Legal links" className="mb-4 flex flex-wrap items-center justify-center gap-x-4 gap-y-2">
            <Link href="/privacy" className="hover:text-white transition-colors">
              Privacy Policy
            </Link>
            <span aria-hidden="true">|</span>
            <Link href="/terms" className="hover:text-white transition-colors">
              Terms of Service
            </Link>
            <span aria-hidden="true">|</span>
            <Link href="/dmca" className="hover:text-white transition-colors">
              DMCA
            </Link>
            <span aria-hidden="true">|</span>
            <Link href="/accessibility" className="hover:text-white transition-colors">
              Accessibility
            </Link>
            <span aria-hidden="true">|</span>
            <Link href="/status" className="hover:text-white transition-colors">
              Status
            </Link>
          </nav>
          <EqualHousingOpportunity />
          <p className="mt-4">&copy; {new Date().getFullYear()} Real Estate Star. All rights reserved.</p>
          <p className="mt-1 text-gray-500 text-xs" data-testid="version">v1.0</p>
        </footer>
        <CookieConsentBanner />
      </body>
    </html>
  );
}
