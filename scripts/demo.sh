#!/usr/bin/env bash
# Walks through the main flow against a running stack (docker compose up).
# Usage: ./scripts/demo.sh
set -euo pipefail

GATEWAY="${GATEWAY:-http://localhost:8000}"

step() { printf '\n\033[1;35m==> %s\033[0m\n' "$1"; }
json() { python3 -m json.tool; }

step "Available plans"
curl -s "$GATEWAY/api/plans" | json

# Random number so the demo can be run repeatedly (MSISDNs are unique).
MSISDN="90532$(printf '%07d' $((RANDOM * RANDOM % 10000000)))"

step "Create a subscriber on the Mini 5 GB plan ($MSISDN)"
SUBSCRIBER=$(curl -s -X POST "$GATEWAY/api/subscribers" \
  -H 'Content-Type: application/json' \
  -d "{\"msisdn\":\"$MSISDN\",\"email\":\"ayse@example.com\",\"fullName\":\"Ayse Yilmaz\",\"planCode\":\"MINI-5GB\"}")
echo "$SUBSCRIBER" | json
ID=$(echo "$SUBSCRIBER" | python3 -c "import sys, json; print(json.load(sys.stdin)['id'])")

step "Use 4600 MB (~90% of 5120 MB) -> crosses the 80% threshold"
curl -s -X POST "$GATEWAY/api/subscribers/$ID/usage" -H 'Content-Type: application/json' -d '{"megabytes":4600}' | json

step "Use 1000 MB more -> quota exceeded, 480 MB billed as overage"
curl -s -X POST "$GATEWAY/api/subscribers/$ID/usage" -H 'Content-Type: application/json' -d '{"megabytes":1000}' | json

# Billing and notifications are eventually consistent: poll until the events have been processed.
wait_for() { # url, python expression on the parsed JSON that must be true
  for _ in $(seq 1 30); do
    body=$(curl -s "$1")
    if echo "$body" | python3 -c "import sys, json; d = json.load(sys.stdin); sys.exit(0 if ($2) else 1)" 2>/dev/null; then
      echo "$body" | json
      return
    fi
    sleep 1
  done
  echo "Timed out waiting for $1" >&2
  echo "$body" | json
}

step "Invoice (Billing service, built from Kafka events)"
wait_for "$GATEWAY/api/billing/$ID/invoices" "len(d) == 1 and d[0]['usageRecordCount'] == 2"

step "Notifications sent (Notification service, via RabbitMQ)"
wait_for "$GATEWAY/api/notifications/$ID" "len(d) == 4"

printf '\nSubscriber ID: %s\n' "$ID"
