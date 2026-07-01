#!/bin/bash
# Updates the bundled disposable email domain blocklist from the upstream source.
# Run manually or via GitHub Actions (.github/workflows/update-disposable-domains.yml).

set -euo pipefail

DEST="src/HookSentry.Api/Resources/disposable-domains.txt"
URL="https://raw.githubusercontent.com/disposable-email-domains/disposable-email-domains/master/disposable_email_blocklist.conf"

curl -fsSL "$URL" -o "$DEST"
echo "Updated: $(wc -l < "$DEST") domains"
