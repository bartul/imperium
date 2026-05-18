---
name: research-issue
description: Deep research on a GitHub issue or problem description. Use when asked to investigate an issue before implementation, compare implementation approaches, produce a structured research report, or follow a phased architecture research workflow.
---

# Research Issue

Read and follow `WORKFLOW.md`.

Adapt tool usage to Codex:

- Prefer the GitHub connector for issue, PR, and repository metadata.
- Use `rg`, `sed`, and local shell reads for codebase inspection.
- Use web search only when external or current sources are required.
- Do not modify project files while using this research workflow.
- Write the final report to `/tmp/research-issue-<issue-number>.md` for issue
  numbers or `/tmp/research-topic.md` for free-text descriptions.
