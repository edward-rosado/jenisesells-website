"use client";

import { useState } from "react";
import type { AgentConfig, CmaFormData } from "@/lib/types";

interface CmaFormProps {
  agent: AgentConfig;
  data: CmaFormData;
}

export function CmaForm({ agent, data }: CmaFormProps) {
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    const formData = new FormData(e.currentTarget);
    const endpoint = agent.integrations?.form_handler === "formspree"
      ? `https://formspree.io/f/${agent.integrations.form_handler_id}`
      : `/api/agents/${agent.id}/cma`;

    try {
      const response = await fetch(endpoint, {
        method: "POST",
        body: formData,
        headers: { Accept: "application/json" },
      });
      if (!response.ok) {
        throw new Error(`Submission failed (${response.status})`);
      }
      window.location.href = `/thank-you?agentId=${encodeURIComponent(agent.id)}`;
    } catch {
      setError("Something went wrong. Please try again.");
      setSubmitting(false);
    }
  }

  return (
    <section id="cma-form" className="py-16 px-10 max-w-2xl mx-auto">
      <h2 className="text-3xl font-bold text-center mb-2" style={{ color: "var(--color-primary)" }}>
        {data.title}
      </h2>
      <p className="text-center text-gray-500 mb-10">{data.subtitle}</p>
      {error && (
        <p className="text-red-600 text-center mb-4 font-medium">{error}</p>
      )}
      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="firstName" className="sr-only">First Name</label>
            <input id="firstName" name="firstName" placeholder="First Name" required className="border rounded-lg px-4 py-3 w-full" />
          </div>
          <div>
            <label htmlFor="lastName" className="sr-only">Last Name</label>
            <input id="lastName" name="lastName" placeholder="Last Name" required className="border rounded-lg px-4 py-3 w-full" />
          </div>
        </div>
        <label htmlFor="email" className="sr-only">Email Address</label>
        <input id="email" name="email" type="email" placeholder="Email Address" required className="border rounded-lg px-4 py-3 w-full" />
        <label htmlFor="phone" className="sr-only">Phone Number</label>
        <input id="phone" name="phone" type="tel" placeholder="Phone Number" required className="border rounded-lg px-4 py-3 w-full" />
        <label htmlFor="address" className="sr-only">Property Address</label>
        <input id="address" name="address" placeholder="Property Address" required className="border rounded-lg px-4 py-3 w-full" />
        <div className="grid grid-cols-3 gap-4">
          <div>
            <label htmlFor="city" className="sr-only">City</label>
            <input id="city" name="city" placeholder="City" required className="border rounded-lg px-4 py-3 w-full" />
          </div>
          <div>
            <label htmlFor="state" className="sr-only">State</label>
            <input id="state" name="state" placeholder="State" defaultValue={agent.location.state} required className="border rounded-lg px-4 py-3 w-full" />
          </div>
          <div>
            <label htmlFor="zip" className="sr-only">Zip Code</label>
            <input id="zip" name="zip" placeholder="Zip" required className="border rounded-lg px-4 py-3 w-full" />
          </div>
        </div>
        <label htmlFor="timeline" className="sr-only">When are you looking to sell?</label>
        <select id="timeline" name="timeline" required className="border rounded-lg px-4 py-3 w-full">
          <option value="">When are you looking to sell?</option>
          <option value="asap">As soon as possible</option>
          <option value="1-3m">1-3 months</option>
          <option value="3-6m">3-6 months</option>
          <option value="6-12m">6-12 months</option>
          <option value="curious">Just curious about my home&apos;s value</option>
        </select>
        <label htmlFor="notes" className="sr-only">Additional notes</label>
        <textarea id="notes" name="notes" placeholder="Anything else I should know?" rows={3} className="border rounded-lg px-4 py-3 w-full" />
        <input type="hidden" name="_subject" value={`New CMA Request — ${agent.identity.name}`} />
        <button
          type="submit"
          disabled={submitting}
          className="w-full py-4 rounded-full text-lg font-bold transition-transform hover:-translate-y-0.5 disabled:opacity-50"
          style={{ backgroundColor: "var(--color-accent)", color: "var(--color-primary)" }}
        >
          {submitting ? "Submitting..." : "Get My Free Home Value Report →"}
        </button>
      </form>
    </section>
  );
}
