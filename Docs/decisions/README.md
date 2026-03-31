# Architecture Decision Records (ADRs)

This folder contains Architecture Decision Records for the SkyCMS project.

## What is an ADR?

An ADR is a short markdown document that captures a significant architectural or design decision, including the **context** that motivated it, the **decision** made, and the **consequences** (trade-offs, follow-on work, risks).

ADRs are *append-only*. When a decision is reversed or superseded, the old ADR is marked `Superseded` and a new one is created referencing it.

## Status values

| Status | Meaning |
|---|---|
| **Proposed** | Under discussion, not yet accepted |
| **Accepted** | Agreed upon and in effect |
| **Deprecated** | No longer applies but kept for history |
| **Superseded by [NNNN]** | Replaced by a later ADR |

## File naming

```
NNNN-short-hyphenated-title.md
```

e.g. `0001-plugin-system-architecture.md`

## Template

```markdown
# NNNN — Title

**Status:** Proposed | Accepted | Deprecated | Superseded by [NNNN](NNNN-filename.md)
**Date:** YYYY-MM-DD
**Deciders:** (names or roles)

## Context

What is the situation or problem that prompted this decision?

## Decision

What was decided?

## Consequences

What are the results of this decision — positive, negative, and neutral?
```
