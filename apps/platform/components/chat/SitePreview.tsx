interface SitePreviewProps {
  siteUrl: string;
  onApprove: () => void;
  /** When true, shows the CMA form section header instead of approve button */
  showCmaHighlight?: boolean;
}

export function SitePreview({ siteUrl, onApprove, showCmaHighlight }: SitePreviewProps) {
  // Append #cma-form anchor when highlighting the CMA section
  const iframeSrc = showCmaHighlight ? `${siteUrl}#cma-form` : siteUrl;

  return (
    <div className="bg-gray-800 rounded-xl p-4 max-w-lg space-y-3">
      <h3 className="font-semibold text-white">
        {showCmaHighlight ? "Your CMA Form" : "Your Site Preview"}
      </h3>
      {showCmaHighlight && (
        <p className="text-sm text-gray-400">
          This is the CMA form on your website. When leads fill it out, they get a
          professional market analysis automatically.
        </p>
      )}
      <div className="rounded-lg overflow-hidden border border-gray-700">
        <iframe
          src={iframeSrc}
          title={showCmaHighlight ? "CMA form preview" : "Site preview"}
          className="w-full h-80"
          sandbox="allow-scripts"
        />
      </div>
      {!showCmaHighlight && (
        <button
          onClick={onApprove}
          className="w-full px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold transition-colors"
        >
          Approve
        </button>
      )}
    </div>
  );
}
