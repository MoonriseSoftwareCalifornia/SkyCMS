# Track: Narrow Class Visibility Across Solution

## Summary
Reduce unnecessary `public` exposure in class libraries by narrowing types and members to the minimum required visibility (`internal`, `private protected`, `private`) without behavior changes.

## Why
- Reduce accidental coupling between projects.
- Shrink long-term API surface and refactoring risk.
- Make intended extension points explicit.

## Scope
- [ ] `Cosmos.MicrosoftGraph` (first candidate)
- [ ] `Cosmos.BlobService`
- [ ] `Cosmos.EmailServices`
- [ ] `AspNetCore.Identity.FlexDb`
- [ ] `Sky.Cms.Api.Shared`
- [ ] `Cosmos.ConnectionStrings` (later; high coupling)
- [ ] `Common/Cosmos.Common` (later; highest coupling)

## Plan
1. Work in a dedicated branch per library.
2. Keep PRs visibility-only (no behavior refactors mixed in).
3. Narrow one small batch at a time.
4. Build and run tests after each batch.

## Safety Checks Per PR
- [ ] `dotnet build SkyCMS.sln`
- [ ] Relevant tests pass (`dotnet test` target projects)
- [ ] DI registrations resolve at runtime
- [ ] Reflection/serialization paths validated where applicable

## Candidate First Batch (`Cosmos.MicrosoftGraph`)
- Proposed to narrow to `internal`:
  - `IMsGraphService`
  - `Settings`
  - `GroupAuthorizationRequirement`
  - `HandlerUsingAzureGroups`
- Keep public initially:
  - `MsGraphService`
  - `MsGraphClaimsTransformation`

## Notes
- Prefer `InternalsVisibleTo` for tests/sibling access rather than keeping broad `public` visibility.
- Re-evaluate public API exposure at the end of each library pass.
