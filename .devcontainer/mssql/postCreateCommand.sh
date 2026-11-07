#!/usr/bin/env bash
set -euo pipefail

sudo bash .devcontainer/mssql/installSQLtools.sh

password="${SA_PASSWORD:-P@ssw0rd}"
for attempt in $(seq 1 60); do
    if sqlcmd -C -S localhost -U sa -P "$password" -d master \
        -Q "SELECT 1" >/dev/null 2>&1; then
        echo "SQL Server is ready."
        break
    fi
    sleep 1
done

if ! sqlcmd -C -S localhost -U sa -P "$password" -d master \
    -Q "SELECT 1" >/dev/null 2>&1; then
    echo "SQL Server did not become ready in time." >&2
    exit 1
fi

sqlcmd -C -S localhost -U sa -P "$password" -d master \
    -i .devcontainer/mssql/setup.sql
