#!/usr/bin/env bash
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive

if ! command -v sqlcmd >/dev/null 2>&1; then
    curl -fsSL https://packages.microsoft.com/keys/microsoft.asc \
        | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg
    . /etc/os-release
    echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft-prod.gpg] https://packages.microsoft.com/ubuntu/${VERSION_ID}/prod ${VERSION_CODENAME} main" \
        > /etc/apt/sources.list.d/microsoft-prod.list
    apt-get update
    ACCEPT_EULA=Y apt-get install -y --no-install-recommends \
        msodbcsql17 mssql-tools18 unixodbc-dev libunwind8 unzip
fi

if [ ! -x /opt/sqlpackage/sqlpackage ]; then
    tmp_dir="$(mktemp -d)"
    curl -fsSL -o "$tmp_dir/sqlpackage.zip" https://aka.ms/sqlpackage-linux
    install -d -m 0755 /opt/sqlpackage
    unzip -q "$tmp_dir/sqlpackage.zip" -d /opt/sqlpackage
    chmod 0755 /opt/sqlpackage/sqlpackage
    rm -rf "$tmp_dir"
fi

ln -sf /opt/mssql-tools18/bin/sqlcmd /usr/local/bin/sqlcmd
ln -sf /opt/mssql-tools18/bin/bcp /usr/local/bin/bcp
