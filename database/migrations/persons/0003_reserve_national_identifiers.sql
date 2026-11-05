-- National identity numbers remain reserved after deactivation.
-- Reuse could silently attach historical references to a different real-world party.
DROP INDEX IF EXISTS ux_persons_natural_national_id;
DROP INDEX IF EXISTS ux_persons_legal_national_id;

CREATE UNIQUE INDEX ux_persons_natural_national_id
    ON persons (national_id)
    WHERE party_type = 1 AND national_id IS NOT NULL;

CREATE UNIQUE INDEX ux_persons_legal_national_id
    ON persons (legal_national_id)
    WHERE party_type = 2 AND legal_national_id IS NOT NULL;
