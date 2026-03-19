# Claude To Codex Migration

## Source Read

- `.claude/agents/code-reviewer.md`
- `.claude/agent-memory/code-reviewer/`

## What Was Found

- One Claude agent definition: `code-reviewer`
- One matching memory directory
- No existing memory entries inside the Claude memory folder

## Migration Result

- Preserved the original Claude agent text under `context/source/`
- Rewrote the agent into a Codex-oriented prompt under `agents/`
- Initialized a mirrored memory area under `memory/code-reviewer/`

## Intent

This `.codex` folder is the working home for agents, memory, and supporting context going forward.
