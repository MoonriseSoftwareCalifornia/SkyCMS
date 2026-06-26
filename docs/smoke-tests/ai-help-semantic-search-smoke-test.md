# AI Help Semantic Search Smoke Test Script

Purpose
- Quick manual verification of help-query ranking behavior, embedding toggle behavior, and fallback safety.

Scope
- Help query pipeline using docs/source/FAQ context.
- Embedding reranker on/off behavior through Admin settings.

Location of settings
- Admin page: SkyCMS Settings -> AI Provider.
- View implementation: Editor/Views/SkyCmsSettings/AiProvider.cshtml.

---

## Preconditions

1. Build succeeds:

```powershell
dotnet build SkyCMS.sln
```

2. Focused tests succeed:

```powershell
dotnet test Tests/Sky.Tests.csproj --filter "FullyQualifiedName~AiDocumentationIndexServiceTests|FullyQualifiedName~AiFaqIndexServiceTests|FullyQualifiedName~AiSourceCodeIndexServiceTests|FullyQualifiedName~Cosmos___SettingsControllerTests|FullyQualifiedName~CopilotProxyOptionsServiceTests"
```

3. App is running and AI Provider settings are reachable.

---

## Test Matrix

Run the same 3 queries in each configuration below.

- Scenario A: Embedding toggle OFF
- Scenario B: Embedding toggle ON, OpenAI provider configured
- Scenario C: Embedding toggle ON, non-supported provider (fallback expected)

Expected high-level outcomes:
- A: normal grounded answers
- B: normal grounded answers, possible ranking/citation improvements
- C: normal grounded answers (no failures), fallback ranking active

---

## 3-Query Script

### Query 1 (Docs-heavy)

Prompt:
- How do I configure multi-tenant cookie isolation and domain handling in SkyCMS?

Expected signals:
- Mentions tenant/domain handling and cookie isolation guidance.
- Includes at least one documentation citation URL from docs.sky-cms.com.
- No errors/timeouts.

### Query 2 (Source/API-heavy)

Prompt:
- Where is tenant resolution established in code, and which middleware/service should I inspect first?

Expected signals:
- References tenant resolution components (for example DomainMiddleware and dynamic configuration provider patterns).
- Includes at least one code/source-style citation (GitHub-style source context if surfaced by help response).
- No errors/timeouts.

### Query 3 (Troubleshooting/FAQ-heavy)

Prompt:
- Why might AI help answers become stale, and how do I check index freshness?

Expected signals:
- Mentions docs/source index freshness and staleness monitoring.
- References the index health endpoint concept (/api/ai-proxy/index-health).
- Includes at least one docs citation.

---

## Pass/Fail Criteria

Pass
- All 3 queries return coherent, non-error responses in all 3 scenarios (A/B/C).
- Responses include at least one grounded source/citation for each query.
- Scenario C does not degrade into failures when embedding path is unavailable.

Fail
- Any query returns a server error, empty response, or ungrounded generic output repeatedly.
- Settings toggle causes request failures.
- Scenario C fails instead of gracefully falling back.

---

## Optional API Spot Checks

1. Health endpoint:

```http
GET /api/ai-proxy/index-health
```

Expected: JSON payload with docs/source health metadata.

2. Help query endpoint:

```http
POST /api/ai-help/query
```

Expected: normal response with grounded context behavior.

---

## Notes for QA Log

Capture these per run:
- Scenario (A/B/C)
- Query number (1/2/3)
- Response success/failure
- Presence of citations (yes/no)
- Any ranking quality notes (brief)

This provides a small, repeatable record for regression checks.
