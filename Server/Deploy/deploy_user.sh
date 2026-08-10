#!/usr/bin/env bash
set -euo pipefail

readonly APP_ROOT="/home/economyslg/apps/economy-slg"
readonly VERSION="0.2.0"
readonly EXPECTED_SHA256="c9817117cebc3e0e54ea8ec634ca7d6e45ee403e1355bba58c93d6341de62322"
readonly ARCHIVE="$APP_ROOT/incoming/game-server-$VERSION-linux-x64.tar.gz"
readonly RELEASE_DIR="$APP_ROOT/releases/$VERSION"
readonly CONFIG_DIR="$APP_ROOT/config"
readonly RUN_DIR="$APP_ROOT/run"
readonly LOG_DIR="$APP_ROOT/logs"
readonly DATA_DIR="$APP_ROOT/data"
readonly ENV_FILE="$CONFIG_DIR/pvp.env"
readonly PID_FILE="$RUN_DIR/server.pid"
readonly HEALTH_URL="http://127.0.0.1:5100/health"

mkdir -p "$APP_ROOT/incoming" "$APP_ROOT/releases" "$CONFIG_DIR" "$RUN_DIR" "$LOG_DIR" "$DATA_DIR"

if [[ ! -f "$ARCHIVE" ]]; then
    echo "배포 파일이 없습니다: $ARCHIVE" >&2
    exit 19
fi

actual_sha256="$(sha256sum "$ARCHIVE" | awk '{print $1}')"
if [[ "$actual_sha256" != "$EXPECTED_SHA256" ]]; then
    echo "배포 파일 해시가 일치하지 않습니다." >&2
    echo "예상: $EXPECTED_SHA256" >&2
    echo "실제: $actual_sha256" >&2
    exit 20
fi

if [[ ! -x "$RELEASE_DIR/Game.Server" ]]; then
    if [[ -e "$RELEASE_DIR" ]]; then
        echo "불완전한 릴리스 디렉터리가 이미 있습니다: $RELEASE_DIR" >&2
        exit 21
    fi
    staging_dir="$APP_ROOT/releases/.${VERSION}.staging.$$"
    mkdir -p "$staging_dir"
    tar -xzf "$ARCHIVE" -C "$staging_dir"
    chmod 750 "$staging_dir/Game.Server"
    mv "$staging_dir" "$RELEASE_DIR"
fi

umask 077
touch "$ENV_FILE"

ensure_env() {
    local key="$1"
    local value="$2"
    if ! grep -q "^${key}=" "$ENV_FILE"; then
        printf '%s=%s\n' "$key" "$value" >> "$ENV_FILE"
    fi
}

random_token() {
    printf '%s%s' "$(cat /proc/sys/kernel/random/uuid)" "$(cat /proc/sys/kernel/random/uuid)"
}

ensure_env "PVP_PLAYER1_TOKEN" "$(random_token)"
ensure_env "PVP_PLAYER2_TOKEN" "$(random_token)"
ensure_env "PVP_URLS" "http://127.0.0.1:5100"
ensure_env "PVP_DATA_DIR" "$DATA_DIR"
ensure_env "PVP_MATCH_ID" "dev-match-001"
ensure_env "PVP_TURN_TIMEOUT_SECONDS" "120"
ensure_env "ASPNETCORE_ENVIRONMENT" "Production"
chmod 600 "$ENV_FILE"

set -a
# shellcheck disable=SC1090
source "$ENV_FILE"
set +a

old_target=""
if [[ -L "$APP_ROOT/current" ]]; then
    old_target="$(readlink -f "$APP_ROOT/current")"
fi

old_pid=""
if [[ -f "$PID_FILE" ]]; then
    old_pid="$(cat "$PID_FILE")"
fi

stop_process() {
    local pid="$1"
    if [[ -z "$pid" ]] || ! kill -0 "$pid" 2>/dev/null; then
        return 0
    fi

    kill -TERM "$pid"
    for _ in {1..30}; do
        if ! kill -0 "$pid" 2>/dev/null; then
            return 0
        fi
        sleep 1
    done

    echo "프로세스가 정상 종료되지 않아 강제 종료합니다. PID=$pid" >&2
    kill -KILL "$pid"
}

start_target() {
    local executable="$1"
    local log_file="$2"
    nohup "$executable" >> "$log_file" 2>&1 &
    local pid=$!
    printf '%s\n' "$pid" > "$PID_FILE.tmp"
    mv "$PID_FILE.tmp" "$PID_FILE"
    printf '%s' "$pid"
}

wait_for_health() {
    for _ in {1..30}; do
        if curl --fail --silent --show-error "$HEALTH_URL" >/dev/null; then
            return 0
        fi
        sleep 1
    done
    return 1
}

stop_process "$old_pid"
ln -sfn "$RELEASE_DIR" "$APP_ROOT/current"
new_pid="$(start_target "$APP_ROOT/current/Game.Server" "$LOG_DIR/server.log")"

if wait_for_health; then
    echo "게임 서버 배포 성공. PID=$new_pid PORT=5100 VERSION=$VERSION"
    curl --fail --silent "$HEALTH_URL"
    echo
    exit 0
fi

echo "새 서버가 제한 시간 안에 준비되지 않아 이전 버전으로 복구합니다." >&2
stop_process "$new_pid"
tail -n 80 "$LOG_DIR/server.log" >&2 || true

if [[ -n "$old_target" ]] && [[ -x "$old_target/Game.Server" ]]; then
    ln -sfn "$old_target" "$APP_ROOT/current"
    rollback_pid="$(start_target "$APP_ROOT/current/Game.Server" "$LOG_DIR/server.log")"
    if wait_for_health; then
        echo "이전 서버 복구 성공. PID=$rollback_pid TARGET=$old_target" >&2
        exit 22
    fi
fi

echo "자동 복구에도 실패했습니다. 로그를 확인하십시오: $LOG_DIR/server.log" >&2
exit 23
