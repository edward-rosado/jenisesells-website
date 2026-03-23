interface CmaProgressStep {
  label: string;
  status: "done" | "active" | "pending";
}

interface CmaProgressCardProps {
  address: string;
  recipientEmail: string;
  status: "running" | "complete" | "failed";
  steps: CmaProgressStep[];
}

function StepIcon({ status }: { status: CmaProgressStep["status"] }) {
  if (status === "done") {
    return (
      <span className="flex h-5 w-5 items-center justify-center rounded-full bg-emerald-500 text-white text-xs">
        &#10003;
      </span>
    );
  }
  if (status === "active") {
    return (
      <span className="flex h-5 w-5 items-center justify-center rounded-full border-2 border-emerald-500">
        <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse" />
      </span>
    );
  }
  return (
    <span className="flex h-5 w-5 items-center justify-center rounded-full border-2 border-gray-600" />
  );
}

export function CmaProgressCard({
  address,
  recipientEmail,
  status,
  steps,
}: CmaProgressCardProps) {
  const isComplete = status === "complete";
  const isFailed = status === "failed";

  return (
    <div className="bg-gray-800 rounded-xl p-5 max-w-md space-y-4">
      <div className="space-y-1">
        <h3 className="font-semibold text-white">
          {isComplete ? "CMA Report Delivered" : isFailed ? "CMA Report Failed" : "Generating CMA Report..."}
        </h3>
        <p className="text-sm text-gray-400">{address}</p>
      </div>

      <ul className="space-y-2" role="list" aria-label="CMA pipeline progress">
        {steps.map((step) => (
          <li key={step.label} className="flex items-center gap-3">
            <StepIcon status={step.status} />
            <span
              className={`text-sm ${
                step.status === "done"
                  ? "text-gray-300"
                  : step.status === "active"
                    ? "text-emerald-400 font-medium"
                    : "text-gray-500"
              }`}
            >
              {step.label}
            </span>
          </li>
        ))}
      </ul>

      {isComplete && (
        <div className="rounded-lg bg-emerald-900/30 border border-emerald-700/50 px-4 py-3">
          <p className="text-sm text-emerald-300">
            Report sent to <span className="font-medium">{recipientEmail}</span>
          </p>
        </div>
      )}

      {isFailed && (
        <div className="rounded-lg bg-red-900/30 border border-red-700/50 px-4 py-3">
          <p className="text-sm text-red-300">
            The CMA pipeline encountered an error. The team will investigate.
          </p>
        </div>
      )}
    </div>
  );
}
