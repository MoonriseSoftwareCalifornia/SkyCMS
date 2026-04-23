# Architecture & Design Decision Records (ADRs)

SkyCMS uses Architecture & Design Decision Records (ADRs) to document
significant decisions about the platform’s architecture, UX, naming,
and long-term direction.

ADRs exist to preserve **intent**, not just outcomes.

## Why ADRs Exist in SkyCMS

Over time, platforms evolve and contributors change. Without a written
record of *why* decisions were made, teams are forced to rediscover
context through code archaeology or repeated debate.

In SkyCMS, ADRs serve as:
- a historical record of important decisions,
- guidance for future contributors,
- and a safeguard against accidental conceptual drift.

## When to Create an ADR

An ADR should be created when a decision:
- affects UX patterns, naming, or conceptual models,
- introduces or changes architectural direction,
- establishes a standard or convention,
- or is likely to be questioned or revisited in the future.

Not every change requires an ADR. Small, localized implementation details
generally do not.

## SkyCMS Philosophy: Be Thorough, Not Brief

SkyCMS favors **complete and thoughtful ADRs** over minimal or terse ones.

ADR authors are encouraged to:
- document meaningful context,
- explain trade-offs and alternatives,
- and capture intent for future developers who were not present.

Clarity for the future is more important than brevity in the present.

## How to Create an ADR

1. Copy `ADR-TEMPLATE.md`
2. Rename it using the next available number:
   `000X-short-decision-title.md`
3. Fill out all relevant sections
4. Submit the ADR via a pull request
5. Once accepted, set the status to `Accepted`

ADRs should be added intentionally and reviewed carefully.

## Changing or Superseding ADRs

ADRs are not deleted.

If a decision must change:
- create a new ADR that supersedes the existing one,
- and reference the older ADR explicitly.

This preserves the historical record and makes change intentional.
``
## What Does Not Require an ADR

An ADR should NOT be created for:

- Routine bug fixes
- Minor refactors limited to a single module
- UI tweaks that do not establish new patterns
- Purely internal implementation optimizations
- Changes that can be trivially reversed
- Decisions whose impact is strictly local and short‑lived

As a rule of thumb:

> If future developers will not ask “why was this done this way?”  
> an ADR is probably not needed.