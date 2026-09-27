#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts/release"
mkdir -p "$OUT"
dotnet restore "$ROOT/LwXLab.Data.sln"
dotnet build "$ROOT/LwXLab.Data.sln" -c Release --no-restore
dotnet test "$ROOT/LwXLab.Data.sln" -c Release --no-build --no-restore
find "$ROOT/src" -name '*.csproj' -print0 | while IFS= read -r -d '' p; do
  dotnet pack "$p" -c Release --no-build --no-restore -o "$OUT"
done
dotnet pack "$ROOT/tools/dotnet-lwx-data/dotnet-lwx-data.csproj" -c Release --no-build --no-restore -o "$OUT"
pwsh -NoProfile -File "$ROOT/build/Test-Packages.ps1" -PackageDirectory "$OUT"
pwsh -NoProfile -File "$ROOT/build/Test-PackageConsumers.ps1" -PackageDirectory "$OUT"
echo "Packages: $OUT"
