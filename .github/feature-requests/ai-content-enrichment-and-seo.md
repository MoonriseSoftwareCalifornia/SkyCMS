# Feature Request: AI Content Enrichment and SEO Metadata Generation

## Summary

Add AI-assisted content enrichment to SkyCMS so editors can generate and apply:

- A concise semantic summary stored in `Introduction`
- Structured SEO metadata for pages and blog posts
- Social sharing metadata such as Open Graph and Twitter values
- Optional structured metadata such as JSON-LD where supported

This feature should work in both existing editor surfaces:

- Monaco/code editor
- CKEditor/live editor

The implementation should build on the current AI provider abstraction and editor AI infrastructure rather than introducing a separate AI pipeline.

## Problem Statement

SkyCMS already has a strong concept of a short content summary through the `Introduction` property on core content entities, including:

- `Article`
- `CatalogEntry`
- `PublishedPage`

That summary is intended to support use cases such as:

- Blog excerpts
- Article teasers
- Listing and preview text
- Meta description style reuse

Today, the platform only partially automates this behavior. Blog posts can auto-populate `Introduction` when the field is empty, but the current logic is deterministic rather than semantic. It extracts the first non-empty paragraph from the body rather than generating a true summary.

That leaves several product and workflow gaps:

1. Summary quality is inconsistent because the fallback is structural, not editorial.
2. SEO metadata is incomplete and often manually maintained.
3. Social preview fields are only partially populated.
4. AI in SkyCMS is currently more editor-assistance-oriented than publish-workflow-oriented.

## Current State

### Existing Summary Support

The content model already includes `Introduction` in the main article lifecycle entities:

- `Common/Data/Article.cs`
- `Common/Data/CatalogEntry.cs`
- `Common/Data/PublishedPage.cs`

The current save flow for articles already auto-generates `Introduction` for blog posts when the field is blank:

- `Editor/Features/Articles/Save/SaveArticleHandler.cs`

That generation currently uses HTML extraction logic from:

- `Editor/Services/Html/ArticleHtmlService.cs`

The current behavior is a good fallback, but it is not a semantic summary and does not reflect tone, topic emphasis, or SEO intent.

### Existing SEO and Social Metadata Support

The page view model already exposes several metadata-oriented fields:

- `OGImage`
- `OGDescription`
- `OGUrl`
- `Introduction`

These are defined in:

- `Common/Models/ArticleViewModel.cs`

Published-page view model construction already populates some of these values:

- `Common/Features/Articles/Shared/ArticleViewModelBuilder.cs`

At present:

- `OGImage` is populated from the banner image
- `OGUrl` is composed from the page URL and publisher URL
- `OGDescription` is currently left empty in the published-page builder

SkyCMS also already supports raw head markup injection via `HeadJavaScript`, which is used in editor and export/render flows such as:

- `Editor/Features/Articles/Save/SaveArticleHandler.cs`
- `Editor/Data/Logic/ArticleEditLogic.cs`

That means the platform can already inject head content, but raw head injection is not a good primary storage model for AI-generated SEO metadata.

### Existing AI Integration Surface

SkyCMS already has tenant-aware AI provider configuration and editor-facing AI endpoints:

- `Editor/Controllers/AiProxyController.cs`
- `Editor/Controllers/SkyCmsSettingsController.cs`
- `Editor/Services/Copilot/*`

The current editor AI experiences already exist in both editors:

- Monaco/code editor: `Editor/Views/Shared/_CodeEditorShared.cshtml`
- CKEditor/live editor: `Editor/Views/Editor/Edit.cshtml`

This provides the necessary foundation to extend AI from chat and rewrite assistance into publish-time enrichment workflows.

## Requested Feature

Add an AI-assisted content enrichment workflow that can:

1. Generate a semantic summary from the current content and place it into `Introduction`
2. Generate structured SEO metadata for the current page or post
3. Allow preview, apply, and regenerate flows in both editors
4. Preserve editor control by avoiding silent overwrites of curated content

## Goals

- Improve quality of summaries used for listings, previews, and search metadata
- Improve SEO metadata completeness and consistency
- Improve social sharing previews
- Reuse the existing AI provider infrastructure
- Keep behavior consistent across Monaco and CKEditor/live editing
- Keep deterministic non-AI fallbacks available

