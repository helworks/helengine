#!/bin/bash
# Build the macOS recorder only from the real checkout into its fixed output.
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd -P)"
output=/private/tmp/helengine/macos/recording
app="$output/Helengine Recorder.app"
mkdir -p "$output/module-cache" "$output/package-cache" "$output/package-config" "$output/package-security" "$app/Contents/MacOS"
export CLANG_MODULE_CACHE_PATH="$output/module-cache"
export SWIFTPM_MODULECACHE_OVERRIDE="$output/module-cache"
args=(--package-path "$repo/tools/window-recorder-apple" --scratch-path "$output/cli" --cache-path "$output/package-cache" --config-path "$output/package-config" --security-path "$output/package-security" --jobs 2 -c release)
/usr/bin/xcrun swift build "${args[@]}"
bin="$(/usr/bin/xcrun swift build "${args[@]}" --show-bin-path)"
/usr/bin/install -m 755 "$bin/helengine-window-recorder" "$app/Contents/MacOS/helengine-window-recorder"
cat > "$app/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>com.helworks.window-recorder</string>
<key>CFBundleName</key><string>Helengine Recorder</string>
<key>CFBundleDisplayName</key><string>Helengine Recorder</string>
<key>CFBundleExecutable</key><string>helengine-window-recorder</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>1.0</string>
<key>CFBundleVersion</key><string>1</string>
<key>LSMinimumSystemVersion</key><string>14.0</string>
<key>LSUIElement</key><true/>
<key>NSScreenCaptureUsageDescription</key><string>Record the explicitly selected DemoDisc menu window as a video.</string>
</dict></plist>
PLIST
sign_identity="${HELENGINE_RECORDING_SIGN_IDENTITY:--}"
/usr/bin/codesign --force --sign "$sign_identity" --options runtime --identifier com.helworks.window-recorder "$app"
/usr/bin/codesign --verify --deep --strict "$app"
printf '%s\n' "$app"
