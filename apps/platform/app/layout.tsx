import type { Metadata } from "next";
import Link from "next/link";
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
        <header className="fixed top-0 w-full z-50 flex items-center justify-between px-6 py-4">
          <span className="text-lg font-bold tracking-tight">
            <span aria-hidden="true">★ </span>
            <span>Real Estate Star</span>
          </span>
          <Link
            href="/login"
            className="text-sm text-gray-400 hover:text-white transition-colors"
          >
            Log In
          </Link>
        </header>
        {children}
      </body>
    </html>
  );
}
