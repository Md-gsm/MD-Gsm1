-- Reserve national identifiers for the lifetime of a Party, including after deactivation.
-- This preserves historical references and prevents reassignment to another real-world identity.
-- The migration is transactional; abort before changing indexes if historical duplicates exist.
DO $$
BEGIN
    IF EXISTS (
        SELECT national_id
        FROM persons
        WHERE party_type = 1 AND national_id IS NOT NULL
        GROUP BY national_id
        HAVING count(*) > 1
    ) THEN
        RAISE EXCEPTION 'Cannot reserve natural-person national identifiers: historical duplicates exist.';
    END IF;

    IF EXISTS (
        SELECT legal_national_id
        FROM persons
        WHERE party_type = 2 AND legal_national_id IS NOT NULL
        GROUP BY legal_national_id
        HAVING count(*) > 1
    ) THEN
        RAISE EXCEPTION 'Cannot reserve legal-person national identifiers: historical duplicates exist.';
    END IF;
END
$$;

DROP INDEX IF EXISTS ux_persons_natural_national_id;
DROP INDEX IF EXISTS ux_persons_legal_national_id;

CREATE UNIQUE INDEX ux_persons_natural_national_id
    ON persons (national_id)
    WHERE party_type = 1 AND national_id IS NOT NULL;

CREATE UNIQUE INDEX ux_persons_legal_national_id
    ON persons (legal_national_id)
    WHERE party_type = 2 AND legal_national_id IS NOT NULL;
