# ADR 0036: Layout Terminology Standardization and Documentation Strategy

## Status

Accepted

## Context

SkyCMS currently uses mixed terminology for the same conceptual object across
different surfaces.

- In core model, code, and route semantics, the object is represented as Layout.
- In parts of user-facing language and documentation, the same object has been
  referred to as Site Design.

This mismatch creates avoidable cognitive load. A user can see one term in the
UI and another in documentation, URLs, code references, support discussions, or
developer tooling. Over time, this weakens conceptual clarity and makes the
platform harder to teach and maintain.

The historical intent behind Layout in SkyCMS was explicit and grounded in
editorial design tradition: structured arrangement of content areas to provide
hierarchy, readability, consistency, and visual predictability while allowing
purposeful variation by content type or context.

Recent terminology review and comparative research show a split in industry
language:

- Some platforms expose Layout directly as a first-class concept.
- Many modern CMS products use broader user-facing labels such as Template,
  Theme, or Design, while still describing structural behavior that matches
  layout semantics.

WordPress documentation is especially instructive because it explicitly states
that templates define the layout around content and notes overlapping terms such
as page layout, homepage design, and site design. This confirms that broad CMS
language often blends terms that are conceptually distinct.

For SkyCMS, preserving a precise, structural term is strategically important.
The platform serves editorial and publishing scenarios where structure,
hierarchy, and repeatable composition patterns are not incidental details, but a
core authoring concern.

## Design Goals

This decision aims to:

1. Establish one canonical term for the structural page-composition concept
2. Align UI language with domain model, code, and routing vocabulary
3. Preserve and document the design intent behind the term Layout
4. Reduce user confusion caused by interchangeable or overlapping labels
5. Improve onboarding for both non-technical editors and future developers
6. Provide a stable terminology framework for future feature growth

## Non-Goals

This decision does not attempt to:

- Claim that all external CMS terminology is invalid
- Re-litigate unrelated editor naming decisions from ADR 0001
- Redesign all visual identity or theming systems
- Collapse every design-related concept into a single term
- Force immediate, high-risk breaking changes across all legacy surfaces

## Decision

SkyCMS will standardize on Layout as the canonical term for structural
page-composition in product language, documentation, and future feature design.

Site Design will no longer be the primary term for this object.

When additional clarity is useful in user-facing contexts, Page Layout may be
used as a descriptive label, but Layout remains the canonical concept and source
term.

SkyCMS will also adopt a documentation strategy that teaches term boundaries,
explains historical and practical rationale, and provides a crosswalk to common
industry labels.

## Detailed Rationale

### Conceptual Precision and Structural Intent

Layout is the most precise term for the object SkyCMS models:

- region and block arrangement,
- hierarchy and reading flow,
- repeatable structural composition,
- controlled variance for different content purposes.

Site Design is broader and can imply brand expression, visual style systems,
color, typography, and aesthetic direction beyond structure. Using Site Design as
the primary label for a structural object introduces semantic overreach.

### Alignment Across UX and Architecture

Terminology consistency is a design-system concern and an architecture concern.
When UI says Site Design while code says Layout, users and contributors must
continuously translate concepts. Standardizing on Layout reduces this translation
cost and improves supportability.

### Historical Continuity and Future Maintainability

SkyCMS originated with the term Layout and associated intent. Re-centering the
platform on this term preserves architectural and editorial continuity while
clarifying expectations for future contributors.

### Industry Context Without Adversarial Framing

External CMS ecosystems are inconsistent. Some products promote Template or
Design as top-level labels, while others expose Layout explicitly. SkyCMS should
document these differences, but avoid adversarial claims that other platforms are
wrong. A neutral, standards-oriented explanation is more credible, more durable,
and more useful to users.

### WordPress as Evidence of Terminology Overlap

WordPress documentation explicitly links template behavior to layout semantics.
This supports SkyCMS in choosing a more precise canonical term while recognizing
that many users arrive with different vocabulary.

### Documentation Strategy as Part of the Decision

Terminology standardization succeeds only if users are taught the model. SkyCMS
will include the following documentation strategy:

1. Glossary-first definition
  Define Layout as structural composition and distinguish it from style-oriented
  concerns.
2. Concept boundary guidance
  Clarify relationships among Layout, Template, and Design language used in other
  ecosystems.
3. Cross-platform terminology crosswalk
  Show how SkyCMS Layout maps to terms users may encounter elsewhere.
4. Contextual microcopy and helper text
  Reinforce meaning in UI where users choose or change layouts.
5. Neutral comparative language
  Explain differences between platforms without dismissive framing.

This strategy preserves thought process and intent for future developers while
reducing onboarding friction for editors.

## Alternatives Considered

### Keep Site Design as Primary Term

Rejected because it is less precise for the modeled object and increases
semantic drift from code and domain language.

### Keep Dual Terms Indefinitely (Site Design and Layout)

Rejected because persistent dual naming keeps translation burden in place and
preserves ambiguity instead of resolving it.

### Standardize on Template as Primary Term

Rejected because Template is implementation-oriented and ecosystem-dependent. In
SkyCMS, template-like artifacts may exist, but the top-level concept is
structural composition, better expressed as Layout.

### Standardize on Page Design as Primary Term

Rejected because Design remains broader than the specific structural meaning
required by this object.

## Consequences

### Positive Outcomes

- Stronger conceptual clarity for users and contributors
- Better alignment between UI language and technical model
- Reduced support and onboarding confusion
- More stable foundation for future documentation and feature naming

### Constraints Introduced

- Existing references to Site Design require gradual migration
- Documentation updates must preserve backward discoverability for old terms
- New features must follow established term boundaries

## Implementation Guidance

1. Adopt Layout as canonical in all new UI copy and documentation
2. Add transitional alias support where needed for discoverability
3. Prefer Page Layout in explanatory labels when additional clarity is helpful
4. Update glossary and contributor guidance before or alongside UI changes
5. Track migration of legacy labels as a staged documentation and UX effort

## Evidence

- Historical SkyCMS intent and internal model usage of Layout
- Comparative CMS documentation review including WordPress, Drupal, Sitecore,
  Kentico, and Umbraco terminology patterns
- WordPress template documentation indicating that template behavior defines the
  layout around content and overlaps with page layout or site design phrasing

## Future Intent

This ADR establishes a terminology contract for SkyCMS:

- Layout is structural and canonical.
- Design language can be used for broader styling contexts, not as a replacement
  for Layout.
- External terminology differences should be documented through translation,
  not internal inconsistency.

Any future proposal that changes the canonical term must supersede this ADR and
explicitly justify the shift in terms of user clarity, architecture coherence,
and long-term maintainability.