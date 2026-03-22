"use client";

import { Suspense, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { ChatWindow } from "@/features/onboarding/ChatWindow";

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5135";
const COMING_SOON = process.env.NEXT_PUBLIC_COMING_SOON === "true";

function ComingSoon() {
  return (
    <main className="min-h-screen bg-gray-950 text-white flex items-center justify-center px-4">
      <div className="text-center space-y-6 max-w-lg">
        <h1 className="text-4xl md:text-5xl font-bold tracking-tight">
          Coming Soon
        </h1>
        <p className="text-lg text-gray-400">
          We&apos;re putting the finishing touches on our AI-powered automation
          platform for real estate agents. Check back soon.
        </p>
        <div className="pt-4">
          <Link
            href="/"
            className="inline-block px-8 py-3 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold transition-colors"
          >
            Back to Home
          </Link>
        </div>
        <p className="text-sm text-gray-600">
          14 days free. $14.99/mo after.
        </p>
      </div>
    </main>
  );
}

function OnboardContent() {
  const searchParams = useSearchParams();
  const profileUrl = searchParams.get("profileUrl");
  const paymentStatus = searchParams.get("payment");
  const sessionIdParam = searchParams.get("session_id");
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [sessionToken, setSessionToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [paymentVerified, setPaymentVerified] = useState<boolean | null>(null);

  // Verify payment status with the server
  useEffect(() => {
    if (paymentStatus !== "success" || !sessionIdParam) return;

    async function verifyPayment() {
      try {
        const res = await fetch(`${API_BASE}/onboard/${sessionIdParam}`, {
          method: "GET",
          headers: { "Content-Type": "application/json" },
        });
        if (!res.ok) throw new Error("Failed to verify payment");
        const data = await res.json();
        setPaymentVerified(data.state === "TrialActivated");
      } catch {
        setPaymentVerified(false);
      }
    }
    verifyPayment();
  }, [paymentStatus, sessionIdParam]);

  useEffect(() => {
    if (paymentStatus === "success" || paymentStatus === "cancelled") return;

    async function createSession() {
      try {
        const res = await fetch(`${API_BASE}/onboard`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ profileUrl }),
        });
        if (!res.ok) throw new Error("Failed to create session");
        const data = await res.json();
        setSessionId(data.sessionId);
        setSessionToken(data.token);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Something went wrong");
      }
    }
    createSession();
  }, [profileUrl, paymentStatus]);

  if (paymentStatus === "success") {
    if (paymentVerified === null) {
      return (
        <main className="min-h-screen bg-gray-950 text-white flex items-center justify-center">
          <div className="flex items-center gap-3">
            <div className="h-5 w-5 rounded-full border-2 border-emerald-500 border-t-transparent animate-spin" />
            <span className="text-gray-400">Verifying payment...</span>
          </div>
        </main>
      );
    }

    if (!paymentVerified) {
      return (
        <main className="min-h-screen bg-gray-950 text-white flex items-center justify-center">
          <div className="text-center space-y-4 max-w-md">
            <h1 className="text-2xl font-bold text-yellow-400">
              Payment Not Confirmed
            </h1>
            <p className="text-gray-400">
              We could not verify your payment. Please contact support or try again.
            </p>
            <a
              href="/onboard"
              className="inline-block px-6 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold transition-colors"
            >
              Try Again
            </a>
          </div>
        </main>
      );
    }

    return (
      <main className="min-h-screen bg-gray-950 text-white flex items-center justify-center">
        <div className="text-center space-y-4 max-w-md">
          <div className="text-5xl">&#10003;</div>
          <h1 className="text-2xl font-bold text-emerald-400">
            Trial Activated!
          </h1>
          <p className="text-gray-400">
            Your 7-day free trial has started. You will not be charged until the
            trial ends. Check your email for next steps.
          </p>
        </div>
      </main>
    );
  }

  if (paymentStatus === "cancelled") {
    return (
      <main className="min-h-screen bg-gray-950 text-white flex items-center justify-center">
        <div className="text-center space-y-4 max-w-md">
          <h1 className="text-2xl font-bold text-yellow-400">
            Payment Cancelled
          </h1>
          <p className="text-gray-400">
            No worries! You can restart the process whenever you are ready.
          </p>
          <a
            href="/onboard"
            className="inline-block px-6 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold transition-colors"
          >
            Try Again
          </a>
        </div>
      </main>
    );
  }

  if (error) {
    return (
      <main className="min-h-screen bg-gray-950 text-white flex items-center justify-center">
        <p className="text-red-400">{error}</p>
      </main>
    );
  }

  if (!sessionId) {
    return (
      <main className="min-h-screen bg-gray-950 text-white flex items-center justify-center">
        <div className="flex items-center gap-3">
          <div className="h-5 w-5 rounded-full border-2 border-emerald-500 border-t-transparent animate-spin" />
          <span className="text-gray-400">Starting your onboarding...</span>
        </div>
      </main>
    );
  }

  return (
    <ChatWindow
      sessionId={sessionId}
      token={sessionToken!}
      initialMessages={[]}
      autoMessage={profileUrl ?? undefined}
    />
  );
}

export default function OnboardPage() {
  if (COMING_SOON) {
    return <ComingSoon />;
  }

  return (
    <Suspense
      fallback={
        <main className="min-h-screen bg-gray-950 text-white flex items-center justify-center">
          <div className="flex items-center gap-3">
            <div className="h-5 w-5 rounded-full border-2 border-emerald-500 border-t-transparent animate-spin" />
            <span className="text-gray-400">Loading...</span>
          </div>
        </main>
      }
    >
      <OnboardContent />
    </Suspense>
  );
}
