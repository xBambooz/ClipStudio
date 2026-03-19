---
name: sync-keyword
description: In this repo, treat the user shorthand `sync` as a request to refresh Codex and Claude context/rules docs to match current code and behavior.
type: feedback
---

When the user says `sync`, interpret it as a request to update the documentation/context layer for this repository.

**Why:** The user wants a short keyword instead of repeating the full request to update `.codex`, `.claude`, and `rules/` guidance after changes.

**How to apply:** On `sync`, refresh the relevant `.codex` context/rules plus the repo's Claude-facing docs and rules files so they reflect the current implementation.
