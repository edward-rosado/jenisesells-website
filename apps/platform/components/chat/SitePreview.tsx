interface SitePreviewProps {
  siteUrl: string;
  onApprove: () => void;
  /** When true, shows the CMA form section header instead of approve button */
  showCmaHighlight?: boolean;
}

function isSafePreviewUrl(url: string): boolean {
  try {
    const parsed = new URL(url);
    return (
      (parsed.protocol === "https:" || parsed.protocol === "http:") &&
      (parsed.hostname === "localhost" ||
        parsed.hostname.endsWith(".realestatestar.com") ||
        parsed.hostname.endsWith(".pages.dev"))
    );
  } catch {
    return false;
  }
}

export function SitePreview({ siteUrl, onApprove, showCmaHighlight }: SitePreviewProps) {
  // Append #cma-form anchor when highlighting the CMA section
  const iframeSrc = showCmaHighlight ? `${siteUrl}#cma-form` : siteUrl;
  const urlSafe = isSafePreviewUrl(siteUrl);

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
      {urlSafe ? (
        <div className="rounded-lg overflow-hidden border border-gray-700">
          <iframe
            src={iframeSrc}
            title={showCmaHighlight ? "CMA form preview" : "Site preview"}
            className="w-full h-80"
            sandbox="allow-scripts"
          />
        </div>
      ) : (
        <div className="rounded-lg border border-red-700 bg-red-900/20 p-4 text-center">
          <p className="text-red-400 font-medium">Unable to preview this URL</p>
          <p className="text-sm text-gray-400 mt-1">
            The provided URL is not from an allowed domain.
          </p>
        </div>
      )}
      {!showCmaHighlight && urlSafe && (
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
