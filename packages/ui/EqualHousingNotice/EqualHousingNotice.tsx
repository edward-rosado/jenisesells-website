export interface EqualHousingNoticeProps {
  agentState?: string;
  textColor?: string;
}

const FEDERAL_CLASSES =
  "race, color, religion, sex, national origin, disability, familial status";

const NJ_CLASSES =
  "race, color, religion, sex, national origin, disability, familial status, " +
  "ancestry, marital status, domestic partnership or civil union status, " +
  "affectional or sexual orientation, gender identity or expression, " +
  "or source of lawful income";

function getComplianceText(agentState?: string): string {
  if (agentState === "NJ") {
    return (
      "We are committed to compliance with federal and New Jersey fair housing laws. " +
      `We do not discriminate on the basis of ${NJ_CLASSES}.`
    );
  }
  return (
    "We are committed to compliance with federal fair housing laws. " +
    `We do not discriminate on the basis of ${FEDERAL_CLASSES}.`
  );
}

export function EqualHousingNotice({
  agentState,
  textColor = "rgba(255,255,255,0.7)",
}: EqualHousingNoticeProps) {
  const statementColor = textColor;

  return (
    <div>
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          gap: "8px",
          fontSize: "11px",
          color: textColor,
        }}
      >
        <svg
          aria-label="Equal Housing Opportunity logo"
          role="img"
          width="20"
          height="20"
          viewBox="0 0 64 64"
          fill="currentColor"
          xmlns="http://www.w3.org/2000/svg"
        >
          <path d="M32 4L2 30h10v28h40V30h10L32 4zm-12 48V28h24v24H20z" />
          <rect x="24" y="32" width="16" height="4" />
          <rect x="24" y="40" width="16" height="4" />
        </svg>
        <span>Equal Housing Opportunity</span>
      </div>
      <p
        style={{
          fontSize: "11px",
          color: statementColor,
          maxWidth: "700px",
          margin: "8px auto 0",
          lineHeight: 1.5,
          textAlign: "center",
        }}
      >
        {getComplianceText(agentState)}
      </p>
    </div>
  );
}
