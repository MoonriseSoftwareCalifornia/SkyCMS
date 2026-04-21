# Feature Request: AI Help Chat Retrieval + Optional Web Search

## Summary
Add a retrieval layer to the AI Help chat experience so answers can be grounded in authoritative sources, with optional external web search when allowed by tenant policy.

This extends the current `AiProxyController` help chat modes (`general-help`, `site-help`) so users can get answers based on:
1. SkyCMS documentation (first-party),
2. Tenant/site context,
3. Optionally approved external sources.

## Problem Statement
Today the AI Help chat can use provided prompt context and model knowledge, but it does not perform live retrieval from docs/web per request. This can lead to:
- stale or generic answers,
- reduced trust in answers,
- no visible citations for verification.

For SkyCMS users, especially editors and admins, grounded answers with source links are needed for confidence and auditability.

## Goals
- Ground AI Help responses in retrieved source snippets.
- Prioritize SkyCMS docs as the primary source.
- Support optional external retrieval under explicit controls.
- Return citations/sources in responses.
- Keep tenant safety, policy controls, and rate limits intact.

## Non-Goals
- Replacing existing Monaco/CKEditor coding and writing flows.
- Full autonomous web browsing.
- Unrestricted open-web crawling without governance.

## User Stories
1. As an editor, I can ask “How do I configure X in SkyCMS?” and get an answer grounded in SkyCMS docs with source links.
2. As an admin, I can enable/disable external search for my tenant.
3. As an author, I can see which source(s) informed the answer.
4. As a reviewer, I can trust answers because unsupported claims are reduced.

## Scope
### In Scope
- Retrieval orchestration in AI Help path.
- Source/citation metadata in chat responses.
- UI toggle for external sources in AI Help page.
- Tenant-safe controls (allowlist, timeout, quotas).

### Out of Scope
- Changes to inline completion (`/api/ai-proxy/complete`).
- Replacing existing provider model selection flow.
- New public API surface outside current AI proxy routes.

## Proposed Architecture

### 1) Retrieval Service Layer
Introduce a new scoped abstraction:
- `IAiRetrievalService`

Suggested responsibilities:
- Build retrieval query from user message + context.
- Query first-party docs retrieval source.
- Optionally query external provider(s) when enabled.
- Normalize results into source items (title/url/snippet/score/sourceType).

Suggested model (illustrative):
- `AiRetrievalRequest`
- `AiRetrievalResult`
- `AiRetrievedSource`

### 2) Controller Integration
Update `AiProxyController.Chat` help path:
- If help mode (`general-help`/`site-help`), call retrieval service before upstream model call.
- Add retrieved snippets to prompt context with strict token caps.
- Instruct model to ground answers in supplied sources and acknowledge uncertainty.
- Return citations as part of chat response metadata.

### 3) Existing Context Services
Reuse existing context services where applicable:
- `IAiDocumentationContextService`
- `IAiLayoutContextService`

These remain useful for page/layout context enrichment, while retrieval adds source-backed grounding.

### 4) UI/UX (`AiHelp`)
In `Editor/Views/Editor/AiHelp.cshtml` + `Editor/wwwroot/js/editors/ai-help-chat.js`:
- Add toggle: “Use external sources” (default off unless tenant policy allows and user enables).
- Render source links under assistant reply.
- Show source type labels (e.g., `SkyCMS Docs`, `External`).

## Security and Governance Requirements
- External retrieval must be disabled by default.
- Tenant-configurable allowlist for external domains.
- Strip tracking parameters from citation URLs.
- Enforce timeouts, max results, and max snippet sizes.
- Respect existing auth/roles and rate limiting (`copilot-chat`).
- Log retrieval provider failures without exposing sensitive details.

## Performance and Cost Controls
- Per-request retrieval timeout (e.g., 1-2 seconds budget).
- Cap retrieved chunks and total retrieval tokens.
- Cache frequent retrieval queries (short TTL).
- Fall back gracefully when retrieval unavailable.

## API Contract Changes (Proposed)
### Request (`CopilotChatRequest`)
Add optional fields:
- `AllowExternalSearch` (`bool?`)
- `RetrievalMode` (`string?`: `docs-only`, `docs-plus-approved-web`)
- `MaxSources` (`int?`)

### Response (`CopilotChatResponse`)
Add optional metadata:
- `Sources` (`List<CopilotChatSource>`)

Where `CopilotChatSource` includes:
- `Title`
- `Url`
- `Snippet`
- `SourceType` (`docs`, `external`)
- `Score` (optional)

## Rollout Plan
### Phase 1: Docs-grounded Help
- Implement retrieval using SkyCMS docs + existing context services.
- Add citations in response.
- No external web.

### Phase 2: Approved External Sources
- Add tenant-controlled external source toggle.
- Enforce allowlist + safety caps.

### Phase 3: Optimization
- Add caching and ranking improvements.
- Refine citation UI and telemetry.

## Acceptance Criteria
1. Help chat can return citations for docs-grounded answers.
2. External retrieval is disabled unless explicitly enabled.
3. In disabled mode, no external source is queried.
4. If retrieval fails, chat still returns a safe fallback response.
5. Existing chat behaviors for Monaco/CKEditor remain unchanged.
6. Unit tests and integration tests cover retrieval enabled/disabled paths.

## Testing Strategy
- Update/add tests in `Tests/Controllers/CopilotControllerTests.cs`:
  - help mode with docs retrieval,
  - help mode with external disabled,
  - citation payload shape,
  - retrieval timeout/failure fallback.
- Add service-level tests for retrieval normalization and policy enforcement.
- Run solution build and targeted tests.

## Risks
- Hallucinated citations if prompt and source mapping diverge.
- Increased latency and cost per help request.
- Governance/compliance concerns with external domains.

## Mitigations
- Strict source grounding prompt instructions.
- Return only citations from retrieved source objects.
- Conservative limits and fallback behavior.
- Tenant-level controls and explicit UX signaling.

## Open Questions
1. Which external provider should be used first (if any)?
2. Should external search be tenant opt-in only or user opt-in within tenant policy?
3. Should we persist user retrieval preference similar to launch mode?
4. Do we want per-role policy (e.g., admins can use external, authors cannot)?

## Suggested Initial Tasks
1. Add retrieval contracts and service interface.
2. Implement docs-only retrieval provider.
3. Integrate into `AiProxyController.Chat` help path.
4. Extend response with `Sources`.
5. Add UI rendering for citations.
6. Add tests and validate build.
