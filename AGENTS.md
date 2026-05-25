# ChurroStack — Agent Guide

ChurroStack is an Nx + pnpm monorepo; see [`README.md`](README.md) for the
project layout and common commands. This is the canonical agent guide for the
whole repository. Each project under `apps/` has its own `AGENTS.md` that
references this file.

## Core Invariants

These rules take precedence over all other guidance in this file.

1. **Don't assume. Don't hide confusion. Surface tradeoffs.** If something is ambiguous or has meaningful alternatives, say so before proceeding. Never silently pick one path.
2. **Minimum code that solves the problem. Nothing speculative.** Write only what the task requires. No extra abstractions, no future-proofing, no "while I'm here" cleanup.
3. **Touch only what you must. Clean up only your own mess.** Scope changes tightly to the problem. Don't refactor surrounding code unless you broke it.
4. **Define success criteria. Loop until verified.** Before starting, state what "done" looks like. After changes, verify that criterion is met — build, test, or observable behavior — before reporting complete.

## Worktree Discipline

- Always verify `pwd` before editing; this repo uses git worktrees and edits must land in the active worktree, not the main repo path.
- If a worktree directory appears missing or stale, stop and ask before retrying exploration.

## Concepts

Canonical vocabulary docs live under [`docs/concepts/`](docs/concepts/). Read the relevant one before touching the area — names are deliberate and the UI, API, and enforcement code are expected to use the same words.

- [Environment resources — Used / Requested / Allocated / Quota](docs/concepts/environment-resources.md): what each of the four resource numbers means, where they're computed, and how CPU/Memory quota enforcement (`EnsureEnvironmentRunningQuota` + per-env Redis lock) works.

## Traceability

- Add useful trace information for new implementation paths, especially at process boundaries, external tool/API calls, persistence reads/writes, approval/permission decisions, and error/fallback branches.
- Trace logs should include stable correlation identifiers where available (request id, sub-chat id, session/thread id, workspace/project id) plus the outcome and compact reason/error. Avoid logging secrets, tokens, full prompts, large payloads, or noisy per-frame/per-keystroke data.
- Prefer existing logging conventions and prefixes in the touched area. If no convention exists, use a concise component prefix that makes the source searchable.

## Change Scope

- Prefer the simplest fix that solves the reported problem; do not introduce new config fields, abstractions, or specificity hacks before reading the relevant library/theming docs.
- When a user says 'one-line fix', apply only that—do not refactor surrounding code.

## Postmortems

- Document every issue or bugfix that the OpenSpec workflow does **not** already cover under `openspec/postmortems/` when OpenSpec is initialized, or `docs/postmortems/` otherwise. Folders are dated and descriptively named (e.g., `2026-05-04-fix-<short-slug>/`) and contain a short markdown summary covering trigger, root cause, fix, and verification steps, plus any DB scripts or other artifacts the fix required. One folder per logical change.
