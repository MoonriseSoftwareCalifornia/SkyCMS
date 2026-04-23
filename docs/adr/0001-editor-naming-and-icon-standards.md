# ADR 0001: Editor Naming and Icon Standards

## Status
Accepted

## Context

SkyCMS provides multiple editing experiences that operate at different levels
of abstraction and technical complexity. Over time, as new editing capabilities
were added, naming inconsistencies emerged that made it difficult for users to
clearly understand which editor should be used for a given task.

In particular, previous naming (for example, “Live Editor”) described editor
behavior rather than user intent. This led to confusion because all editors
save changes automatically and behave “live” in some sense. The name also
failed to communicate whether the editor was intended for content, layout, or
code-level work.

Because editor selection is a critical decision point for users, this naming
ambiguity increased hesitation, misclicks, and reliance on external guidance.

A deliberate, user‑oriented naming system was required to clearly express:
- what kind of work each editor is for,
- how editors differ from one another,
- and which editor a user should choose in a given situation.

---

## Design Goals

This decision aims to:

1. Describe user intent rather than implementation details  
2. Be understandable to non‑technical users  
3. Scale across UI, documentation, and accessibility contexts  
4. Remain stable even if editor implementations change  
5. Clearly differentiate content editing, layout design, and code editing  

---

## Non‑Goals

This decision does not attempt to:

- Expose or brand specific editor implementations (e.g., CKEditor, GrapesJS)
- Collapse all editing experiences into a single editor
- Optimize naming for marketing or feature differentiation

---

## Decision

SkyCMS standardizes on exactly three editor categories:

- **Visual Editor**
- **Page Builder**
- **Code Editor**

Each editor is defined not only by name, but also by:
- documentation‑friendly long names,
- concise hover/alt descriptions,
- and standardized Font Awesome icons.

These standards are defined in the document:
**SkyCMS Editor Naming & Icon Standards**.

---

## Detailed Rationale

### Intent‑Based Naming

Editor names are based on *what the user intends to do*, not how the editor
works internally. This avoids coupling UX language to implementation details
and makes the system more durable over time.

### Iconography

Each editor is paired with a widely recognized, monochrome‑friendly icon:

- Visual Editor → writing/editing metaphor (pencil)
- Page Builder → structural layout metaphor (columns/grid)
- Code Editor → logic/code metaphor (brackets)

Icons were selected to:
- work without color,
- scale cleanly in UI and documentation,
- and remain recognizable at small sizes.

---

## Alternatives Considered

### Implementation‑Based Naming

Examples:
- CKEditor
- GrapesJS Editor
- Monaco Editor

Rejected because these expose internal details and provide little value to
non‑technical users.

### Behavioral or Marketing‑Driven Naming

Examples:
- Live Editor
- Advanced Editor
- Smart Editor

Rejected because they describe behavior rather than purpose and risk becoming
misleading.

### Single Editor with Multiple Modes

Rejected because it obscures conceptual boundaries and increases onboarding
complexity.

---

## Consequences

### Positive Outcomes
- Users select editors more confidently
- Naming remains stable over time
- Documentation and UI language stay consistent
- Future contributors have a clear conceptual framework

### Constraints Introduced
- New editors must follow this intent‑based model
- Naming changes must revise or supersede this ADR

---

## Future Intent

This ADR establishes a naming *system*, not just labels.

Future editors should follow the same principles:
- intent‑first naming,
- clear conceptual boundaries,
- consistent visual language.

Any deviation from this model must be intentional and formally documented.