#!/usr/bin/env bash
# Kill any running RealEstateStar.Api process and restart it
set -e

PID=$(tasklist 2>/dev/null | grep -i "RealEstateStar.Api" | awk '{print $2}' | head -1)
if [ -n "$PID" ]; then
    echo "Killing RealEstateStar.Api (PID $PID)..."
    taskkill //PID "$PID" //F 2>/dev/null || true
fi

cd "$(dirname "$0")/../RealEstateStar.Api"
echo "Starting API..."
exec dotnet run
