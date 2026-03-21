"use client";

import { useState } from "react";
import { requestExport } from "@/actions/privacy";
import type { ExportData } from "@/actions/privacy";

interface MyDataFormProps {
  agentId: string;
  initialEmail: string;
  privacyHref: string;
}

export function MyDataForm({ agentId, initialEmail, privacyHref }: MyDataFormProps) {
  const [email, setEmail] = useState(initialEmail);
  const [status, setStatus] = useState<"idle" | "loading" | "success" | "not-found" | "error">("idle");
  const [errorMessage, setErrorMessage] = useState<string>("");
  const [data, setData] = useState<ExportData[]>([]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const trimmed = email.trim();
    if (!trimmed) return;
    setStatus("loading");
    setErrorMessage("");
    const result = await requestExport(agentId, trimmed);
    if (!result.ok) {
      setStatus("error");
      setErrorMessage(result.error ?? "Something went wrong. Please try again.");
      return;
    }
    if (!result.data || result.data.length === 0) {
      setStatus("not-found");
      setData([]);
      return;
    }
    setStatus("success");
    setData(result.data);
  }

  return (
    <>
      <form onSubmit={handleSubmit} noValidate>
        <div className="mb-4">
          <label htmlFor="mydata-email" className="block text-sm font-medium text-gray-700 mb-1">
            Email Address
          </label>
          <input
            id="mydata-email"
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
          {status === "loading" ? "Loading..." : "Request My Data"}
        </button>
      </form>

      {status === "not-found" && (
        <div role="status" aria-live="polite" className="mt-6 text-center py-4">
          <p className="text-gray-600">No data found for this email address.</p>
        </div>
      )}

      {status === "success" && data.length > 0 && (
        <div role="status" aria-live="polite" className="mt-6">
          <h2 className="text-lg font-semibold mb-4">Your Data</h2>
          <div className="space-y-4">
            {data.map((item, index) => (
              <div key={index} className="border border-gray-200 rounded-lg p-4">
                {item.name && <p><span className="font-medium">Name:</span> {item.name}</p>}
                <p><span className="font-medium">Email:</span> {item.email}</p>
                {item.phone && <p><span className="font-medium">Phone:</span> {item.phone}</p>}
                {item.propertyAddress && <p><span className="font-medium">Property:</span> {item.propertyAddress}</p>}
                {item.source && <p><span className="font-medium">Source:</span> {item.source}</p>}
                {item.status && <p><span className="font-medium">Status:</span> {item.status}</p>}
                {item.submittedAt && <p><span className="font-medium">Submitted:</span> {item.submittedAt}</p>}
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="mt-6 text-center">
        <a href={privacyHref} className="text-blue-600 hover:underline text-sm">
          Back to Privacy Policy
        </a>
      </div>
    </>
  );
}
