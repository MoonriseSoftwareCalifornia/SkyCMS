# Feature Request: Built-in Ollama Fallback for Editor AI

## Summary

Add a built-in Ollama fallback to SkyCMS Editor so AI features remain available when a tenant has not configured a paid AI provider such as GitHub Copilot or another hosted AI service.

The implementation should:

- Reuse the existing editor AI proxy architecture rather than adding a second AI pipeline
- Keep the current Monaco and CKEditor integrations working through the same backend endpoints
- Prefer a configured paid provider when present
- Fall back to a deployment-local Ollama instance when paid AI is not configured and tenant fallback is enabled
- Fit the current Docker deployment model where Editor and Publisher already run as separate containers

## Problem Statement

SkyCMS already has editor-facing AI infrastructure for:

- Chat assistance
- Inline completion
- Model discovery
- User model preference storage
- Tenant-aware AI provider settings
- Context enrichment for documentation and layout editing

Today, those features effectively depend on a configured external AI provider. That creates a gap for:

- Self-hosted deployments
- Cost-sensitive deployments
- Private or internal environments
- Organizations that want local inference for privacy or governance reasons
- Tenants who want AI assistance without requiring a paid AI subscription for every environment

This is especially relevant in SkyCMS because the application is already deployed in containers. The current deployment shape is already close to supporting a colocated local inference service, but there is no first-class fallback path in the current AI architecture.

## Current State

### Existing AI Integration Surface

SkyCMS already has a reusable backend AI surface in:

- `Editor/Controllers/AiProxyController.cs`
- `Editor/Controllers/SkyCmsSettingsController.cs`
- `Editor/Services/Copilot/CopilotProxyOptionsService.cs`
- `Editor/Services/Copilot/AiProviderModelCatalogService.cs`
- `Editor/Services/Copilot/AiProviderMetadataResolver.cs`
- `Editor/Services/Copilot/AiUserPreferenceService.cs`
- `Editor/Services/Copilot/AiDocumentationContextService.cs`
- `Editor/Services/Copilot/AiLayoutContextService.cs`

Current editor surfaces already using the shared AI backend include:

- Monaco/code editor: `Editor/Views/Shared/_CodeEditorShared.cshtml`
- CKEditor/live editor: `Editor/Views/Editor/Edit.cshtml`
- AI help surface: `Editor/Views/Editor/AiHelp.cshtml`

This means the foundational pieces already exist. The main missing capability is runtime resolution of a built-in local provider when a paid provider is unavailable or not configured.

### Existing Deployment Shape

The current Docker deployment already uses separate sibling services for Editor and Publisher via:

- `docker-compose.yml`
- `docker-compose.override.yml`

That makes a sibling Ollama container a natural fit for the deployment topology.

## Requested Feature

Add a tenant-aware built-in Ollama fallback for SkyCMS Editor AI.

Recommended behavior:

1. If a tenant has a valid paid AI provider configured, SkyCMS uses that provider.
2. If a tenant does not have a valid paid AI provider configured, SkyCMS can fall back to a built-in Ollama instance when fallback is enabled for that tenant.
3. If neither paid AI nor Ollama fallback is available, the editor should surface a clear unavailable state.

The feature should cover the existing AI flows:

- Chat assistant
- Inline completion
- Model discovery
- User model selection and preference persistence
- Existing documentation and layout context enrichment

## Goals

- Keep AI available in self-hosted deployments without requiring paid AI subscriptions
- Reuse the current AI proxy architecture
- Preserve a single backend contract for Monaco and CKEditor
- Keep multi-tenant behavior explicit and predictable
- Support Docker-based deployments cleanly
- Keep failure behavior observable rather than silent

## Non-Goals

- Embedding Ollama into the ASP.NET process
- Running Ollama on each editor workstation in phase 1
- Automatic model download/orchestration by SkyCMS in phase 1
- Advanced provider load balancing in phase 1
- Major editor UX redesign in phase 1
- Replacing paid providers as the preferred option when explicitly configured

## Recommended Architecture

### 1. Provider Resolution Layer

Introduce a scoped provider resolver used by all AI entry points.

The resolver should answer:

- Which provider is active for the current request
- Why it was selected
- Which model should be used
- Whether model discovery is supported
- Whether user model selection is supported

Recommended precedence:

1. Explicitly configured paid provider if valid and enabled
2. Built-in Ollama if tenant fallback is enabled and Ollama is configured
3. Unavailable otherwise

This resolution logic should be reused by:

- `AiProxyController`
- `AiProviderModelCatalogService`
- `AiUserPreferenceService`
- AI status endpoints

### 2. Separate Settings Model for Built-in Fallback

Do not overload the existing paid-provider settings model with fallback semantics.

Recommended tenant-scoped built-in provider settings:

