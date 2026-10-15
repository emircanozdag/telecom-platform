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

step "Waiting for events to flow through Kafka and RabbitMQ..."
sleep 8

step "Invoice (Billing service, built from Kafka events)"
curl -s "$GATEWAY/api/billing/$ID/invoices" | json

step "Notifications sent (Notification service, via RabbitMQ)"
curl -s "$GATEWAY/api/notifications/$ID" | json

printf '\nSubscriber ID: %s\n' "$ID"
