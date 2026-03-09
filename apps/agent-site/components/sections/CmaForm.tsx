"use client";

import { useState } from "react";
import type { AgentConfig, CmaFormData } from "@/lib/types";

interface CmaFormProps {
  agent: AgentConfig;
  data: CmaFormData;
}

export function CmaForm({ agent, data }: CmaFormProps) {
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setSubmitting(true);
    const formData = new FormData(e.currentTarget);
    const endpoint = agent.integrations?.form_handler === "formspree"
      ? `https://formspree.io/f/${agent.integrations.form_handler_id}`
      : `/api/leads`;

    try {
      await fetch(endpoint, { method: "POST", body: formData });
      window.location.href = "/thank-you";
    } catch {
      setSubmitting(false);
    }
  }

  return (
    <section id="cma-form" className="py-16 px-10 max-w-2xl mx-auto">
      <h2 className="text-3xl font-bold text-center mb-2" style={{ color: "var(--color-primary)" }}>
        {data.title}
      </h2>
      <p className="text-center text-gray-500 mb-10">{data.subtitle}</p>
      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <input name="firstName" placeholder="First Name" required className="border rounded-lg px-4 py-3 w-full" />
          <input name="lastName" placeholder="Last Name" required className="border rounded-lg px-4 py-3 w-full" />
        </div>
        <input name="email" type="email" placeholder="Email Address" required className="border rounded-lg px-4 py-3 w-full" />
        <input name="phone" type="tel" placeholder="Phone Number" required className="border rounded-lg px-4 py-3 w-full" />
        <input name="address" placeholder="Property Address" required className="border rounded-lg px-4 py-3 w-full" />
        <div className="grid grid-cols-3 gap-4">
          <input name="city" placeholder="City" required className="border rounded-lg px-4 py-3 w-full" />
          <input name="state" placeholder="State" defaultValue={agent.location.state} required className="border rounded-lg px-4 py-3 w-full" />
          <input name="zip" placeholder="Zip" required className="border rounded-lg px-4 py-3 w-full" />
        </div>
        <select name="timeline" required className="border rounded-lg px-4 py-3 w-full">
          <option value="">When are you looking to sell?</option>
          <option value="asap">As soon as possible</option>
          <option value="1-3m">1-3 months</option>
          <option value="3-6m">3-6 months</option>
          <option value="6-12m">6-12 months</option>
          <option value="curious">Just curious about my home&apos;s value</option>
        </select>
        <textarea name="notes" placeholder="Anything else I should know?" rows={3} className="border rounded-lg px-4 py-3 w-full" />
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
