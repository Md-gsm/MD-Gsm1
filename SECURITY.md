# Security Policy

## Reporting a vulnerability
Please do not open a public issue. Use GitHub's "Report a vulnerability" (Security tab > Advisories) on this repository, or contact the repository owner privately.

## Scope notes
- This project is pre-release (Persons module preparation). No production deployment exists.
- Never commit secrets, connection strings or personal data. If a secret is committed, treat it as compromised and rotate it; removing it from the latest commit is not enough.
- Test databases must be disposable and carry the `MDGSM_DISPOSABLE_TEST_DB` marker.
