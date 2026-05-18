---
name: research-issue
description: >
  Deep research on a GitHub issue or problem description. Analyzes architecture,
  evaluates multiple implementation approaches, and produces a structured
  report with code sketches, tradeoff analysis, and recommendations.
  Interactive — pauses for Q&A between phases. Use when given a GitHub issue
  number or a free-text problem description to investigate before implementation.
argument-hint: <issue-number-or-description>
disable-model-invocation: true
allowed-tools: Read, Write, Grep, Glob, Bash(gh issue view:*), Bash(gh api:*), Agent, WebSearch, WebFetch
---

# Research Issue

Read and follow `../../../.agents/workflows/research-issue/WORKFLOW.md`.

Use `$ARGUMENTS` as the issue number or problem description. For GitHub issue
numbers, prefer `gh issue view $ARGUMENTS --json title,body,labels,comments`.
Write the final report to `/tmp/research-issue-$ARGUMENTS.md` for issue numbers
or `/tmp/research-topic.md` for free-text descriptions.
