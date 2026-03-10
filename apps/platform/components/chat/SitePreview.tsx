interface SitePreviewProps {
  siteUrl: string;
  onApprove: () => void;
}

export function SitePreview({ siteUrl, onApprove }: SitePreviewProps) {
  return (
    <div className="bg-gray-800 rounded-xl p-4 max-w-lg space-y-3">
      <h3 className="font-semibold text-white">Your Site Preview</h3>
      <div className="rounded-lg overflow-hidden border border-gray-700">
        <iframe
          src={siteUrl}
          title="Site preview"
          className="w-full h-80"
          sandbox="allow-scripts allow-same-origin"
        />
      </div>
      <button
        onClick={onApprove}
        className="w-full px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold transition-colors"
      >
        Approve
      </button>
    </div>
  );
}
