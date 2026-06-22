# ADR 0044: AI Editor Context Schema with Layered Delivery and Entity Awareness

## Status

Accepted

## Context

SkyCMS integrates AI assistance (inline completions, chat, and actions) into Monaco and CKEditor instances.
AI models require sufficient context to provide accurate, safe, and relevant suggestions without being
overwhelmed by excessive or irrelevant data.

Prior implementations either sent minimal context (losing accuracy) or dumped entire documents and layouts
into prompts (losing efficiency). A structured, layered approach was needed to balance context richness
with token efficiency and safety constraints.

## Design Goals

This decision aims to:

1. Define a structured, typed context contract for all AI requests (startup, actions, on-demand)
2. Minimize token overhead by layering context (always-send, conditional, on-demand)
3. Enable entity-aware AI assistance that understands article → layout → template rendering flow
4. Include safety and preservation rules to prevent AI from breaking SkyCMS structure and conventions
5. Support editing-intent awareness so AI context shifts based on the user's action (explain, fix, generate, etc.)
6. Enable extensibility for new editing surfaces and document kinds as the system evolves

## Non-Goals

This decision does not attempt to:

- Define AI prompt engineering or system prompts (that is separate from context contract)
- Specify AI model selection or routing policies
- Replace human review or validation for published content
- Guarantee AI output quality or safety (context is necessary but not sufficient)
- Solve all cross-entity knowledge problems (e.g., full site-wide component registries)

## Decision

SkyCMS uses a typed, layered context schema for all AI integration:

### Layered Delivery Model

**Always-send** (startup and all requests):
- Base editor context (surface, document kind, current field, language, AI status)
- Entity context (article, layout, or template properties)
- Validation context (errors and warnings if present)

**Conditionally-send** (on demand based on intent or entity type):
- Rendering context (layout + template flow)
- Knowledge context (documentation, constraints, editorial rules)
- Recent changes context (edit history)

**Fetch-on-demand** (user explicitly requests or system detects need):
- Full layout/template markup
- Detailed documentation pages
- Cross-entity references

### Entity-Aware Context

Context is shaped by the editing surface:
- **Article editing**: full article properties, layout/template references, rendering flow
- **Layout editing**: region definitions, placeholders, included stylesheets/scripts
- **Template editing**: composition contract, expected fields, rendering rules

### Intent-Aware Context

Context adaptation based on editing intent:
- `explain`: emphasize document structure, rendering flow, related documentation
- `fix-syntax`: emphasize validation errors, constraints, preservation rules
- `generate-section`: emphasize structure, related sections, markdown/HTML constraints
- `convert-selection`: emphasize target language requirements, layout safety
- `optimize`: emphasize performance, SEO, or accessibility rules per document kind

### Safety-First Preservation

Every context includes:
- Preservation rules (what must not be changed)
- Anti-patterns (common mistakes to avoid)
- Technical constraints (allowed elements, script loading rules, etc.)

## Detailed Rationale

### Why Layered?

Sending full layout and template markup, plus complete documentation, on every startup causes unnecessary token overhead and slower AI response times. Layering allows us to:
- Initialize quickly with essential metadata and current content
- Expand to full rendering context when multi-entity understanding is needed
- Fetch deep documentation only when explicitly requested

This balances AI accuracy with API latency and cost.

### Why Entity-Aware?

Articles are meaningless without understanding their layout and optional template. Layouts are meaningless without knowing which placeholders are required. This creates a rendering contract that the AI must understand to avoid suggesting changes that break the page. By structuring context around entities and their relationships, the AI makes better decisions.

### Why Intent-Aware?

"Fix syntax" and "explain file" are fundamentally different tasks with different context needs. Explaining a file benefits from full structure and related documentation. Fixing syntax benefits from validation errors and constraints. Intent-aware context avoids wasting tokens on irrelevant details.

### Why Safety Rules?

SkyCMS uses reserved placeholders (<!-- [CONTENT] -->), editor-injected hooks, and rendering-time directives that the AI cannot see but must preserve. Explicit preservation rules and anti-patterns help the AI avoid suggesting changes that look harmless but would break publish-time rendering or layout regions.

## Alternatives Considered

### Single Monolithic Prompt

Send all document content, layout, template, documentation, and rules in one large prompt.

