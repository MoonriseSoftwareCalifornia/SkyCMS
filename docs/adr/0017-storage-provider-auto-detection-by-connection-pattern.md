# ADR 0017: Storage Provider Auto-Detection by Connection Pattern

## Status

Accepted

## Context

SkyCMS supports multiple object storage providers for file and publishing assets,
including Azure Blob Storage, Amazon S3-compatible storage, and Cloudflare R2.

Requiring explicit provider flags in every environment introduces configuration drift,
misclassification risk, and duplicated startup logic. A single, reusable detection
mechanism was needed to map storage configuration to the correct provider implementation.

## Design Goals

This decision aims to:

1. Select the storage provider from one canonical detection path
2. Reduce per-environment provider flag overhead
3. Keep storage initialization deterministic and testable
4. Fail clearly when connection strings are invalid or unsupported
5. Preserve extensibility for future provider additions

## Non-Goals

This decision does not attempt to:

- Hide all provider-specific capabilities
- Enforce one storage vendor across all deployments
- Replace security and secret management practices
- Remove the need for provider-specific integration tests

## Decision

SkyCMS determines storage provider type by analyzing connection string patterns
through ConnectionStringParser.DetermineProvider, then parses provider-specific
parameters through dedicated parsing methods.

Current detection conventions include:

- DefaultEndpointsProtocol=... for Azure Blob
- accountid + bucket patterns for Cloudflare R2
- bucket + region patterns for S3-compatible flows

Invalid or incomplete strings are rejected with explicit storage parsing exceptions.

## Detailed Rationale

### One Detection Path Prevents Drift

Centralizing provider detection avoids inconsistent provider selection logic across
services and deployment entry points.

### Configuration Simplicity

Operators can provide a valid provider connection string without additional provider
mode flags in most flows.

### Stronger Failure Semantics

Explicit parse-time validation reduces silent misconfiguration and improves diagnostics.

## Alternatives Considered

### Explicit Provider Flag Everywhere

Rejected because it duplicates truth already present in connection strings and increases
configuration burden.

### Provider Auto-Detection by Trial Connection Attempts

Rejected because it introduces extra latency and ambiguous failure modes.

### Single-Provider Storage Architecture

Rejected because SkyCMS intentionally supports multi-cloud and varied hosting models.

## Consequences

### Positive Outcomes

- Consistent provider selection behavior
- Cleaner startup and storage composition
- Better operational diagnostics for malformed connection strings

### Constraints Introduced

- Detection relies on expected connection-string patterns
- New providers require parser updates and compatibility tests
- Provider-specific edge cases still require dedicated handling

## Evidence

- Storage provider detection and parsing implementation:
  - Cosmos.BlobService/ConnectionStringParser.cs
- Storage provider auto-detection documentation:
  - SkyCMS.Docs/for-developers/storage-provider-auto-detection.md
