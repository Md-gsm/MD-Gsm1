-- Enforce nonblank identity names at the database boundary.
-- Preflight first so legacy bad rows stop the migration before any constraint is installed.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM persons
        WHERE party_type = 1
          AND (
              given_name IS NULL OR length(regexp_replace(given_name, '[[:space:]]', '', 'g')) = 0
              OR family_name IS NULL OR length(regexp_replace(family_name, '[[:space:]]', '', 'g')) = 0
          )
    ) THEN
        RAISE EXCEPTION 'Cannot enforce natural-person name integrity: blank historical names exist.';
    END IF;
    IF EXISTS (
        SELECT 1 FROM persons
        WHERE party_type = 2
          AND (legal_name IS NULL OR length(regexp_replace(legal_name, '[[:space:]]', '', 'g')) = 0)
    ) THEN
        RAISE EXCEPTION 'Cannot enforce legal-person name integrity: blank historical names exist.';
    END IF;
END
$$;
ALTER TABLE persons
    ADD CONSTRAINT ck_persons_natural_names_nonblank
    CHECK (party_type <> 1 OR (
        given_name IS NOT NULL AND length(regexp_replace(given_name, '[[:space:]]', '', 'g')) > 0
        AND family_name IS NOT NULL AND length(regexp_replace(family_name, '[[:space:]]', '', 'g')) > 0
    ));
ALTER TABLE persons
    ADD CONSTRAINT ck_persons_legal_name_nonblank
    CHECK (party_type <> 2 OR (
        legal_name IS NOT NULL AND length(regexp_replace(legal_name, '[[:space:]]', '', 'g')) > 0
    ));