## Non-Goals

- Replacing all SEO strategy with AI
- Making AI the only way to populate summaries
- Storing primary SEO output as arbitrary raw head markup
- Introducing provider-specific behavior in editor UX when a shared backend contract can be used

## Proposed Solution

### 1. AI-Generated Summary for `Introduction`

Add a shared backend content enrichment service that can generate a concise summary based on:

- Title
- URL path
- Article type
- Category
- Current body content
- Existing `Introduction`
- Banner image context if useful
- Optional tenant/site context

The service should return a suggested `Introduction` that:

- Fits the existing `Introduction` field length expectations
- Is suitable for article or blog teasers
- Can also serve as a good meta description baseline

Behavioral guidance:

- If the editor has already written a custom `Introduction`, the AI suggestion should be previewed, not auto-applied
- If AI is disabled or unavailable, keep the current first-paragraph extraction fallback
- The current deterministic extractor should remain as the resilience path even after AI support is added

### 2. Structured SEO Metadata Generation

Add a structured SEO model rather than using `HeadJavaScript` as the canonical storage target.

Recommended structured fields:

- Meta description
- Canonical URL
- Open Graph title
- Open Graph description
- Open Graph image
- Twitter title
- Twitter description
- Twitter image
- Optional JSON-LD payload

Potential future fields:

- Robots directives
- Article keywords if still desired
- Article author schema hints
- Publish/updated schema hints

The AI enrichment service should generate suggestions for these values based on current content and page metadata.

### 3. Rendering Strategy

Render SEO tags into `<head>` from structured fields in the rendering/view-model pipeline.

Recommended principle:

- Structured metadata is the primary source of truth
- `HeadJavaScript` remains an advanced/manual escape hatch

This avoids:

- Duplicate tags
- Conflicting tags
- Hard-to-test freeform output
- Long-term governance problems caused by arbitrary AI-generated markup

### 4. Editor Integration

Both editor surfaces should expose the same feature set through a shared backend contract.

#### Monaco/code editor

Add actions such as:

- Generate Summary
- Generate SEO Metadata
- Regenerate Summary
- Regenerate SEO Metadata
- Apply Selected Suggestions

Recommended UX:

- Side panel or preview area
- Field-by-field apply controls where practical
- Clear indication of what will change before save

#### CKEditor/live editor

Add page-level AI actions such as:

- Generate Summary
- Generate SEO Metadata
- Apply Suggestions

Recommended UX:

- Accessible from the existing AI assistant region or page settings area
- Preview/apply/regenerate flow consistent with Monaco behavior

## Architectural Principles

1. Use one shared backend enrichment service for both editors.
2. Reuse the current AI provider abstraction.
3. Keep provider-specific differences behind the service boundary.
4. Preserve multi-tenant behavior through existing configuration and persistence layers.
5. Store SEO data in structured fields, not raw generated head fragments.
6. Keep deterministic fallback logic available when AI is not configured.

## Suggested Backend Contract

The shared enrichment service should accept inputs such as:

- Article/page title
- URL path
- Document kind
- Editor kind
- Article type
- Category
- Full content
- Existing introduction
- Existing SEO values
- Banner image
- Optional layout or site context

It should return a result shaped approximately as:

- Suggested introduction
- Suggested meta description
- Suggested OG title
- Suggested OG description
- Suggested OG image
- Suggested Twitter title
- Suggested Twitter description
- Suggested Twitter image
- Suggested canonical URL
- Optional JSON-LD
- Optional warnings or rationale

## Persistence Considerations

The introduction already has persistence paths in existing entities and save flows.

Structured SEO metadata will require a persistence design decision. Recommended options to evaluate:

1. Extend article and published-page storage with dedicated SEO properties.
2. Store SEO metadata in a related structured metadata table or owned type.
3. Use settings-style metadata only if it can be kept strongly associated with the article lifecycle.

Preferred direction:

- Persist SEO metadata as article-associated structured content rather than generic page script content.

## Phased Implementation Plan

### Phase 1: Summary Generation

- Add a shared AI enrichment service for summary generation
- Generate a suggested `Introduction`
- Keep the current HTML first-paragraph extraction fallback
- Add preview/apply behavior in both editors
- Add tests for AI summary generation and fallback behavior

### Phase 2: Core SEO Metadata