- `Enabled`
- `FallbackWhenNoPaidProviderConfigured`
- `Endpoint`
- `DefaultModel`
- `TimeoutMs`
- `Temperature`
- `MaxTokens`
- Optional `DisplayName`

This keeps administrator intent explicit and avoids confusing combinations of cloud-provider and local-provider semantics in a single settings record.

### 3. Provider Adapter Boundary

Keep provider-specific HTTP behavior out of the controller by introducing provider adapters or equivalent service abstractions.

The controller should be responsible for:

- Input validation
- Provider resolution
- Context enrichment orchestration
- Response mapping to the existing frontend contract

Provider adapters should handle:

- Endpoint-specific request mapping
- Response parsing
- Provider-specific authentication handling
- Model discovery behavior
- Capability metadata

### 4. Ollama Adapter

Add an Ollama adapter that supports:

- Chat requests
- Completion-style requests needed by current editor flows
- Model discovery
- Status checks

Phase 1 recommendation:

- Prefer non-streaming support first unless the current UI depends on streaming to remain usable
- Assume no authentication for internal compose-network traffic by default
- If operators need external exposure, recommend fronting Ollama with a secured reverse proxy rather than opening the service directly

### 5. Existing Context-Enrichment Reuse

Keep the current enrichment services in place:

- `IAiDocumentationContextService`
- `IAiLayoutContextService`

Notes from current behavior that should be preserved:

- Monaco code-editor chat intentionally keeps cross-field context within a multi-part editing session, especially for layouts where head, body-start, and body-end are one logical document split across injection points. This should be treated as a design tradeoff, not an automatic bug.
- CKEditor live-editor apply behavior should continue to prefer safety when saved selection snapshots no longer match the current region state.

If local models struggle with prompt size, add context budgets or truncation strategies at the backend service boundary rather than branching behavior heavily by editor.

## Recommended Deployment Model

### Docker Compose Topology

Add Ollama as a sibling container in the same Docker Compose deployment as:

- `sky.editor`
- `sky.publisher`

Recommended compose-network endpoint from the editor container:

- `http://ollama:11434`

Do not use `localhost` from inside the editor container because that points back to the editor container itself.

Recommended deployment details:

- Add an `ollama` service to `docker-compose.yml`
- Add a persistent volume for model storage
- Add a health check
- Add optional GPU/runtime configuration in `docker-compose.override.yml` where supported
- Keep Ollama outside the ASP.NET process boundary

This deployment model matches the existing containerized architecture and keeps operations isolated.

## Editor UX Expectations

Phase 1 should keep the current frontend routes unchanged.

The editor should continue to call the same backend endpoints for:

- Status
- Model catalog
- Chat
- Completion
- Model preference persistence

The backend should expose enough provider metadata for the UI to indicate:

- Whether AI is available
- Which provider was selected
- Which model is active when appropriate

This is important because local model quality and latency will differ from paid providers, and users need clarity about what is serving their request.

## Failure and Degraded-State Handling

The feature should define explicit behavior for:

- Paid provider not configured
- Paid provider configured but invalid
- Built-in Ollama fallback disabled
- Built-in Ollama selected but unavailable
- Empty or unreadable Ollama model catalog
- Startup race conditions during container boot

Required behavior:

- AI status endpoints should report unavailability explicitly
- UI should not silently hang on unavailable provider paths
- Model list failures should degrade gracefully
- Operators should have enough information to distinguish configuration failure from runtime outage

## PR-by-PR Execution Plan

### PR 1: Provider Resolution Foundation

Scope:

- Introduce a scoped AI provider resolver
- Define provider resolution result model
- Centralize provider selection logic for status, models, chat, and completion
- Add tests for precedence logic

Primary files likely involved:

- `Editor/Controllers/AiProxyController.cs`
- `Editor/Services/Copilot/AiProviderMetadataResolver.cs`
- `Editor/Program.cs`
- `Tests/Controllers/CopilotControllerTests.cs`

Acceptance for PR 1:

- Paid provider wins when configured
- Built-in fallback can be selected when paid provider is absent
- Unavailable state is explicit when nothing is usable

### PR 2: Built-in Ollama Settings Model

Scope:

- Add tenant-scoped settings for built-in fallback
- Add retrieval, save, and remove patterns consistent with existing AI settings flow
- Add cache invalidation and settings validation

Primary files likely involved:

- `Editor/Models/CopilotProxyOptions.cs` or companion built-in settings model
- `Editor/Services/Copilot/CopilotProxyOptionsService.cs`
- `Editor/Features/Copilot/GetSettings/*`
- `Editor/Features/Copilot/SaveSettings/*`
- `Editor/Features/Copilot/RemoveSettings/*`
- `Tests/Editor/Services/Copilot/CopilotProxyOptionsServiceTests.cs`

Acceptance for PR 2:

- Tenant can persist built-in fallback settings independently of paid-provider settings
- Cache and invalidation behavior remain tenant-safe

