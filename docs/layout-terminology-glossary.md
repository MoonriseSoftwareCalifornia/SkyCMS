# Layout Terminology Glossary

## Purpose

SkyCMS uses Layout as the canonical term for structural page composition.

This glossary explains term boundaries and provides a translation guide for users
coming from other CMS platforms.

For the formal architecture decision, see:
- docs/adr/0036-layout-terminology-standardization-and-documentation-strategy.md

## Canonical Terms in SkyCMS

### Layout (Canonical)

A Layout defines the structural arrangement around content, including regions,
hierarchy, and reading flow.

Typical layout responsibilities include:
- header/footer structure
- content region arrangement
- section and block placement patterns
- predictable structural consistency across pages

In SkyCMS, Layout is the source term used in:
- user-facing product language
- domain model and code concepts
- routing and architecture discussions

### Page Layout (Descriptive UI Label)

Page Layout is a user-friendly phrasing used when additional clarity is helpful.
It is descriptive only and maps directly to the canonical term Layout.

### Template (Related but Different)

A Template is an implementation artifact used to render page output.

A template may express one layout variant, but template and layout are not the
same concept in SkyCMS.

### Design / Site Design (Legacy or Broad Label)

Design is a broader term that can include visual identity and style systems
(fonts, colors, spacing, branding).

Site Design is a legacy label previously used in some SkyCMS UI and setup flows.
During transition, some UI surfaces may display:
- "Layouts (formerly Site Design)"

## Why SkyCMS Standardizes on Layout

SkyCMS serves editorial and publishing scenarios where structure and flow are
first-class concerns.

Layout provides the most precise and stable term for:
- structural intent
- repeatable composition patterns
- architecture and UX consistency

Using one canonical term reduces translation burden between UI, docs, and code.

## Cross-Platform Term Crosswalk

Different CMS platforms use overlapping labels for similar behavior.

Use this crosswalk as a translation guide:

- SkyCMS Layout -> often called Template or Page Design in other systems
- SkyCMS Page Layout -> explanatory phrasing for Layout
- SkyCMS Template -> implementation-level rendering artifact
- SkyCMS Design -> broader visual/style context, not the primary structural term

## Migration Guidance for Existing Users

If you previously used the term Site Design in SkyCMS:
- treat Site Design as the legacy label
- use Layout going forward
- expect transitional helper text in parts of the UI

## Documentation Language Rules

When writing SkyCMS docs:
- Prefer Layout as the canonical term for structure
- Use Page Layout only for clarification in user-facing prose
- Avoid switching between Layout and Site Design for the same concept
- Use neutral comparative language when referencing other CMS terminology
