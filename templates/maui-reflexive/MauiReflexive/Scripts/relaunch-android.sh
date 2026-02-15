#!/bin/bash
# Builds and deploys the MAUI app to an Android emulator.
# Requires: Android SDK, emulator running.

set -e

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT_NAME="$(basename "$PROJECT_DIR")"

echo "🔨 Building for Android..."
cd "$PROJECT_DIR"

BUILD_OUTPUT=$(dotnet build "$PROJECT_NAME.csproj" -f net10.0-android 2>&1)
BUILD_EXIT_CODE=$?

if [ $BUILD_EXIT_CODE -ne 0 ]; then
    echo "❌ BUILD FAILED!"
    echo "$BUILD_OUTPUT" | grep -A 5 "error " || echo "$BUILD_OUTPUT" | tail -30
    exit 1
fi

echo "$BUILD_OUTPUT" | tail -3

# Check for connected device/emulator
ADB_DEVICES=$(adb devices | grep -v "^List" | grep -v "^$" | head -1)
if [ -z "$ADB_DEVICES" ]; then
    echo "❌ No Android device/emulator connected. Start an emulator first."
    exit 1
fi

echo "📱 Installing to Android device..."
APK_PATH=$(find "$PROJECT_DIR/bin/Debug/net10.0-android" -name "*-Signed.apk" | head -1)

if [ -z "$APK_PATH" ]; then
    APK_PATH=$(find "$PROJECT_DIR/bin/Debug/net10.0-android" -name "*.apk" | head -1)
fi

if [ -z "$APK_PATH" ]; then
    echo "❌ APK not found."
    exit 1
fi

adb install -r "$APK_PATH"

# Set up port forwarding for maui-devflow
DEVFLOW_PORT=$(python3 -c "import json; print(json.load(open('$PROJECT_DIR/.mauidevflow')).get('port', 9223))" 2>/dev/null || echo "9223")
adb reverse tcp:$DEVFLOW_PORT tcp:$DEVFLOW_PORT 2>/dev/null || true

# Launch the app
PACKAGE_NAME="com.companyname.$(echo "$PROJECT_NAME" | tr '[:upper:]' '[:lower:]')"
adb shell am start -n "$PACKAGE_NAME/crc64*.MainActivity" 2>/dev/null || \
    adb shell monkey -p "$PACKAGE_NAME" 1

echo "✅ Deployed and launched on Android!"
