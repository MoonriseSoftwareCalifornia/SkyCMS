# ADR 0014: Conditional OAuth Provider Registration Strategy

## Status

Accepted

## Context

SkyCMS supports optional external identity providers such as Google OAuth and Microsoft
(Entra ID) OAuth. Deployments vary widely: some use only local accounts, some one provider,
and others tenant-specific identity constraints.

Hard-requiring OAuth configuration would reduce deployment flexibility and complicate
self-hosted or minimal environments.

A conditional registration strategy was needed so OAuth providers are enabled only when
properly configured.

## Design Goals

This decision aims to:

1. Keep OAuth providers optional and configuration-driven
2. Avoid broken startup/auth flows when provider secrets are absent
3. Support Microsoft tenant-specific endpoint customization
4. Support callback domain customization where required
5. Keep authentication composition explicit in startup

## Non-Goals

This decision does not attempt to:

- Mandate SSO in all deployments
- Define complete external identity governance policy
- Replace local account authentication
- Implement provider onboarding UX in this ADR

## Decision

SkyCMS conditionally registers OAuth providers based on configuration validity:

- Google provider is added only when GoogleOAuth is present and configured
- Microsoft provider is added only when MicrosoftOAuth is present and configured
- Microsoft OAuth supports optional tenant-specific authorization/token endpoints
- Microsoft OAuth supports optional callback-domain redirect adjustment

## Detailed Rationale

### Deployment Flexibility

Optional provider registration supports diverse environments without forcing unnecessary
identity dependencies.

### Safer Startup Composition

Only registering configured providers avoids partial auth setup and reduces runtime surprises.

### Enterprise Identity Compatibility

TenantId and callback domain customization support common enterprise SSO constraints.

## Alternatives Considered

### Always Register All Providers

Rejected because missing credentials would create fragile or broken auth behavior.

### Separate Builds Per Auth Provider

Rejected because it increases operational and maintenance complexity.

### External-Provider-Only Authentication

Rejected because local accounts remain a required/valuable option in many scenarios.

## Consequences

### Positive Outcomes

- Flexible authentication composition across deployments
- Cleaner startup behavior with fewer invalid provider states
- Better support for enterprise Entra ID requirements

### Constraints Introduced

- Configuration quality directly affects provider availability
- Callback customization logic must be preserved during auth changes
- Provider-specific behavior requires targeted testing when modified

## Evidence

- Conditional OAuth provider registration and configuration:
  - Editor/Program.cs
- Microsoft tenant endpoint and callback customization behavior:
  - Editor/Program.cs
