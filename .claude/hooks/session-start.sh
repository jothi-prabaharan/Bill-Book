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
# npm install rather than npm ci: the container image is cached after this
# script finishes, and install reuses that cache where ci would discard it.
if [ -f "$PROJECT_DIR/frontend/package.json" ]; then
  echo "==> Installing frontend packages"
  (cd "$PROJECT_DIR/frontend" && npm install --no-audit --no-fund) \
    || echo "!! npm install failed — the Angular apps will not build."
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
