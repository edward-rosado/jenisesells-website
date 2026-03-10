import type { StepItem } from "@/lib/types";

interface HowItWorksProps {
  steps: StepItem[];
}

export function HowItWorks({ steps }: HowItWorksProps) {
  return (
    <section className="py-16 px-10 max-w-6xl mx-auto">
      <h2 className="text-3xl font-bold text-center mb-10" style={{ color: "var(--color-primary)" }}>
        How It Works
      </h2>
      <div className="flex justify-center gap-10 flex-wrap">
        {steps.map((step) => (
          <div key={step.number} className="text-center max-w-[250px]">
            <div
              className="w-14 h-14 rounded-full flex items-center justify-center text-2xl font-bold text-white mx-auto mb-4"
              style={{ backgroundColor: "var(--color-secondary)" }}
            >
              {step.number}
            </div>
            <h3 className="font-bold mb-2" style={{ color: "var(--color-primary)" }}>
              {step.title}
            </h3>
            <p className="text-gray-500 text-sm">{step.description}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