**Rejected** because it wastes tokens, increases latency, and makes it hard for the AI to distinguish relevant from irrelevant context.

### Minimal Context Only

Send only the current field value and basic metadata.

**Rejected** because the AI lacks rendering awareness and may suggest changes that break layouts or templates. Entity relationships are critical for safety.

### User-Configurable Context Layers

Allow users to toggle what context is sent per request.

**Rejected** as initial approach because it adds complexity and most users need consistent, opinionated defaults. Can be revisited later.

### Separate Prompts for Startup vs. Action

Different payload structure for initialization vs. action requests.

**Rejected** in favor of consistent structure across all payloads (startup is just the minimal subset of the action payload).

## Consequences

### Positive Outcomes

- AI has sufficient entity awareness to avoid breaking layouts, templates, and rendering
- Context overhead is optimized through layering
- Preservation rules and constraints are explicit in context, improving suggestion safety
- Intent-aware context focuses AI effort on the user's actual task
- Extensible structure supports new editing surfaces and document kinds without schema overhaul
- Structured types enable validation and testing before sending to AI

### Constraints Introduced

- Context builders must be implemented for each entity type and surface
- Client and server must remain in sync on context structure and truncation rules
- AI system prompt design is now constrained by the assumption of structured context (not free-form prompts)
- Token limits per field require truncation logic; contributors must understand when context is elided
- Documentation and constraints must be actively maintained as editorial/technical rules evolve

## Implementation Guidance

### Context Builders

Implement service methods to construct payloads per entity type:
- `BuildArticleContextAsync(articleNumber)` → ArticleEntityContext
- `BuildLayoutContextAsync(layoutId)` → LayoutEntityContext
- `BuildTemplateContextAsync(templateId)` → TemplateEntityContext
- `BuildRenderingContextAsync(articleNumber)` → RenderingContext (multi-entity)

### Truncation and Synthesis

- Truncate field values > 50KB; provide first 1000 chars + summary
- Generate concise layout region summaries rather than full markup in startup payload
- Cache entity context during a session to reduce redundant API calls

### Validation

- Validate context payload structure before sending to AI
- Log context sizes to monitor token efficiency
- Periodically audit preservation rules and constraints for accuracy

### Client-Side Collection

- Capture `currentFieldValue` from active Monaco/CKEditor model
- Collect `currentSelection` from editor state
- Build `unsavedChanges` list from form diff or dirty-tracking
- Gather `recentEdits` from editor event log (if available)

## Evidence

- Complete context schema specification:
  - Editor/docs/ai-editor-context-schema.md
- Reference implementation examples for Article, Layout, and Template editing
- Integration with Monaco editor chat and inline completion surfaces:
  - Editor/wwwroot/js/editors/monaco-editor-chat.js
  - Editor/wwwroot/js/editors/monaco-editor-copilot.js
- AI proxy context handling:
  - Sky.Api/Controllers/AiProxyController.cs (planned)

## Related Documentation

For the complete technical specification including TypeScript interface definitions, layered payload examples, and implementation patterns, see:

- [AI Editor Context Schema Reference](../ai-editor-context-schema.md)
  - Core context interfaces (EditorContextBase, ArticleEntityContext, LayoutEntityContext, etc.)
  - Payload structure examples (startup, action request, on-demand expansions)
  - Concrete JSON examples for article and layout editing
  - Implementation notes on truncation, caching, and privacy

### Concrete Example: Article Content Editing (Startup)

For illustrative purposes, the [reference specification](../ai-editor-context-schema.md#example-1-article-content-editing-startup) includes a complete JSON example showing how article editing context is structured.

---

## Implementation Roadmap

Planned work to realize this ADR:

1. **Create context builders** for Article, Layout, and Template entity types in Sky.Api or Sky.Cms.Api.Shared
2. **Implement payload validation** to ensure context structure matches schema before sending to AI
3. **Integrate with AI proxy** to send layered payloads on editor initialization and user requests
4. **Monitor token usage** to verify layering is effective and adjust truncation as needed
5. **Add client-side collection** logic to Monaco and CKEditor integrations to gather current field values and selections
6. **Extend with new entities** (Blog, Settings, etc.) as new editing surfaces are added

See the reference specification for detailed implementation patterns.
