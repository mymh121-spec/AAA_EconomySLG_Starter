#!/usr/bin/env bash
set -euo pipefail

readonly APP_ROOT="/home/economyslg/apps/economy-slg"
readonly BASE_URL="http://127.0.0.1:5100"

set -a
# shellcheck disable=SC1090
source "$APP_ROOT/config/pvp.env"
set +a

: "${PVP_PLAYER1_TOKEN:?PVP_PLAYER1_TOKEN이 필요합니다.}"
: "${PVP_PLAYER2_TOKEN:?PVP_PLAYER2_TOKEN이 필요합니다.}"
PVP_MATCH_ID="${PVP_MATCH_ID:-dev-match-001}"

health="$(curl --fail --silent "$BASE_URL/health")"
snapshot1="$(curl --fail --silent -H "Authorization: Bearer $PVP_PLAYER1_TOKEN" "$BASE_URL/api/v1/match")"
snapshot2="$(curl --fail --silent -H "Authorization: Bearer $PVP_PLAYER2_TOKEN" "$BASE_URL/api/v1/match")"

read -r turn revision sequence1 <<< "$(python3 - "$snapshot1" <<'PY'
import json, sys
s = json.loads(sys.argv[1])
p = next(p for p in s["players"] if p["playerId"] == s["playerId"])
print(s["turn"], s["revision"], p["expectedSequence"])
PY
)"

read -r turn2 revision2 sequence2 <<< "$(python3 - "$snapshot2" <<'PY'
import json, sys
s = json.loads(sys.argv[1])
p = next(p for p in s["players"] if p["playerId"] == s["playerId"])
print(s["turn"], s["revision"], p["expectedSequence"])
PY
)"

if [[ "$turn" != "$turn2" ]] || [[ "$revision" != "$revision2" ]]; then
    echo "두 플레이어가 서로 다른 경기 상태를 보고 있습니다." >&2
    exit 30
fi

run_id="$(python3 -c 'import uuid; print(uuid.uuid4().hex)')"

command_body1="$(python3 - "$run_id" "$PVP_MATCH_ID" "$revision" "$turn" "$sequence1" <<'PY'
import json, sys
run, match_id, revision, turn, sequence = sys.argv[1:]
print(json.dumps({
    "requestId": f"verify-command-p1-{run}", "protocolVersion": 1,
    "matchId": match_id, "expectedRevision": int(revision),
    "commandId": f"verify-p1-{run}", "turn": int(turn),
    "sequence": int(sequence), "kind": "MarketBuy", "regionId": "capital",
    "resourceId": "iron", "targetCompanyId": "", "targetId": "",
    "quantity": 1, "limitPrice": 1000000
}, separators=(",", ":")))
PY
)"

command_body2="$(python3 - "$run_id" "$PVP_MATCH_ID" "$revision" "$turn" "$sequence2" <<'PY'
import json, sys
run, match_id, revision, turn, sequence = sys.argv[1:]
print(json.dumps({
    "requestId": f"verify-command-p2-{run}", "protocolVersion": 1,
    "matchId": match_id, "expectedRevision": int(revision),
    "commandId": f"verify-p2-{run}", "turn": int(turn),
    "sequence": int(sequence), "kind": "MarketSell", "regionId": "capital",
    "resourceId": "iron", "targetCompanyId": "", "targetId": "",
    "quantity": 1, "limitPrice": 0.01
}, separators=(",", ":")))
PY
)"

submit1="$(curl --fail --silent -H "Authorization: Bearer $PVP_PLAYER1_TOKEN" -H 'Content-Type: application/json' -d "$command_body1" "$BASE_URL/api/v1/commands")"
submit2="$(curl --fail --silent -H "Authorization: Bearer $PVP_PLAYER2_TOKEN" -H 'Content-Type: application/json' -d "$command_body2" "$BASE_URL/api/v1/commands")"

next_sequence1="$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["expectedSequence"])' "$submit1")"
next_sequence2="$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["expectedSequence"])' "$submit2")"

ready_body1="$(python3 - "$run_id" "$PVP_MATCH_ID" "$turn" "$revision" "$next_sequence1" <<'PY'
import json, sys
run, match_id, turn, revision, sequence = sys.argv[1:]
print(json.dumps({"requestId": f"verify-ready-p1-{run}", "protocolVersion": 1,
    "matchId": match_id, "turn": int(turn), "expectedRevision": int(revision),
    "lastSequence": int(sequence)}, separators=(",", ":")))
PY
)"

ready_body2="$(python3 - "$run_id" "$PVP_MATCH_ID" "$turn" "$revision" "$next_sequence2" <<'PY'
import json, sys
run, match_id, turn, revision, sequence = sys.argv[1:]
print(json.dumps({"requestId": f"verify-ready-p2-{run}", "protocolVersion": 1,
    "matchId": match_id, "turn": int(turn), "expectedRevision": int(revision),
    "lastSequence": int(sequence)}, separators=(",", ":")))
PY
)"

ready1="$(curl --fail --silent -H "Authorization: Bearer $PVP_PLAYER1_TOKEN" -H 'Content-Type: application/json' -d "$ready_body1" "$BASE_URL/api/v1/ready")"
ready2="$(curl --fail --silent -H "Authorization: Bearer $PVP_PLAYER2_TOKEN" -H 'Content-Type: application/json' -d "$ready_body2" "$BASE_URL/api/v1/ready")"
snapshot="$(curl --fail --silent -H "Authorization: Bearer $PVP_PLAYER1_TOKEN" "$BASE_URL/api/v1/match")"

python3 - "$health" "$submit1" "$submit2" "$ready1" "$ready2" "$snapshot" "$revision" "$turn" <<'PY'
import json, sys
health, submit1, submit2, ready1, ready2, snapshot = map(json.loads, sys.argv[1:7])
old_revision, old_turn = map(int, sys.argv[7:9])
assert submit1["accepted"] and submit2["accepted"]
assert ready1["accepted"] and ready2["accepted"]
assert not ready1["turnResolved"] and ready2["turnResolved"]
assert snapshot["revision"] == old_revision + 1
assert snapshot["turn"] == old_turn + 1
assert len(snapshot["stateHash"]) == 64
assert snapshot["world"]["turn"] == snapshot["turn"]
assert len(snapshot["world"]["markets"]) >= 1
assert len(snapshot["world"]["companies"]) == 2
print(json.dumps({
    "health": health["status"], "turn": snapshot["turn"],
    "revision": snapshot["revision"], "stateHash": snapshot["stateHash"],
    "markets": len(snapshot["world"]["markets"]),
    "companies": len(snapshot["world"]["companies"]),
    "result": "원격 한 턴 검증 성공"
}, ensure_ascii=False))
PY
