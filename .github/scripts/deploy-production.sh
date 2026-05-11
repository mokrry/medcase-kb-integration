#!/usr/bin/env bash
set -euo pipefail

APP_DIR="/opt/med-module"
ARCHIVE_PATH="/tmp/med-module-deploy.tar.gz"
RELEASE_DIR="/tmp/med-module-release"

if [ ! -f "$ARCHIVE_PATH" ]; then
  echo "Deployment archive not found: $ARCHIVE_PATH" >&2
  exit 1
fi

rm -rf "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR" "$APP_DIR"
tar -xzf "$ARCHIVE_PATH" -C "$RELEASE_DIR"

if [ ! -f "$APP_DIR/.env" ]; then
  cp "$RELEASE_DIR/.env.production.example" "$APP_DIR/.env"
  echo "Created $APP_DIR/.env from example. Fill production secrets and rerun deployment." >&2
  exit 1
fi

rm -rf \
  "$APP_DIR/backend" \
  "$APP_DIR/frontend" \
  "$APP_DIR/docker-compose.prod.yml" \
  "$APP_DIR/.env.production.example" \
  "$APP_DIR/DEPLOY.md"

cp -a "$RELEASE_DIR"/. "$APP_DIR"/

cd "$APP_DIR"
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
docker image prune -f
