#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
if [[ -f .env ]]; then
  set -a
  source .env
  set +a
fi
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
: "${ConnectionStrings__DefaultConnection:?Hãy cấu hình ConnectionStrings__DefaultConnection trong .env hoặc environment.}"
: "${Jwt__Key:?Hãy cấu hình Jwt__Key trong .env hoặc environment.}"
mkdir -p .local/logs
dotnet build ElectroShop.sln --nologo --verbosity quiet
pids=()
cleanup() {
  trap - EXIT INT TERM
  if (( ${#pids[@]} )); then
    kill "${pids[@]}" 2>/dev/null || true
    for pid in "${pids[@]}"; do wait "$pid" 2>/dev/null || true; done
  fi
}
trap cleanup EXIT INT TERM
dotnet run --project API --no-build --no-launch-profile --urls http://0.0.0.0:5100 > .local/logs/api.log 2>&1 &
pids+=("$!")
ready=false
for ((attempt=0; attempt<45; attempt++)); do
  if curl --silent --fail http://localhost:5100/health > /dev/null; then ready=true; break; fi
  if ! kill -0 "${pids[0]}" 2>/dev/null; then cat .local/logs/api.log; exit 1; fi
  sleep 1
done
if [[ "$ready" != true ]]; then echo 'API chưa sẵn sàng. Xem .local/logs/api.log'; exit 1; fi
for spec in 'RazorPage:5101' 'BlazorWeb:5102' 'MVC:5103'; do
  project="${spec%:*}"; port="${spec#*:}"
  dotnet run --project "$project" --no-build --no-launch-profile --urls "http://0.0.0.0:$port" > ".local/logs/$project.log" 2>&1 &
  pids+=("$!")
done
printf '%s\n' 'API: http://localhost:5100 (OpenAPI: /openapi/v1.json)' 'Razor Pages: http://localhost:5101' 'Blazor: http://localhost:5102' 'MVC: http://localhost:5103' 'Logs: .local/logs/ · Ctrl+C để dừng cả bốn ứng dụng.'
wait -n "${pids[@]}"