### PR 3: Provider Adapter Refactor

Scope:

- Move provider-specific HTTP logic out of `AiProxyController`
- Introduce provider adapter boundary
- Keep public endpoint behavior stable

Primary files likely involved:

- `Editor/Controllers/AiProxyController.cs`
- `Editor/Services/Copilot/*`
- `Tests/Controllers/CopilotControllerTests.cs`

Acceptance for PR 3:

- Controller becomes orchestration-only
- Existing cloud-provider behavior remains unchanged
- Tests prove no regression in current routes

### PR 4: Ollama Adapter and Model Discovery

Scope:

- Add Ollama provider adapter
- Add Ollama model discovery support
- Wire provider capabilities into model catalog responses
- Keep user model preferences isolated by provider key

Primary files likely involved:

- `Editor/Services/Copilot/AiProviderModelCatalogService.cs`
- `Editor/Services/Copilot/AiUserPreferenceService.cs`
- `Editor/Services/Copilot/AiProviderMetadataResolver.cs`
- `Tests/Controllers/CopilotControllerTests.cs`

Acceptance for PR 4:

- Model discovery works for Ollama
- User model preferences do not cross-contaminate between paid provider and Ollama
- Chat/completion requests can be routed to Ollama successfully

### PR 5: Admin Settings UX and Availability Messaging

Scope:

- Extend AI configuration UI to support built-in fallback settings
- Make provider resolution state visible to tenant admins
- Improve unavailability messaging in editor-facing status flows

Primary files likely involved:

- `Editor/Controllers/SkyCmsSettingsController.cs`
- Settings-related views and view models
- `Editor/Views/Shared/_CodeEditorShared.cshtml`
- `Editor/Views/Editor/Edit.cshtml`

Acceptance for PR 5:

- Tenant admin can see whether paid, fallback, both, or neither are configured
- Editor surfaces show a clearer availability state

### PR 6: Compose Deployment and Operator Guidance

Scope:

- Add `ollama` service to compose configuration
- Add persistent model volume and health check guidance
- Add optional development and GPU override guidance
- Document local and production deployment expectations

Primary files likely involved:

- `docker-compose.yml`
- `docker-compose.override.yml`
- Docs surfaces under `SkyCMS.Docs` or repo docs as appropriate

Acceptance for PR 6:

- Editor, Publisher, and Ollama can run in the same stack
- Documented endpoint is service-name based, not loopback based
- Operators have clear setup guidance

### PR 7: Hardening and Follow-up Enhancements

Scope:

- Evaluate streaming support if still needed
- Add prompt-size guardrails for local models if required
- Add operator diagnostics or health visibility if needed

Acceptance for PR 7:

- Local-model experience is stable enough for supported deployment profiles
- Operational visibility is adequate for support and rollout

## Testing Strategy

Add focused automated coverage for:

- Provider precedence resolution
- Built-in settings persistence and cache invalidation
- Model discovery for paid provider and Ollama
- Chat and completion routing
- User model preference isolation by provider key
- Availability and degraded-state behavior

Recommended manual validation matrix:

1. Paid provider only
2. Built-in Ollama only
3. Both configured
4. Neither configured
5. Ollama selected but container down

Cross-editor validation should confirm Monaco and CKEditor continue working through the same backend contract.

## Risks and Tradeoffs

### Model Quality and Latency

Local models will differ from paid providers in latency, quality, and context-window behavior. The UI should not hide which provider is currently active.

### Resource Requirements

CPU-only deployments may need smaller default models and clear guidance around response times. GPU support should be optional, not assumed.

### Operational Complexity

This feature reduces dependency on paid services but increases local operational responsibility. Documentation needs to be clear that “built-in” means operator-managed local service inside the deployment, not magic in-process inference.

### Scope Creep

The first release should focus on fallback availability, not full local-model lifecycle management.

## Suggested Acceptance Criteria

- A tenant with a valid paid AI provider configured continues to use the paid provider
- A tenant without paid AI configured can use AI features when built-in Ollama fallback is enabled and reachable
- Model discovery works for both provider paths
- User model preferences remain isolated by provider
- Existing documentation and layout context enrichment continue to function
- AI status and editor UX clearly report unavailable or degraded states
- Docker Compose guidance exists for running Editor, Publisher, and Ollama in the same stack
- Controller and service tests cover provider resolution, fallback behavior, and failure states

## Additional Notes

- Multi-tenant behavior should continue to flow through existing tenant-scoped settings and request-scoped services.
- This feature should preserve the existing backend AI contract rather than introducing editor-specific provider logic in multiple places.
- The deployment recommendation is a sibling Ollama container, not a change to the Editor or Publisher container images themselves.
- The long-term path can add better provider diagnostics, streaming, and model capability reporting after the basic fallback path is stable.