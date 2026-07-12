FROM mcr.microsoft.com/devcontainers/dotnet:1-9.0-bookworm

# Some environments include a Yarn apt source without the required public key.
# Removing it avoids apt update failures during Dev Container feature installs.
RUN rm -f /etc/apt/sources.list.d/yarn.list \
    /etc/apt/trusted.gpg.d/yarn.gpg \
    /usr/share/keyrings/yarnkey.gpg || true
