"use client";

import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { ChatWindow } from "@/components/chat/ChatWindow";

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export default function OnboardPage() {
  const searchParams = useSearchParams();
  const profileUrl = searchParams.get("profileUrl");
  const paymentStatus = searchParams.get("payment");
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
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
      } catch (err) {
        setError(err instanceof Error ? err.message : "Something went wrong");
      }
    }
    createSession();
  }, [profileUrl]);

  if (paymentStatus === "success") {
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

  return <ChatWindow sessionId={sessionId} initialMessages={[]} />;
}
