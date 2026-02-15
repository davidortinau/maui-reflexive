#!/bin/bash
# Builds and deploys the MAUI app to an iOS simulator.
# Requires: Xcode, iOS simulator booted.

set -e

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT_NAME="$(basename "$PROJECT_DIR")"

echo "🔨 Building for iOS simulator..."
cd "$PROJECT_DIR"

BUILD_OUTPUT=$(dotnet build "$PROJECT_NAME.csproj" -f net10.0-ios -p:_DevicesOperatingSystem=ios -p:_DevicesArchitecture=x86_64 2>&1)
BUILD_EXIT_CODE=$?

if [ $BUILD_EXIT_CODE -ne 0 ]; then
    echo "❌ BUILD FAILED!"
    echo "$BUILD_OUTPUT" | grep -A 5 "error CS" || echo "$BUILD_OUTPUT" | tail -30
    exit 1
fi

echo "$BUILD_OUTPUT" | tail -3

# Find the booted simulator
BOOTED_SIM=$(xcrun simctl list devices booted -j | python3 -c "
import json,sys
data = json.load(sys.stdin)
for runtime, devices in data.get('devices', {}).items():
    for d in devices:
        if d.get('state') == 'Booted':
            print(d['udid'])
            sys.exit(0)
" 2>/dev/null)

if [ -z "$BOOTED_SIM" ]; then
    echo "❌ No booted iOS simulator found. Boot one first: xcrun simctl boot <device-udid>"
    exit 1
fi

echo "📱 Deploying to simulator $BOOTED_SIM..."
APP_PATH="$PROJECT_DIR/bin/Debug/net10.0-ios/iossimulator-x64/$PROJECT_NAME.app"

if [ ! -d "$APP_PATH" ]; then
    # Try arm64 (Apple Silicon)
    APP_PATH="$PROJECT_DIR/bin/Debug/net10.0-ios/iossimulator-arm64/$PROJECT_NAME.app"
fi

if [ ! -d "$APP_PATH" ]; then
    echo "❌ App bundle not found."
    exit 1
fi

xcrun simctl install "$BOOTED_SIM" "$APP_PATH"
BUNDLE_ID=$(defaults read "$APP_PATH/Info.plist" CFBundleIdentifier 2>/dev/null || echo "com.companyname.$PROJECT_NAME")
xcrun simctl launch "$BOOTED_SIM" "$BUNDLE_ID"

echo "✅ Deployed and launched on iOS simulator!"
