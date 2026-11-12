#!/usr/bin/env bash
# Generates a solution from the Vertical Slice + Clean Architecture template and verifies it:
#   1. no template directives (including //~ alternate-branch markers) survived generation
#   2. no identity leaks (the template's source name or Vercuski) remain
#   3. the generated .slnx lists exactly the .csproj files on disk
#   4. the generated projects match the repo's real source/Vertical-Slice_Clean-Architecture.slnx (drift guard)
#   5. generated JSON parses strictly
#   6. the solution builds with zero warnings (TreatWarningsAsErrors comes from Directory.Build.props)
#   7. all generated tests pass (when the solution contains a test project)
#
# Usage: verify-template.sh <case-name> [dotnet new options...]
#   e.g. verify-template.sh defaults
# Requires the template to be installed: dotnet new install <repo>/source (or the packed .nupkg)
set -euo pipefail

CASE="$1"; shift
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOURCE_NAME="Vertical-Slice_Clean-Architecture"
WORK="${VERIFY_WORK_DIR:-${TMPDIR:-/tmp}/vsca-verify}/$CASE"
NAME="Acme.Verify"

fail() { echo "FAIL [$CASE]: $*" >&2; exit 1; }

rm -rf "$WORK" && mkdir -p "$WORK"
dotnet new vsca -n "$NAME" -o "$WORK" "$@" >/dev/null

cd "$WORK"
[ -f "$NAME.slnx" ] || fail "expected $NAME.slnx was not generated"

# 1. leftover template directives
if grep -rnE '^[[:space:]]*(//)?#(if|else|elseif|elif|endif)\b|<!--#(if|else|elseif|endif)|^[[:space:]]*//~' \
     --include='*.cs' --include='*.csproj' --include='*.json' --include='*.slnx' --include='Dockerfile' --include='*.props' . ; then
  fail "template directives left in generated output"
fi

# 2. identity leaks: every spelling of the source name (project, namespace and repository forms) and the author
if grep -rniE 'vertical[-_]slice[-_]clean[-_]architecture|vercuski' . ; then
  fail "template identity leaked into generated output"
fi

# 3. solution <-> disk consistency
sln_projects=$(dotnet sln "$NAME.slnx" list | grep -E '\.csproj$' | tr '\\' '/' | sort)
disk_projects=$(find . -name '*.csproj' -not -path '*/bin/*' -not -path '*/obj/*' | sed 's|^\./||' | sort)
[ "$sln_projects" = "$disk_projects" ] || fail "solution and disk disagree:
  sln:  $(echo $sln_projects)
  disk: $(echo $disk_projects)"

# 4. drift guard: the template must reproduce the reference solution's projects
ref_projects=$(dotnet sln "$REPO_ROOT/source/$SOURCE_NAME.slnx" list | grep -E '\.csproj$' | tr '\\' '/' | sed "s/$SOURCE_NAME/$NAME/g" | sort)
[ "$sln_projects" = "$ref_projects" ] || fail "template solution has drifted from source/$SOURCE_NAME.slnx:
  template:  $(echo $sln_projects)
  reference: $(echo $ref_projects)"

# 5. generated JSON is valid (appsettings*.json may contain // and /* */ comments)
python3 - <<'PY' || fail "configuration check failed"
import json, pathlib, sys

def strip_jsonc(text):
    # Remove // and /* */ comments outside of strings; anything left must be strict JSON
    # (so a dangling comma left behind by a removed block fails here).
    out, i, in_str = [], 0, False
    while i < len(text):
        c = text[i]
        if in_str:
            out.append(c)
            if c == "\\": out.append(text[i + 1]); i += 1
            elif c == '"': in_str = False
        elif c == '"': in_str = True; out.append(c)
        elif text.startswith("//", i): i = text.find("\n", i) - 1 if "\n" in text[i:] else len(text)
        elif text.startswith("/*", i): i = text.index("*/", i) + 1
        else: out.append(c)
        i += 1
    return "".join(out)

ok = True
for path in pathlib.Path(".").rglob("*.json"):
    if any(part in ("bin", "obj") for part in path.parts): continue
    try: json.loads(strip_jsonc(path.read_text(encoding="utf-8-sig")))
    except Exception as e: print(f"  invalid JSON: {path}: {e}"); ok = False
sys.exit(0 if ok else 1)
PY

# 6. build, zero warnings
build_log=$(dotnet build "$NAME.slnx" -c Release 2>&1) || { echo "$build_log" | grep -E 'error|warning' | sort -u | head -20; fail "build failed"; }
echo "$build_log" | grep -qE '^\s*0 Warning\(s\)' || { echo "$build_log" | grep -E 'warning' | sort -u | head -20; fail "build produced warnings"; }

# 7. tests (the template ships no test project yet; this runs once one is added)
if find . -name '*Tests.csproj' -not -path '*/bin/*' -not -path '*/obj/*' | grep -q .; then
  test_log=$(dotnet test "$NAME.slnx" -c Release --no-build 2>&1) || { echo "$test_log" | grep -E 'Failed|error' | head -20; fail "tests failed"; }
  summary=$(echo "$test_log" | grep -E 'Passed!|Failed!' | sed -E 's/, Duration.*//; s/ +/ /g')
else
  summary="(no test project)"
fi

echo "PASS [$CASE] $(echo "$sln_projects" | wc -l | tr -d ' ') projects; $summary"
