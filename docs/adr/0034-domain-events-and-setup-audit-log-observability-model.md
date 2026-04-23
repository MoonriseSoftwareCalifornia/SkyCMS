# ADR 0034: Domain Events and Setup Audit Log Observability Model

## Status

Accepted

## Context

SkyCMS requires observability for both runtime domain actions (publish/title/redirect/catalog)
and administrative setup changes. Coupling all side effects directly into primary workflows
would reduce maintainability and auditability.

A unified observability model was needed that supports event-driven side effects plus
structured setup-change tracking.

## Design Goals

This decision aims to:

1. Decouple side effects from primary domain operations
2. Support structured event handling and logging across domain actions
3. Persist setup-related administrative changes in an audit trail
4. Improve traceability for operational and compliance use cases
5. Keep observability architecture extensible as features grow

## Non-Goals

This decision does not attempt to:

- Define enterprise SIEM integration details
- Guarantee immutable audit storage semantics for all environments
- Replace all business telemetry requirements
- Implement every future observability UI/reporting feature

## Decision

SkyCMS uses an event-driven domain model for runtime side effects and a structured setup
audit-log model for configuration/setup operations:

- Domain events are handled by specialized handlers and logging-oriented handlers
- Setup audit records capture who changed what and when, with sensitive data masking

This establishes a dual observability approach for runtime behavior and setup governance.

## Detailed Rationale

### Decoupled Side-Effect Processing

Domain events allow multiple handlers to respond without tightly coupling core workflows to
all downstream actions.

### Setup Governance Traceability

Structured setup audit records support accountability and post-change investigation.

### Extensible Observability Foundation

Event and audit patterns can evolve independently as platform needs expand.

## Alternatives Considered

### Inline Side Effects Only

Rejected because tightly coupled workflows become harder to maintain and extend.

### Logs-Only Without Structured Setup Audit Records

Rejected because setup changes benefit from explicit structured audit shape.

### Separate Observability Stacks Per Feature

Rejected because it reduces architectural consistency and increases maintenance burden.

## Consequences

### Positive Outcomes

- Better separation of concerns for side effects
- Improved operational and administrative traceability
- Clear extension points for future handlers and observability tooling

### Constraints Introduced

- Event and audit schemas require careful evolution
- Observability data quality depends on disciplined handler/audit maintenance
- Documentation and tooling should keep pace with event model changes

## Evidence

- Domain events and handlers model documentation:
  - SkyCMS.Docs/for-developers/audit-logging.md
- Setup audit-log model and masking behavior documentation:
  - SkyCMS.Docs/for-developers/audit-logging.md
