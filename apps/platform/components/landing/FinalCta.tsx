import Link from "next/link";

export function FinalCta() {
  return (
    <section className="py-24 px-6 text-center" aria-labelledby="final-cta-heading">
      <div className="max-w-2xl mx-auto space-y-6">
        <h2 id="final-cta-heading" className="text-3xl md:text-4xl font-bold">
          Ready to Get Started?
        </h2>
        <p className="text-lg text-gray-400">
          Join agents who stopped paying monthly. No credit card required.
        </p>
        <Link
          href="/onboard"
          className="inline-block px-8 py-4 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-lg transition-colors"
        >
          Start Your Free Trial
        </Link>
      </div>
    </section>
  );
}
