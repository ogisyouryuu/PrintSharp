#!/usr/bin/env bash
set -euo pipefail

# Rocky Linux で実際の HTTP 経路と公開ファイル一式を検証します。
repo=$(cd "$(dirname "$0")/.." && pwd)
cd "$repo"
port=${PRINTSHARP_VERIFY_PORT:-5098}
output=${1:-"$repo/output/pdf/rocky-verification"}
mkdir -p "$output"
output=$(cd "$output" && pwd)
publish="$repo/artifacts/skia-api"
demo="$repo/artifacts/skia-demo"
# Windows と WSL で NuGet の絶対パスを共有しないよう、中間生成物を分離します。
build_artifacts=${PRINTSHARP_VERIFY_ARTIFACTS:-/tmp/printsharp-pdf-verify-build}
dotnet publish src/PrintSharp.Api --artifacts-path "$build_artifacts" -c Release -r linux-x64 --self-contained true -o "$publish" -v quiet
dotnet publish demos/PrintSharp.Multilingual.Demo --artifacts-path "$build_artifacts" -c Release -r linux-x64 --self-contained true -o "$demo" -v quiet
if find "$publish" -iname '*pdfsharp*' -print -quit | grep -q .; then
    echo "Unexpected legacy PDF dependency in publish directory" >&2
    exit 1
fi
if grep -qi 'pdfsharp' "$publish/PrintSharp.Api.deps.json"; then exit 1; fi
(cd "$publish" && exec ./PrintSharp.Api --urls "http://127.0.0.1:$port") > "$output/api.log" 2>&1 &
api_pid=$!
trap 'kill "$api_pid" 2>/dev/null || true; wait "$api_pid" 2>/dev/null || true' EXIT
ready=false
for attempt in {1..60}; do
    if curl --fail --silent "http://127.0.0.1:$port/api/v1/health" > /dev/null; then
        ready=true
        break
    fi
    kill -0 "$api_pid"
    sleep 1
done
if [ "$ready" != true ]; then cat "$output/api.log"; exit 1; fi
"$demo/PrintSharp.Multilingual.Demo" "$output" "http://127.0.0.1:$port/"
cat /etc/os-release >> "$output/environment.txt"
pdffonts "$output/multilingual.pdf" > "$output/fonts.txt"
pdfimages -list "$output/multilingual.pdf" > "$output/images.txt"
pdfinfo "$output/multilingual.pdf" > "$output/pdfinfo.txt"
pdftotext -layout "$output/multilingual.pdf" "$output/extracted.txt"
pdftoppm -scale-to 1500 -png "$output/multilingual.pdf" "$output/page"
grep -q 'NotoSansArabic' "$output/fonts.txt"
# Poppler のフォント表は先頭二行が見出しです。
awk 'NR > 2 && $0 !~ /yes[[:space:]]+(yes|no)[[:space:]]+yes/ { exit 1 }' "$output/fonts.txt"
test "$(wc -l < "$output/images.txt")" -eq 2
echo "Verified embedded vector glyphs. Review PNG pages in: $output"
