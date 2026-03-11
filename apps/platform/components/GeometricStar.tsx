interface GeometricStarProps {
  className?: string;
}

export function GeometricStar({ className }: GeometricStarProps) {
  const classes = className ?? "w-8 h-8";
  return (
    <svg
      viewBox="0 0 100 100"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className={classes}
      role="img"
      aria-label="Real Estate Star logo"
    >
      {/* Five-pointed star with gradient fill */}
      <defs>
        <linearGradient id="star-gradient" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor="#34d399" />
          <stop offset="100%" stopColor="#059669" />
        </linearGradient>
      </defs>
      <path
        d="M50 5 L61 38 L95 38 L68 58 L79 91 L50 71 L21 91 L32 58 L5 38 L39 38 Z"
        fill="url(#star-gradient)"
      />
    </svg>
  );
}
