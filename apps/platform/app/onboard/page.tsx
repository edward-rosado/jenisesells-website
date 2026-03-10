"use client";

import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { ChatWindow } from "@/components/chat/ChatWindow";

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export default function OnboardPage() {
  const searchParams = useSearchParams();
  const profileUrl = searchParams.get("profileUrl");
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
