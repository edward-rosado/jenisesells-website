import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Real Estate Agent",
  description: "Your trusted real estate professional",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
