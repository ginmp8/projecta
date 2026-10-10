#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")/.."
solution=Projecta.slnx
case "${1:-}" in
  --core-only) solution=Projecta.Core.slnx ;;
  '') ;;
  *) echo 'Usage: scripts/pack.sh [--core-only]' >&2; exit 2 ;;
esac
if [ "$#" -gt 1 ]; then exit 2; fi
dotnet pack "$solution" -c Release --no-build -o artifacts/packages
