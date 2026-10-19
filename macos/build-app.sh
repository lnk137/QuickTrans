#!/bin/zsh
set -euo pipefail

ROOT="${0:A:h}/QuickTransMac"
OUT="${1:-${ROOT:h}/dist}"
APP="$OUT/QuickTrans.app"

mkdir -p "$OUT"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

if ! command -v xcodebuild >/dev/null 2>&1 || [[ "$(xcode-select -p 2>/dev/null || true)" == "/Library/Developer/CommandLineTools" ]]; then
  echo "需要安装 Xcode 15+ 后再运行此脚本。" >&2
  echo "当前只有 Command Line Tools，无法链接 AppKit 应用。" >&2
  exit 2
fi

cd "$ROOT"
swift build -c release
cp ".build/release/QuickTransMac" "$APP/Contents/MacOS/QuickTransMac"
cp Info.plist "$APP/Contents/Info.plist"
SOURCE_PNG="$ROOT/../../src/QuickTrans/Assets/QuickTrans.png"
cp "$SOURCE_PNG" "$APP/Contents/Resources/QuickTrans.png"
ICON_TMP="$(mktemp -d)"
trap 'find "$ICON_TMP" -type f -delete; find "$ICON_TMP" -depth -type d -empty -delete' EXIT
ICONSET="$ICON_TMP/QuickTrans.iconset"
mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$SOURCE_PNG" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
  sips -z "$((size * 2))" "$((size * 2))" "$SOURCE_PNG" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/QuickTrans.icns"
chmod +x "$APP/Contents/MacOS/QuickTransMac"

# Sign the complete bundle, including Info.plist and resources, after assembly.
# Distribution without Gatekeeper prompts requires a Developer ID and notarization.
codesign --force --sign - "$APP"
codesign --verify --deep --strict --verbose=2 "$APP"

echo "已生成：$APP"
