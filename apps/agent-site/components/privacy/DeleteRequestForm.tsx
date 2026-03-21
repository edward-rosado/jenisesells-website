"use client";

import { useState } from "react";
import { requestDeletion } from "@/actions/privacy";

interface DeleteRequestFormProps {
  agentId: string;
  initialEmail: string;
}

export function DeleteRequestForm({ agentId, initialEmail }: DeleteRequestFormProps) {
  const [email, setEmail] = useState(initialEmail);
  const [status, setStatus] = useState<"idle" | "loading" | "success" | "error">("idle");
  const [errorMessage, setErrorMessage] = useState<string>("");

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!email.trim()) return;
    setStatus("loading");
    const result = await requestDeletion(agentId, email.trim());
    if (result.ok) {
      setStatus("success");
    } else {
      setStatus("error");
      setErrorMessage(result.error ?? "Something went wrong. Please try again.");
    }
  }

  if (status === "success") {
    return (
      <div role="status" aria-live="polite" className="text-center py-8">
        <p className="text-lg font-semibold text-green-700">
          Check your email for a verification link.
        </p>
        <p className="text-gray-600 mt-2">
          We&apos;ve sent a verification link to <strong>{email}</strong>. Click
          the link to confirm your deletion request.
        </p>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      <div className="mb-4">
        <label htmlFor="deletion-email" className="block text-sm font-medium text-gray-700 mb-1">
          Email Address
        </label>
        <input
          id="deletion-email"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="your@email.com"
          required
          aria-required="true"
          className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>
      {status === "error" && (
        <p role="alert" className="text-red-600 mb-4 text-sm">
          {errorMessage}
        </p>
      )}
      <button
        type="submit"
        disabled={status === "loading" || !email.trim()}
        aria-disabled={status === "loading" || !email.trim()}
        className="w-full py-3 px-6 rounded-lg font-semibold text-white bg-gray-800 hover:bg-gray-900 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        {status === "loading" ? "Submitting…" : "Submit Deletion Request"}
      </button>
    </form>
  );
}
