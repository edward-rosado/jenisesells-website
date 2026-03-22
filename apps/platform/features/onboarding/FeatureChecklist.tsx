const FEATURES = [
  "Automated CMA reports",
  "Lead response within 60 seconds",
  "Contract drafting & DocuSign",
  "Professional agent website",
  "Photographer scheduling",
  "MLS listing automation",
];

export function FeatureChecklist() {
  return (
    <div className="bg-gray-800 rounded-xl p-5 max-w-sm space-y-2">
      <h3 className="font-semibold text-white">
        Everything included with Real Estate Star
      </h3>
      <ul className="space-y-1">
        {FEATURES.map((feature) => (
          <li key={feature} className="flex items-center gap-2 text-sm text-gray-300">
            <span className="text-emerald-400">&#10003;</span>
            {feature}
          </li>
        ))}
      </ul>
    </div>
  );
}
