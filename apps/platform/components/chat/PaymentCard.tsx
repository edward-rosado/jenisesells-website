interface PaymentCardProps {
  checkoutUrl?: string;
  onPaymentComplete: () => void;
}

export function PaymentCard({ checkoutUrl, onPaymentComplete }: PaymentCardProps) {
  function handleClick() {
    if (checkoutUrl) {
      window.open(checkoutUrl, "_blank", "noopener,noreferrer");
    }
    onPaymentComplete();
  }

  return (
    <div className="bg-gray-800 rounded-xl p-5 max-w-sm space-y-3 text-center">
      <h3 className="text-2xl font-bold text-white">$900</h3>
      <p className="text-gray-400">One-time setup fee. Everything included.</p>
      <button
        onClick={handleClick}
        className="w-full px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold transition-colors"
      >
        Start Free Trial
      </button>
      <p className="text-xs text-gray-500">
        7-day free trial. No charge until trial ends.
      </p>
    </div>
  );
}
