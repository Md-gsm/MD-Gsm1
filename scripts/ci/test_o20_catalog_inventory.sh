#!/usr/bin/env bash
set -euo pipefail
: "$MDGSM_TEST_DB"
: "$RUNNER_TEMP"
[[ "$MDGSM_TEST_DB" =~ ^mdgsm_ci_disposable_[0-9]+_[0-9]+$ ]]
marker="$(psql -X -d postgres -Atqc \
  "SELECT shobj_description(oid,'pg_database') FROM pg_database WHERE datname='$MDGSM_TEST_DB'")"
[[ "$marker" == MDGSM_DISPOSABLE_TEST_DB\;* ]] || { echo "O20-G refused: target database is not marked disposable." >&2; exit 1; }
superuser="$(psql -X -d "$MDGSM_TEST_DB" -Atqc "SELECT rolsuper FROM pg_roles WHERE rolname=current_user")"
[[ "$superuser" == "t" ]] || { echo "O20-G refused: audit connection is not a catalog-audit superuser." >&2; exit 1; }
psql -X -v ON_ERROR_STOP=1 -d "$MDGSM_TEST_DB" -f scripts/ci/o20g_catalog_assertions.sql
evidence_file="$RUNNER_TEMP/o20-g-catalog-inventory.json"
psql -X -qAt -v ON_ERROR_STOP=1 -d "$MDGSM_TEST_DB" -f scripts/ci/o20g_catalog_inventory.sql > "$evidence_file"
test -s "$evidence_file"
chmod 600 "$evidence_file"
python3 - "$evidence_file" <<'PY'
import json, sys
from pathlib import Path
payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
required = {
    "format", "server_version_num", "database", "role_bindings", "roles",
    "memberships", "schemas", "relations", "column_acls", "routines",
    "types", "default_acls", "extensions", "languages",
    "foreign_data_wrappers", "foreign_servers", "large_objects",
    "tablespaces", "object_ownership", "effective_privileges",
}
missing = sorted(required - payload.keys())
if missing:
    raise SystemExit(f"O20-G evidence is incomplete; missing keys: {missing}")
if payload["format"] != "MDGSM-O20-G-CATALOG-v1":
    raise SystemExit("O20-G evidence format marker mismatch")
print(
    "O20-G inventory counts: "
    f"roles={len(payload['roles'])}, memberships={len(payload['memberships'])}, "
    f"schemas={len(payload['schemas'])}, relations={len(payload['relations'])}, "
    f"routines={len(payload['routines'])}, types={len(payload['types'])}, "
    f"extensions={len(payload['extensions'])}, ownership={len(payload['object_ownership'])}, "
    f"effective_privileges={len(payload['effective_privileges'])}"
)
PY
digest="$(sha256sum "$evidence_file" | awk '{print $1}')"
printf 'O20-G evidence_digest_sha256=%s\n' "$digest"
printf 'O20-G evidence_file=%s\n' "$evidence_file"
echo "PASS: complete marked-database catalog inventory, role graph, object ownership, ACL/default-ACL and effective-privilege digest captured; runtime/migration boundaries and unprovisioned support identities verified."
