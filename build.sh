#!/usr/bin/env bash
# Builds ProjectPinner.exe (a tiny .NET Framework 4.8 single-file Windows app) and
# copies it to dist/. Works on Linux/macOS/Windows with the .NET SDK installed -
# no Windows or Visual Studio required (net48 ref assemblies come from NuGet).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ="$ROOT/src/ProjectPinner/ProjectPinner.csproj"
OUT="$ROOT/src/ProjectPinner/bin/Release/ProjectPinner.exe"

echo "Building ProjectPinner (Release, net48)…"
dotnet build "$PROJ" -c Release -nologo

mkdir -p "$ROOT/dist"
cp "$OUT" "$ROOT/dist/ProjectPinner.exe"
echo "Done -> dist/ProjectPinner.exe ($(du -h "$ROOT/dist/ProjectPinner.exe" | cut -f1))"
