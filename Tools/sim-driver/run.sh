#!/bin/zsh
# Lead's simulator driver (ARCHITECTURE §3, PLAN-M1 L1). Plays a tap script against an
# installed app on a booted iOS Simulator and exports the screenshots it took.
#
#   Tools/sim-driver/run.sh <simulator-udid> "<script>" <out-dir> [bundle-id]
#
# Script steps, separated by ';':  wait:<s>  tap:<nx>,<ny>  taps:<nx>,<ny>,<n>  shot:<name>  relaunch
# Coordinates are normalized to the app's frame (0..1, origin top-left), so they are orientation-proof.
# Example: run.sh ADE26594-... "wait:2;shot:turn1;tap:0.12,0.88;wait:3;shot:after-attack" /tmp/out
set -euo pipefail
UDID="$1"; SCRIPT="$2"; OUT="$3"; BUNDLE="${4:-com.vendoza.storyquest}"
HERE="$(cd "$(dirname "$0")" && pwd)"
DD="${SQ_DRIVER_DERIVED:-/tmp/sq-sim-driver-dd}"
mkdir -p "$OUT"
[ -d "$HERE/SQDriver.xcodeproj" ] || (cd "$HERE" && xcodegen generate >/dev/null)
RESULT="$OUT/driver-$(date +%H%M%S).xcresult"
TEST_RUNNER_SQ_BUNDLE_ID="$BUNDLE" TEST_RUNNER_SQ_SCRIPT="$SCRIPT" \
  xcodebuild test -project "$HERE/SQDriver.xcodeproj" -scheme SQDriver \
  -destination "platform=iOS Simulator,id=$UDID" -derivedDataPath "$DD" \
  -resultBundlePath "$RESULT" CODE_SIGNING_ALLOWED=NO > "$OUT/driver.log" 2>&1 \
  || { grep -E "error:|failed" "$OUT/driver.log" | tail -5; exit 1; }
xcrun xcresulttool export attachments --path "$RESULT" --output-path "$OUT/att" >/dev/null 2>&1 || true
# Rename exported files to the shot names from the script.
python3 - "$OUT/att" <<'PY'
import json, os, sys
d = sys.argv[1]; m = os.path.join(d, "manifest.json")
if os.path.exists(m):
    for t in json.load(open(m)):
        for a in t.get("attachments", []):
            name = a.get("suggestedHumanReadableName", "").split("_0_")[0] or "shot"
            src = os.path.join(d, a["exportedFileName"]); dst = os.path.join(d, name + ".png")
            if os.path.exists(src) and not os.path.exists(dst): os.rename(src, dst)
    print("shots:", sorted(f for f in os.listdir(d) if f.endswith(".png")))
PY
