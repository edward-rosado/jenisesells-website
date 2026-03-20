"use client";

import { useState } from "react";
import { requestSubscribe } from "@/actions/privacy";

interface SubscribeFormProps {
  agentId: string;
  email: string;
  token: string;
}

export function SubscribeForm({ agentId, email, token }: SubscribeFormProps) {
  const [status, setStatus] = useState<"idle" | "loading" | "success" | "error">("idle");
  const [errorMessage, setErrorMessage] = useState<string>("");

  async function handleConfirm() {
    setStatus("loading");
    const result = await requestSubscribe(agentId, email, token);
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
          You have been successfully re-subscribed.
        </p>
        <p className="text-gray-600 mt-2">
          You will receive communications and updates again.
        </p>
      </div>
    );
  }

  return (
    <div>
      {email && (
        <p className="text-sm text-gray-500 mb-4">
          Re-subscribing email: <strong>{email}</strong>
        </p>
      )}
      {status === "error" && (
        <p role="alert" className="text-red-600 mb-4 text-sm">
          {errorMessage}
        </p>
      )}
      <button
        type="button"
        onClick={handleConfirm}
        disabled={status === "loading"}
        aria-disabled={status === "loading"}
        className="w-full py-3 px-6 rounded-lg font-semibold text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        {status === "loading" ? "Processing…" : "Confirm Re-subscribe"}
      </button>
    </div>
  );
}
