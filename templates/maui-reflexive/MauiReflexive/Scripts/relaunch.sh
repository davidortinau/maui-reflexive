#!/bin/bash
# Builds the MAUI app, stages it, launches the new instance, then kills the old one.
# If build fails, the old app instance remains running.

set -e

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT_NAME="$(basename "$PROJECT_DIR")"
BUILD_DIR="$PROJECT_DIR/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64"
APP_NAME="$PROJECT_NAME.app"
STAGING_DIR="$PROJECT_DIR/bin/staging"
MAX_LAUNCH_ATTEMPTS=2
STABILITY_SECONDS=8

# Capture PIDs of currently running instances BEFORE launch
OLD_PIDS=$(ps -eo pid,comm | grep "$PROJECT_NAME" | grep -v grep | grep -v ".csproj" | awk '{print $1}' | tr '\n' ' ')

echo "🔨 Building..."
cd "$PROJECT_DIR"

BUILD_OUTPUT=$(dotnet build "$PROJECT_NAME.csproj" -f net10.0-maccatalyst 2>&1)
BUILD_EXIT_CODE=$?

if [ $BUILD_EXIT_CODE -ne 0 ]; then
    echo "❌ BUILD FAILED!"
    echo ""
    echo "Error details:"
    echo "$BUILD_OUTPUT" | grep -A 5 "error CS" || echo "$BUILD_OUTPUT" | tail -30
    echo ""
    echo "Old app instance remains running."
    exit 1
fi

echo "$BUILD_OUTPUT" | tail -3

echo "📦 Copying to staging..."
rm -rf "$STAGING_DIR/$APP_NAME"
mkdir -p "$STAGING_DIR"
ditto "$BUILD_DIR/$APP_NAME" "$STAGING_DIR/$APP_NAME"

for ATTEMPT in $(seq 1 "$MAX_LAUNCH_ATTEMPTS"); do
    echo "🚀 Launching new instance (attempt $ATTEMPT/$MAX_LAUNCH_ATTEMPTS)..."
    mkdir -p ~/.maui-reflexive
    nohup "$STAGING_DIR/$APP_NAME/Contents/MacOS/$PROJECT_NAME" > ~/.maui-reflexive/console.log 2>&1 &
    NEW_PID=$!

    if [ -z "$NEW_PID" ]; then
        echo "⚠️  Failed to start new instance."
        if [ "$ATTEMPT" -lt "$MAX_LAUNCH_ATTEMPTS" ]; then
            echo "🔁 Retrying..."
            continue
        fi
        echo "Old instance left running."
        exit 1
    fi

    echo "✅ New instance running (PID $NEW_PID)"
    echo "🔎 Verifying stability for ${STABILITY_SECONDS}s..."
    STABLE=true
    for i in $(seq 1 "$STABILITY_SECONDS"); do
        sleep 1
        ACTIVE_NEW_PID=$(ps -eo pid,comm | grep "$PROJECT_NAME" | grep -v grep | grep -v ".csproj" | awk '{print $1}' | while read -r PID; do
            if ! echo "$OLD_PIDS" | grep -qw "$PID"; then
                echo "$PID"
                break
            fi
        done)
        if [ -z "$ACTIVE_NEW_PID" ]; then
            STABLE=false
            break
        fi
    done

    if [ "$STABLE" = true ]; then
        if [ -n "$OLD_PIDS" ]; then
            echo "🔪 Closing old instance(s)..."
            for OLD_PID in $OLD_PIDS; do
                echo "   Killing PID $OLD_PID"
                kill "$OLD_PID" 2>/dev/null || true
            done
        fi
        echo "✅ Handoff complete!"
        exit 0
    fi

    echo "❌ New instance crashed quickly (PID $NEW_PID)."
    if [ "$ATTEMPT" -lt "$MAX_LAUNCH_ATTEMPTS" ]; then
        echo "🔁 Retrying..."
        continue
    fi

    echo "⚠️  New instance unstable. Old instance left running."
    exit 1
done
