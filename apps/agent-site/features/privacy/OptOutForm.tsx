"use client";

import { useState } from "react";
import { requestOptOut } from "./privacy";

interface OptOutFormProps {
  agentId: string;
  email: string;
  token: string;
}

export function OptOutForm({ agentId, email, token }: OptOutFormProps) {
  const [status, setStatus] = useState<"idle" | "loading" | "success" | "error">("idle");
  const [errorMessage, setErrorMessage] = useState<string>("");

  async function handleConfirm() {
    setStatus("loading");
    const result = await requestOptOut(agentId, email, token);
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
          You have been successfully opted out.
        </p>
        <p className="text-gray-600 mt-2">
          You will no longer receive marketing communications.
        </p>
      </div>
    );
  }

  return (
    <div>
      {email && (
        <p className="text-sm text-gray-500 mb-4">
          Opting out email: <strong>{email}</strong>
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
        className="w-full py-3 px-6 rounded-lg font-semibold text-white bg-red-600 hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        {status === "loading" ? "Processing…" : "Confirm Opt Out"}
      </button>
    </div>
  );
}
