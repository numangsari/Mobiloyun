#!/usr/bin/env bash
# Runs Jelly Mold's Unity tasks in batch mode (macOS / Linux).
#
#   scripts/unity.sh setup | test-edit | test-play | screenshots | perf | apk | all
#
# Unity is located from JellyMold/ProjectSettings/ProjectVersion.txt in the default Unity Hub
# install folder; set UNITY_PATH to the editor binary to override. Results and logs go to
# JellyMold/Builds/ (git-ignored).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/JellyMold"
OUT="$PROJECT/Builds"
mkdir -p "$OUT/logs"

find_unity() {
  if [[ -n "${UNITY_PATH:-}" ]]; then echo "$UNITY_PATH"; return; fi
  local version base candidates=()
  version="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
  for base in "/Applications/Unity/Hub/Editor" "$HOME/Applications/Unity/Hub/Editor" "$HOME/Unity/Hub/Editor"; do
    [[ -d "$base" ]] || continue
    for dir in "$base/$version" $(ls -d "$base"/6000.* 2>/dev/null | sort -rV); do
      for bin in "$dir/Unity.app/Contents/MacOS/Unity" "$dir/Editor/Unity"; do
        [[ -x "$bin" ]] && candidates+=("$bin")
      done
    done
  done
  if [[ ${#candidates[@]} -eq 0 ]]; then
    echo "Unity 6 not found. Install it with Unity Hub or set UNITY_PATH." >&2
    exit 1
  fi
  echo "${candidates[0]}"
}

UNITY="$(find_unity)"
echo "Unity: $UNITY"

run() { # name, args...
  local name="$1"; shift
  echo "==> $name"
  set +e
  "$UNITY" -batchmode -projectPath "$PROJECT" -logFile "$OUT/logs/$name.log" "$@"
  local code=$?
  set -e
  if [[ $code -ne 0 ]]; then
    echo "    FAILED (exit $code) - see $OUT/logs/$name.log" >&2
    return $code
  fi
  echo "    ok"
}

summary() { # results xml
  local xml="$1"
  [[ -f "$xml" ]] || { echo "    no results file"; return; }
  local line
  line="$(grep -m1 -o '<test-run[^>]*>' "$xml" || true)"
  echo "    $(echo "$line" | grep -o 'total="[0-9]*"') $(echo "$line" | grep -o 'passed="[0-9]*"') $(echo "$line" | grep -o 'failed="[0-9]*"') $(echo "$line" | grep -o 'skipped="[0-9]*"')"
}

tests() { # name, platform, category (optional), extra args...
  local name="$1" platform="$2" category="$3"; shift 3
  local args=(-runTests -testPlatform "$platform" -testResults "$OUT/$name.xml")
  [[ -n "$category" ]] && args+=(-testCategory "$category")
  run "$name" "${args[@]}" "$@" || true
  summary "$OUT/$name.xml"
  grep -q 'failed="0"' "$OUT/$name.xml" 2>/dev/null
}

task="${1:-all}"
case "$task" in
  setup)       run setup -quit -executeMethod JellyMold.EditorTools.ProjectSetup.Apply ;;
  test-edit)   tests test-edit EditMode "" -nographics ;;
  test-play)   tests test-play PlayMode Smoke ;;
  screenshots) tests screenshots PlayMode Capture; echo "    images: $OUT/Screenshots" ;;
  perf)        tests perf PlayMode Perf; cat "$OUT/perf.json" 2>/dev/null || true ;;
  apk)         run apk -quit -buildTarget Android -executeMethod JellyMold.EditorTools.LevelTools.BuildAndroid
               ls -lh "$OUT/Android/"*.apk ;;
  all)
    status=0
    "$0" setup || status=1
    "$0" test-edit || status=1
    "$0" test-play || status=1
    "$0" screenshots || status=1
    "$0" perf || status=1
    "$0" apk || status=1
    exit $status ;;
  *) echo "usage: $0 setup|test-edit|test-play|screenshots|perf|apk|all" >&2; exit 2 ;;
esac
