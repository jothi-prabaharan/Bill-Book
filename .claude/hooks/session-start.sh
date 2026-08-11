#!/bin/bash
# Installs what a Claude Code on the web session needs to build this repository:
# the frontend's npm packages and the .NET 10 SDK.
#
# Deliberately tolerant of a failed SDK install. Some environments block
# builds.dotnet.microsoft.com by egress policy, and a session that cannot build
# the backend is still useful for the frontend, the docs app and everything
# else — so a blocked download prints what to do and carries on rather than
# stopping the session from starting.
set -uo pipefail

# Local machines already have their own toolchain; this is for the remote one.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
DOTNET_DIR="${DOTNET_ROOT:-$HOME/.dotnet}"

# ---------------------------------------------------------------- frontend
# `npm ci`, not `npm install`, and only when the tree does not already match the
# lockfile.
#
# install was here first, for the right reason — the container image is cached
# after this script finishes, so a warm start should not reinstall. But install
# *rewrites* `package-lock.json` as a side effect: it re-resolves anything the
# lock leaves room for and rewrites peer markers, so every session began with a
# dirty working tree and a lockfile diff nobody asked for. That defeats the
# reason the lockfile is committed at all (master.md 5.15) — a lock that changes
# on its own is not a lock.
#
# The stamp keeps the speed. `npm ci` runs on a cold container or after the
# lockfile genuinely changes, and a warm start with an unchanged lock does
# nothing at all, which is faster than install's no-op.
if [ -f "$PROJECT_DIR/frontend/package.json" ]; then
  FRONTEND="$PROJECT_DIR/frontend"
  LOCK_HASH="$(sha256sum "$FRONTEND/package-lock.json" 2>/dev/null | cut -d' ' -f1)"
  STAMP="$FRONTEND/node_modules/.installed-lock-hash"

  if [ -n "$LOCK_HASH" ] && [ -d "$FRONTEND/node_modules" ] \
      && [ "$(cat "$STAMP" 2>/dev/null)" = "$LOCK_HASH" ]; then
    echo "==> Frontend packages already match the lockfile"
  else
    echo "==> Installing frontend packages"
    if (cd "$FRONTEND" && npm ci --no-audit --no-fund); then
      [ -n "$LOCK_HASH" ] && printf '%s\n' "$LOCK_HASH" > "$STAMP"
    else
      # ci refuses when package.json and the lock disagree. Say which it is
      # rather than falling back to install and quietly editing the lock.
      echo "!! npm ci failed — the Angular apps will not build."
      echo "!! If it reported the lockfile is out of sync, run 'npm install' in"
      echo "!! frontend/ by hand and commit the updated package-lock.json."
    fi
  fi
fi

# ---------------------------------------------------------------- .NET SDK
# backend/Directory.Build.props targets net10.0, so the 10.0 channel is what
# the solution needs; an older SDK cannot restore it.
if command -v dotnet >/dev/null 2>&1; then
  echo "==> .NET SDK already present: $(dotnet --version)"
elif [ -x "$DOTNET_DIR/dotnet" ]; then
  echo "==> .NET SDK already installed at $DOTNET_DIR"
else
  echo "==> Installing the .NET 10 SDK"
  INSTALLER="$(mktemp)"

  # The distribution repository first. It carries dotnet-sdk-10.0 and is a
  # permitted source; dot.net and builds.dotnet.microsoft.com are denied by the
  # egress policy in some environments, and every session before this one
  # concluded from that one failure that the backend could not be compiled at
  # all. The index is often stale, hence the update.
  if apt-get update >/dev/null 2>&1 && apt-get install -y dotnet-sdk-10.0 >/dev/null 2>&1 \
      && command -v dotnet >/dev/null 2>&1; then
    echo "==> Installed the .NET SDK from the distribution repository"
  elif curl -fsSL --retry 2 https://dot.net/v1/dotnet-install.sh -o "$INSTALLER"; then
    bash "$INSTALLER" --channel 10.0 --install-dir "$DOTNET_DIR" --no-path \
      || echo "!! dotnet-install.sh failed."
  else
    cat <<'BLOCKED'
!! Neither the distribution package nor dotnet-install.sh could be obtained.
!! If this was a 403, the egress policy for this session does not allow
!!   dot.net / builds.dotnet.microsoft.com
!! Ask for those hosts to be allowed, then start a new session. Do not work
!! around it — the proxy README at /root/.ccr/README.md says to report the
!! blocked host rather than route around it.
!! The frontend and docs still build; only the backend is affected.
BLOCKED
  fi

  rm -f "$INSTALLER"
fi

# ---------------------------------------------------------------- environment
# Persisted for the session so dotnet is on PATH in every later shell.
if [ -n "${CLAUDE_ENV_FILE:-}" ] && [ -x "$DOTNET_DIR/dotnet" ]; then
  {
    echo "export DOTNET_ROOT=\"$DOTNET_DIR\""
    echo "export PATH=\"$DOTNET_DIR:\$DOTNET_DIR/tools:\$PATH\""
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
    echo 'export DOTNET_NOLOGO=1'
  } >> "$CLAUDE_ENV_FILE"

  export DOTNET_ROOT="$DOTNET_DIR"
  export PATH="$DOTNET_DIR:$DOTNET_DIR/tools:$PATH"

  # dotnet-ef is needed to add or verify migrations, which is how the
  # hand-written ones in this repository get checked against their models.
  if ! "$DOTNET_DIR/dotnet" tool list --global 2>/dev/null | grep -q dotnet-ef; then
    echo "==> Installing dotnet-ef"
    "$DOTNET_DIR/dotnet" tool install --global dotnet-ef \
      || echo "!! dotnet-ef install failed — 'dotnet ef' will not be available."
  fi
fi

echo "==> Session setup finished"
