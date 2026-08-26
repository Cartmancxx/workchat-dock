#!/bin/zsh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
APP_NAME="WorkChat Dock"
APP_DIR="$ROOT/dist/$APP_NAME.app"

cd "$ROOT"
swift build -c release --arch arm64
BIN_DIR="$(swift build -c release --arch arm64 --show-bin-path)"

rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"
cp "$BIN_DIR/WorkChatDockMac" "$APP_DIR/Contents/MacOS/WorkChatDockMac"

cat > "$APP_DIR/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key><string>zh_CN</string>
    <key>CFBundleExecutable</key><string>WorkChatDockMac</string>
    <key>CFBundleIdentifier</key><string>com.workchatdock.mac</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>CFBundleName</key><string>WorkChat Dock</string>
    <key>CFBundleDisplayName</key><string>WorkChat Dock</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>2.0.0</string>
    <key>CFBundleVersion</key><string>2</string>
    <key>LSMinimumSystemVersion</key><string>13.0</string>
    <key>LSUIElement</key><true/>
    <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

chmod +x "$APP_DIR/Contents/MacOS/WorkChatDockMac"
codesign --force --deep --sign - "$APP_DIR"

cd "$ROOT/dist"
ditto -c -k --sequesterRsrc --keepParent "$APP_NAME.app" "WorkChatDock-macOS-arm64.zip"
echo "Built: $APP_DIR"
echo "Archive: $ROOT/dist/WorkChatDock-macOS-arm64.zip"
