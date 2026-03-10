interface ProfileCardProps {
  name: string;
  brokerage?: string;
  state?: string;
  photoUrl?: string;
  homesSold?: number;
  avgRating?: number;
  onConfirm: () => void;
}

export function ProfileCard({
  name,
  brokerage,
  state,
  photoUrl,
  homesSold,
  avgRating,
  onConfirm,
}: ProfileCardProps) {
  return (
    <div className="bg-gray-800 rounded-xl p-5 max-w-sm space-y-3">
      <div className="flex items-center gap-3">
        {photoUrl && (
          <img
            src={photoUrl}
            alt={name}
            className="h-12 w-12 rounded-full object-cover"
          />
        )}
        <div>
          <h3 className="font-semibold text-white">{name}</h3>
          {brokerage && (
            <p className="text-sm text-gray-400">{brokerage}</p>
          )}
        </div>
      </div>
      <div className="flex gap-4 text-sm text-gray-300">
        {state && <span>{state}</span>}
        {homesSold != null && <span>{homesSold} homes sold</span>}
        {avgRating != null && <span>{avgRating} avg rating</span>}
      </div>
      <button
        onClick={onConfirm}
        className="w-full px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold transition-colors"
      >
        Looks right
      </button>
    </div>
  );
}
