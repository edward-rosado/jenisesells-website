"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { FeatureCards } from "@/components/landing/FeatureCards";
import { ComparisonTable } from "@/components/landing/ComparisonTable";
import { TrustStrip } from "@/components/landing/TrustStrip";
import { FinalCta } from "@/components/landing/FinalCta";

export default function LandingPage() {
  const [profileUrl, setProfileUrl] = useState("");
  const router = useRouter();

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const params = profileUrl
      ? `?profileUrl=${encodeURIComponent(profileUrl)}`
      : "";
    router.push(`/onboard${params}`);
  }

  return (
    <main className="min-h-screen bg-gray-950 text-white">
      {/* Hero Section */}
      <section
        data-testid="hero-section"
        className="flex flex-col items-center justify-center px-4 pt-32 pb-20"
      >
        <div className="max-w-xl w-full text-center">
          <h1 className="text-5xl md:text-6xl font-bold tracking-tight mb-2">
            Stop paying monthly.
          </h1>
          <p className="text-4xl md:text-5xl font-bold text-emerald-400 mb-8">
            $900. Everything.
          </p>
          <p className="text-lg text-gray-400 mb-12">
            Website. CMA automation. Lead management. One payment. Done.
          </p>
          <form onSubmit={handleSubmit} className="space-y-4" aria-label="Get started">
            <label htmlFor="hero-profile-url" className="sr-only">
              Your Zillow or Realtor.com profile URL
            </label>
            <input
              id="hero-profile-url"
              type="url"
              value={profileUrl}
              onChange={(e) => setProfileUrl(e.target.value)}
              placeholder="Paste your Zillow or Realtor.com URL"
              className="w-full px-4 py-3 rounded-lg bg-gray-800 border border-gray-700 text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
            <button
              type="submit"
              className="w-full px-6 py-3 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-lg transition-colors"
            >
              Get Started Free
            </button>
          </form>
          <p className="text-sm text-gray-500 mt-4">
            7-day free trial. No credit card.
          </p>
          <p className="text-xs text-gray-600 mt-6">
            By continuing, you agree to our{" "}
            <Link href="/terms" className="underline hover:text-gray-400 transition-colors">
              Terms of Service
            </Link>{" "}
            and{" "}
            <Link href="/privacy" className="underline hover:text-gray-400 transition-colors">
              Privacy Policy
            </Link>
            .
          </p>
        </div>
      </section>

      {/* Trust Strip */}
      <TrustStrip />

      {/* Feature Cards */}
      <FeatureCards />

      {/* Comparison Table */}
      <ComparisonTable />

      {/* Final CTA */}
      <FinalCta />
    </main>
  );
}