- Add structured storage for core SEO values
- Generate:
  - meta description
  - OG description
  - OG title
  - canonical URL suggestion
- Render those values in the page head from structured fields
- Add tests for metadata rendering and fallback behavior

### Phase 3: Social Metadata and Extended SEO

- Add Twitter metadata generation
- Add OG image recommendation/fallback handling
- Add optional JSON-LD generation for supported article types
- Add richer preview/apply UI in both editors

### Phase 4: Governance and Workflow Polish

- Add field-level apply controls
- Avoid silent overwrite of human-authored content
- Add change previews/diffs where useful
- Add tenant/site-level defaults or guidance prompts if needed

## Acceptance Criteria

- AI can generate a suggested `Introduction` for supported content
- The editor can preview and explicitly apply the suggestion
- Existing deterministic `Introduction` fallback remains available
- AI can generate structured SEO metadata for the current page or post
- Both Monaco and CKEditor/live editor support the feature
- Structured SEO values are rendered into page head output
- The system avoids duplicate/conflicting head tags
- Human-authored metadata is not silently overwritten
- Tests cover summary generation, fallback behavior, persistence, rendering, and editor interaction paths

## Testing Expectations

Testing should cover at minimum:

- Summary generation when AI is enabled
- Summary fallback when AI is disabled/unavailable
- Preservation of custom `Introduction`
- Length validation for generated summary and SEO values
- Structured SEO persistence
- Rendered metadata correctness in head output
- Monaco editor apply/regenerate flows
- CKEditor/live editor apply/regenerate flows

## Alternatives Considered

### Alternative A: Keep first-paragraph extraction only

Pros:

- Simple
- Deterministic
- No AI dependency

Cons:

- Not a true summary
- Weak for SEO quality
- Does not improve social metadata or broader metadata coverage

### Alternative B: Let AI generate raw head markup into `HeadJavaScript`

Pros:

- Fast to implement
- Works with current head injection paths

Cons:

- Brittle
- Hard to validate
- Easy to duplicate or conflict with future tags
- Hard to govern or maintain

### Alternative C: Generate structured metadata and render tags from it

Pros:

- Cleanest architecture
- Easiest to validate and test
- Best long-term maintainability
- Better editor UX

Cons:

- Requires persistence and rendering work

Preferred approach: Alternative C.

## Risks and Considerations

- AI quality may vary by provider or model
- Generated metadata may be repetitive or overly generic without prompt tuning
- Structured SEO storage requires careful migration and rendering coordination
- Some sites may want AI suggestions constrained by brand voice or editorial policy
- SEO quality should be editor-assisted rather than editor-replaced

## Competitive Positioning

This feature would materially improve SkyCMS positioning.

### Current strengths

- Strong AI provider flexibility
- Strong editor-facing AI integration foundation
- Good technical control for self-hosted or bring-your-own-provider deployments

### Current gap

SkyCMS currently behaves more like an AI-enabled editor than an AI-native content workflow platform.

The missing layer is publish-oriented AI assistance, especially:

- Summary generation
- SEO assistance
- Social metadata support
- Reusable enrichment workflows tied directly to publication quality

### Expected impact of this feature

Adding AI-generated summaries and structured SEO metadata would:

- Improve publish-time editorial value
- Strengthen the product story beyond rewrite/chat assistance
- Move SkyCMS closer to the stronger CMS AI positioning seen in more workflow-mature competitors

This would not completely close the gap with platforms that also provide advanced governance, bulk AI workflows, approval chains, localization pipelines, and reusable prompt recipes. However, it would be a meaningful and visible step toward that category.

## Recommended Next Steps

1. Decide the persistence model for structured SEO metadata.
2. Define the shared backend enrichment contract.
3. Implement AI-generated summary support first.
4. Keep the current deterministic extractor as fallback.
5. Add core structured SEO fields and head rendering.
6. Add Monaco and CKEditor/live editor preview/apply flows.
7. Add focused tests for save, render, and editor workflows.

## Initial Implementation Recommendation

If scope needs to be staged aggressively, start with the highest-value subset:

- AI-generated `Introduction`
- AI-generated meta description
- AI-generated Open Graph description
- Preview/apply UX in both editors

That delivers immediate editorial value while aligning directly with the existing `Introduction` concept and current metadata model.