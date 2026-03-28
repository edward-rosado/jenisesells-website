#!/usr/bin/env bash
# Run tests with code coverage and display per-class branch coverage report.
# Usage: bash scripts/coverage.sh [--low-only]
#   --low-only  Show only classes with < 100% branch coverage

set -euo pipefail
cd "$(dirname "$0")/.."

LOW_ONLY=false
[[ "${1:-}" == "--low-only" ]] && LOW_ONLY=true

# Clean previous results
rm -rf RealEstateStar.Api.Tests/TestResults

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include="[RealEstateStar.Api]*" \
  2>&1

# Find the coverage file
COVERAGE_FILE=$(find RealEstateStar.Api.Tests/TestResults -name "coverage.cobertura.xml" 2>/dev/null | head -1)

if [[ -z "$COVERAGE_FILE" ]]; then
  echo "ERROR: No coverage file found"
  exit 1
fi

# Extract overall rates
LINE_RATE=$(grep -oP 'line-rate="\K[^"]+' "$COVERAGE_FILE" | head -1)
BRANCH_RATE=$(grep -oP 'branch-rate="\K[^"]+' "$COVERAGE_FILE" | head -1)

echo ""
echo "============================================"
echo "  COVERAGE SUMMARY"
echo "============================================"
printf "  Line coverage:   %.1f%%\n" "$(awk "BEGIN {printf \"%.1f\", $LINE_RATE * 100}")"
printf "  Branch coverage: %.1f%%\n" "$(awk "BEGIN {printf \"%.1f\", $BRANCH_RATE * 100}")"
echo "============================================"

# Per-class breakdown using single grep that extracts name + branch-rate together
echo ""
if $LOW_ONLY; then
  echo "Classes with < 100% branch coverage:"
else
  echo "Per-class branch coverage:"
fi
echo "--------------------------------------------"

grep -oP 'class name="\K[^"]+(?=" filename="[^"]*" line-rate="[^"]*" branch-rate=")' "$COVERAGE_FILE" \
  | while read -r name; do
    # Skip generated/async state machine classes
    [[ "$name" == System.Text.RegularExpressions* ]] && continue
    [[ "$name" == *"<"* ]] && continue
    [[ "$name" == *"__"* ]] && continue
    [[ -z "$name" ]] && continue
    echo "$name"
done | sort -u | while read -r name; do
  # Get branch-rate for this specific class
  rate=$(grep -oP "class name=\"${name//./\\.}\" filename=\"[^\"]*\" line-rate=\"[^\"]*\" branch-rate=\"\K[^\"]*" "$COVERAGE_FILE" | head -1)
  [[ -z "$rate" ]] && continue

  pct=$(awk "BEGIN {printf \"%.1f\", $rate * 100}")
  if $LOW_ONLY; then
    [[ "$rate" == "1" ]] && continue
  fi
  printf "  %-60s %5.1f%%\n" "$name" "$pct"
done

echo "--------------------------------------------"
