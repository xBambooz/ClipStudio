# Codex Workspace

This directory is the Codex-side replacement for the existing `.claude` setup.

## Layout

- `agents/`: Codex agent prompts and operating instructions.
- `memory/`: Persistent project memory, organized per agent.
- `context/`: Reference material migrated from Claude-specific files.
- `context/project-summary.md`: Current product summary and notable behavior.

## Migration Notes

- Source content was read from `.claude/agents/code-reviewer.md`.
- The original Claude agent text is preserved under `context/source/`.
- A Codex-formatted version of that agent lives under `agents/`.
- The Claude memory layout was mirrored under `memory/code-reviewer/`.

## Conventions

- Keep agent prompts in Markdown.
- Keep memory entries in separate Markdown files with frontmatter.
- Keep `MEMORY.md` as an index only.
- Preserve original source material in `context/source/` when migrating from other tool formats.
- Update `.codex` rules/context files when project behavior, features, or workflow expectations change.
