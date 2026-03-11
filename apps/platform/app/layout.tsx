import type { Metadata } from "next";
import Link from "next/link";
import { GeometricStar } from "@/components/GeometricStar";
import "./globals.css";

export const metadata: Metadata = {
  title: "Real Estate Star",
  description: "Stop paying monthly. $900. Everything.",
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
          <Link
            href="/login"
            className="text-sm text-gray-400 hover:text-white transition-colors"
          >
            Log In
          </Link>
        </header>
        <div id="main-content" tabIndex={-1}>
          {children}
        </div>
        <footer
          role="contentinfo"
          className="border-t border-gray-800/50 px-6 py-8 text-center text-sm text-gray-500"
        >
          <p>&copy; {new Date().getFullYear()} Real Estate Star. All rights reserved.</p>
        </footer>
      </body>
    </html>
  );
}
