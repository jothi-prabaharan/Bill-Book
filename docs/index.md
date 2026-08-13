# Docs Index

This repository maintains a small set of living, canonical documentation files under the `docs/` directory. These files are the single source-of-truth for schema, validation rules, per-module status and the process contributors use to claim work.

Maintained docs (short list):

- docs/index.md (this file)
- docs/code-commit-merge-rules.md — commit/merge and migration rules
- docs/project-architecture.md — high-level architecture map
- docs/schema/COMMON_VALIDATION.md — global validation rules used by frontend & backend
- docs/modules/* — one file per module with current status and owner
- docs/status/* — one status file per module used to claim work (add Working: <agent> @ <timestamp>)

How to use these docs:

- Before starting substantial work, claim it by editing the relevant `docs/status/<Module>.md` and adding a Working line.
- Propose schema changes via a PR that updates `SPEC.md` and the corresponding `docs/schema/<Module>.md` file and describes the migration plan.
- Small edits (typos, clarifications) can be directly committed with a normal PR, but large structural moves should be discussed in an issue first.

If you don't see a module file for the area you're working on, create `docs/modules/<Module>.md` and `docs/status/<Module>.md` and add a short owner line at the top.
