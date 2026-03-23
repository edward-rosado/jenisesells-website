interface GeometricStarProps {
  size: number;
  state?: "idle" | "thinking";
  className?: string;
}

const ANIMATION: Record<string, string> = {
  thinking: "star-spin 8s linear infinite",
  idle: "star-pulse 2s ease-in-out infinite",
};

export function GeometricStar({ size, state, className }: GeometricStarProps) {
  const animation = state ? ANIMATION[state] : "";
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 100 100"
      xmlns="http://www.w3.org/2000/svg"
      className={className}
      style={animation ? { animation } : undefined}
      role="img"
      aria-label="Real Estate Star logo"
    >
      {/* Outer star — stroke only */}
      <polygon
        points="50,8 61,36 92,36 67,55 76,84 50,67 24,84 33,55 8,36 39,36"
        fill="none"
        stroke="#10b981"
        strokeWidth="1.5"
      />
      {/* Inner star — solid fill */}
      <polygon
        points="50,22 57,40 76,40 61,51 67,70 50,59 33,70 39,51 24,40 43,40"
        fill="#10b981"
      />
    </svg>
  );
}
