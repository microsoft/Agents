# tests.Dockerfile
#
# Multi-runtime test image. Agents run as subprocesses at test time (via
# run_agent.ps1), so Python, Node, .NET, and PowerShell are all present.
# Nothing agent-specific is baked in — the repo is mounted at /repo at runtime.
#
# Build:  docker build -f tests.Dockerfile -t agents-test .  (from environments/local/)
# Run:    ./scripts/run_local.ps1

# .NET SDK 8 + PowerShell are pre-installed in this image, eliminating the need
# for the Microsoft package repo and its signing key.
FROM mcr.microsoft.com/dotnet/sdk:8.0

ENV DEBIAN_FRONTEND=noninteractive

# Base tools + Node.js 22
RUN apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates git \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

# uv — installed via its own installer (no Python required)
RUN curl -LsSf https://astral.sh/uv/install.sh | sh
ENV PATH="/root/.local/bin:$PATH"

# Python 3.13 managed by uv
RUN uv python install 3.13

WORKDIR /repo
# Keep the venv inside the container, not on the mounted Windows volume.
# Without this, uv tries to recreate /repo/.venv (Windows-style Scripts/ dir)
# and hits cross-OS I/O errors.
ENV UV_PROJECT_ENVIRONMENT=/venv
ENTRYPOINT ["uv", "run", "pytest"]
