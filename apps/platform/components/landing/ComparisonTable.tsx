interface ComparisonRow {
  feature: string;
  star: string;
  kvcore: string;
  ylopo: string;
}

const ROWS: ComparisonRow[] = [
  { feature: "Price", star: "$900 one-time", kvcore: "$499/mo", ylopo: "$395/mo" },
  { feature: "Website", star: "Included", kvcore: "Included", ylopo: "Included" },
  { feature: "CMA Tool", star: "Included", kvcore: "Add-on", ylopo: "No" },
  { feature: "Lead Capture", star: "Included", kvcore: "Included", ylopo: "Included" },
  { feature: "Setup Time", star: "10 minutes", kvcore: "2-4 weeks", ylopo: "1-2 weeks" },
];

export function ComparisonTable() {
  return (
    <section className="py-20 px-6 bg-gray-900/30">
      <div className="max-w-4xl mx-auto">
        <h2 className="text-3xl md:text-4xl font-bold text-center mb-12">
          Why Agents Switch
        </h2>
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="border-b border-gray-700">
                <th className="py-3 px-4 text-gray-400 font-medium text-sm" />
                <th className="py-3 px-4 text-emerald-400 font-semibold">Real Estate Star</th>
                <th className="py-3 px-4 text-gray-400 font-medium">KVCore</th>
                <th className="py-3 px-4 text-gray-400 font-medium">Ylopo</th>
              </tr>
            </thead>
            <tbody>
              {ROWS.map((row) => (
                <tr key={row.feature} className="border-b border-gray-800">
                  <td className="py-3 px-4 text-gray-300 font-medium">{row.feature}</td>
                  <td className="py-3 px-4 text-white font-semibold">{row.star}</td>
                  <td className="py-3 px-4 text-gray-400">{row.kvcore}</td>
                  <td className="py-3 px-4 text-gray-400">{row.ylopo}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
