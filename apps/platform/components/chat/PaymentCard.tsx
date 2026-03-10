import { useState } from "react";

interface PaymentCardProps {
  checkoutUrl?: string;
  price?: string;
}

export function PaymentCard({ checkoutUrl, price = "$900" }: PaymentCardProps) {
  const [opened, setOpened] = useState(false);

  function handleClick() {
    if (checkoutUrl) {
      window.open(checkoutUrl, "_blank", "noopener,noreferrer");
      setOpened(true);
    }
  }

  return (
    <div className="bg-gray-800 rounded-xl p-5 max-w-sm space-y-3 text-center">
      <h3 className="text-2xl font-bold text-white">{price}</h3>
      <p className="text-gray-400">One-time setup fee. Everything included.</p>
      {opened ? (
        <p className="text-emerald-400 text-sm animate-pulse">
          Waiting for payment confirmation...
        </p>
      ) : (
        <button
          onClick={handleClick}
          disabled={!checkoutUrl}
          className="w-full px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold transition-colors disabled:opacity-50"
        >
          Start Free Trial
        </button>
      )}
      <p className="text-xs text-gray-500">
        7-day free trial. No charge until trial ends.
      </p>
    </div>
  );
}
