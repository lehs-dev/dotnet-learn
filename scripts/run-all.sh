#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

# Tối ưu RAM cho .NET: Dùng Workstation GC, tắt MSBuild Server daemon và nodeReuse
export DOTNET_GCServer=0
export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1
export MSBUILDDISABLENODEREUSE=1

if [[ -f .env ]]; then
  set -a
  source .env
  set +a
fi
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
: "${ConnectionStrings__DefaultConnection:?Hãy cấu hình ConnectionStrings__DefaultConnection trong .env hoặc environment.}"
: "${Jwt__Key:?Hãy cấu hình Jwt__Key trong .env hoặc environment.}"

TARGET="${1:-all}"

mkdir -p .local/logs
chmod 777 .local .local/logs 2>/dev/null || true

# Build với cờ tắt nodeReuse để không để lại tiến trình MSBuild ngầm chiếm dụng RAM
echo "==> Đang build solution (tối ưu bộ nhớ)..."
dotnet build ElectroShop.sln --nologo --verbosity quiet -nr:false -p:UseSharedCompilation=false

pids=()
cleanup() {
  trap - EXIT INT TERM
  if (( ${#pids[@]} )); then
    echo ""
    echo "==> Đang dừng các dịch vụ..."
    kill "${pids[@]}" 2>/dev/null || true
    for pid in "${pids[@]}"; do wait "$pid" 2>/dev/null || true; done
  fi
}
trap cleanup EXIT INT TERM

echo "==> Khởi động API trên http://0.0.0.0:5100..."
dotnet run --project API --no-build --no-launch-profile --urls http://0.0.0.0:5100 > .local/logs/api.log 2>&1 &
pids+=("$!")

ready=false
for ((attempt=0; attempt<45; attempt++)); do
  if curl --silent --fail http://localhost:5100/health > /dev/null; then ready=true; break; fi
  if ! kill -0 "${pids[0]}" 2>/dev/null; then
    echo "Lỗi khởi động API:"
    cat .local/logs/api.log
    exit 1
  fi
  sleep 1
done
if [[ "$ready" != true ]]; then echo 'API chưa sẵn sàng. Xem .local/logs/api.log'; exit 1; fi

FRONTENDS=()
case "$TARGET" in
  api)
    ;;
  razor)
    FRONTENDS=('RazorPage:5101')
    ;;
  blazor)
    FRONTENDS=('BlazorWeb:5102')
    ;;
  mvc)
    FRONTENDS=('MVC:5103')
    ;;
  all|*)
    FRONTENDS=('RazorPage:5101' 'BlazorWeb:5102' 'MVC:5103')
    ;;
esac

for spec in "${FRONTENDS[@]}"; do
  project="${spec%:*}"; port="${spec#*:}"
  echo "==> Khởi động $project trên http://0.0.0.0:$port..."
  dotnet run --project "$project" --no-build --no-launch-profile --urls "http://0.0.0.0:$port" > ".local/logs/$project.log" 2>&1 &
  pids+=("$!")
done

echo "------------------------------------------------------------"
printf '%s\n' 'API: http://localhost:5100 (OpenAPI: /openapi/v1.json)'
for spec in "${FRONTENDS[@]}"; do
  project="${spec%:*}"; port="${spec#*:}"
  printf '%s: http://localhost:%s\n' "$project" "$port"
done
printf '%s\n' 'Logs: .local/logs/ · Nhấn Ctrl+C để dừng.'
echo "------------------------------------------------------------"

wait -n "${pids[@]}"
