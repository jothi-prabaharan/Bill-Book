# Code, commit & merge rules

This file documents the repository's preferred workflow for commits, migrations and merges.

- Branching: Create a feature branch off `main` for any non-trivial work. Branch name format: `feature/<module>-short-description` or `fix/<module>-short-description`.
- Commits: Use short, imperative subject lines and a body that explains why. Example: `sales: add invoice number-on-posting`.
- Migrations: Name EF Core migrations with `YYYYMMDDHHMM_<module>_<purpose>` or `<module>_<short-purpose>`. Include a short migration plan in the PR description (which DBs it runs against and whether it touches master/contacts).
- PRs: Always open a PR for review. Assign at least one reviewer and include which modules are impacted.
- Main branch: Protected; merge only via PR after CI passes. (Note: You asked to commit to `main` for this change — prefer PRs for future edits.)
- Reverts: Prefer a compensating migration over a destructive revert of a posted journal entry.

Why: Consistency prevents accidental data-loss or differing schema between environments. The migration naming rules help when diagnosing a migration-applied order problem.
