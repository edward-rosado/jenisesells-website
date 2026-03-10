interface PaymentCardProps {
  onPaymentComplete: () => void;
}

export function PaymentCard({ onPaymentComplete }: PaymentCardProps) {
  return (
    <div className="bg-gray-800 rounded-xl p-5 max-w-sm space-y-3 text-center">
      <h3 className="text-2xl font-bold text-white">$900</h3>
      <p className="text-gray-400">One-time payment. Everything included.</p>
      <button
        onClick={onPaymentComplete}
        className="w-full px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold transition-colors"
      >
        Start Free Trial
      </button>
      <p className="text-xs text-gray-500">
        7-day trial. No charge until trial ends.
      </p>
    </div>
  );
}
