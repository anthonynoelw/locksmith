#!/bin/bash
set -euo pipefail

HOOK_DIR=".husky"
COMMIT_MSG_HOOK="$HOOK_DIR/commit-msg"

check_command() {
    if ! command -v "$1" &>/dev/null; then
        echo "Error: $1 is not installed. $2"
        exit 1
    fi
}

echo "Checking prerequisites..."
check_command node "Install from https://nodejs.org/"
check_command dotnet "Install from https://dot.net/"
echo "  Node.js $(node --version)"
echo "  .NET    $(dotnet --version)"

echo ""
echo "Installing npm dependencies..."
npm install

echo ""
echo "Configuring git hooks..."
mkdir -p "$HOOK_DIR"
if [ ! -f "$COMMIT_MSG_HOOK" ]; then
    printf 'npx --no -- commitlint --edit "$1"\n' > "$COMMIT_MSG_HOOK"
    chmod +x "$COMMIT_MSG_HOOK"
    echo "  Created $COMMIT_MSG_HOOK"
else
    echo "  $COMMIT_MSG_HOOK already exists"
fi

echo ""
echo "Restoring .NET packages..."
dotnet restore

echo ""
echo "Setup complete."
