# ADR 0045: AI Help Knowledge System with Documentation and Source Indexing

## Status

Accepted

## Context

SkyCMS provides an AI Help chat surface (separate from the editor context in ADR 0044) for users seeking general guidance on SkyCMS concepts, troubleshooting, best practices, and API usage. Unlike editor context (which is entity-focused and request-time), help queries are open-ended, knowledge-seeking questions.

The help AI requires access to:
1. **Documentation website** (https://docs.sky-cms.com) with a pre-built search index
2. **Source code repository** (https://github.com/CWALabs/SkyCMS) for code examples, signatures, and architectural patterns
3. **FAQ and troubleshooting** content extracted from docs

Previously, help queries had no structured knowledge context, leading to generic or incomplete answers. A multi-source indexing strategy is needed to ground help responses in authoritative documentation and working code examples.

## Design Goals

This decision aims to:

1. Build and maintain a queryable knowledge index from both documentation and source code
2. Enable semantic search across all three sources (docs, source code, FAQ) for help queries
3. Return ranked, truncated knowledge results that balance completeness with token efficiency
4. Keep knowledge indices fresh via automated crawling and syncing
5. Provide source attribution (URLs, GitHub permalinks, version info) for all knowledge returned
6. Support extensibility for adding new knowledge sources (videos, community examples, etc.)
7. Leverage the existing mermaid-based search index from docs.sky-cms.com

## Non-Goals

This decision does not attempt to:

- Replace human documentation; AI helps users find and understand existing docs
- Guarantee answer accuracy; knowledge quality depends on source quality
- Support real-time code analysis or live debugging (source indexing is static)
- Solve cross-version knowledge problems (version-specific indexing is out of scope for v1)
- Create a new search UI; help chat uses existing AI Help chat interface

## Decision

SkyCMS implements a three-source knowledge system for help queries:

### Knowledge Sources

1. **Documentation Index** (Primary)
   - Source: https://docs.sky-cms.com/search/search_index.json
   - Updated: nightly or on docs-site deployment
   - Content: all pages, searchable via mermaid/mkdocs indexing
   - Attributes: title, URL, section headings, body text, code blocks, last-modified

2. **Source Code Index** (Secondary)
   - Source: GitHub repository (https://github.com/CWALabs/SkyCMS)
   - Updated: weekly or on-demand
   - Content: C# class/method signatures, XML doc comments, key architectural code
   - Attributes: namespace path, GitHub URL with line number, method signature, docstring

3. **FAQ and Troubleshooting Index** (Tertiary)
   - Source: extracted from docs via tagging or manual curation
   - Updated: as docs are updated
   - Content: Q&A pairs, error messages, symptoms, solutions
   - Attributes: question, answer, tags (symptom, topic, difficulty)

### Query-Time Knowledge Assembly

When a user submits a help query:

1. **Parse query intent** → categorize as 'how-to', 'troubleshoot', 'concept', 'api-reference', or 'example'
2. **Semantic search documentation** (using docs index) → top 3-5 matches with relevance scores
3. **Semantic search source code** (if intent is API or architecture) → top 2-3 examples
4. **Search FAQ index** (if query resembles known problem) → top 1-2 matches
5. **Assemble HelpQueryContext** with all results, truncated to fit token budget
6. **Send to AI** with structured context + user query

### Knowledge Index Maintenance

**Documentation Index:**
- Fetch search_index.json nightly from docs.sky-cms.com
- Parse mermaid-formatted index
- Store in local database or search service (SQLite FTS, Elasticsearch, or simple JSON cache)
- Track Last-Modified timestamp to detect stale data

**Source Code Index:**
- Clone or fetch latest from GitHub weekly (or on-demand via webhook)
- Walk project tree and parse:
  - C# class/interface definitions
  - Method signatures with parameters and return types
  - XML documentation comments
  - Key architectural classes and patterns
- Generate GitHub URLs with line numbers
- Store in queryable index

**FAQ Index:**
- Detect FAQ sections in docs (e.g., pages tagged with "faq", sections with "## Frequently Asked Questions")
- Extract Q&A pairs and symptoms
- Cross-link to full docs pages
- Update when docs index updates

### Context Payload for Help Queries

```typescript
interface HelpQueryContext {
  // User query and detected intent
  query: string;
  queryIntent: 'how-to' | 'troubleshoot' | 'concept' | 'api-reference' | 'example';
  
  // Matched documentation from docs.sky-cms.com
  relevantDocs: Array<{
    title: string;
    url: string;
    excerpt: string; // First 500 chars or relevant section
    difficulty: 'beginner' | 'intermediate' | 'advanced';
    topics: string[];
    codeExample?: string;
    lastUpdated: string;
    searchRelevanceScore: number; // 0.0 to 1.0
  }>;
  
  // Matched source code from GitHub
  relevantCode: Array<{
    filePath: string;
    className?: string;
    methodName?: string;
    signature: string;
    docComment?: string;
    snippet: string; // 5-10 lines of context
    githubUrl: string; // Link to repo at specific line
    isExample: boolean; // True if from /samples or /examples
  }>;
  
  // FAQ matches if query resembles known problem
  faqMatches?: Array<{
    question: string;
    answer: string;
    sourceUrl: string;
    relevanceScore: number;
  }>;
  
  // Related knowledge paths
  relatedTopics: string[];
  prerequisiteKnowledge?: Array<{
    topic: string;
    docUrl: string;
  }>;
  
  // Metadata
  applicableVersions?: string[];
  applicableToModes?: ('single-tenant' | 'multi-tenant' | 'all')[];
  suggestedNextSteps?: string[];
}
```

### Semantic Search Strategy

Use embedding-based similarity search (rather than keyword-only):

- Embed documentation titles and excerpts using a pre-trained model (e.g., sentence-transformers)
- Embed source code docstrings and method names
- At query time, embed the user query and find nearest neighbors in both indices
- Combine results with weighted scoring (e.g., docs score × 1.0, code examples × 0.8, FAQ × 0.9)
- Return top K results per source, truncated for tokens

Alternatively, use keyword search as a fallback if embeddings are unavailable.

## Detailed Rationale

### Why Multi-Source?

- **Docs alone** are authoritative but may not cover every use case; users want working code examples
- **Source code alone** requires developers to read signatures and comments; docs provide narrative and context
- **FAQ** catches common problems and anti-patterns; standalone doc searches might miss relevant Q&A pairs

Combining all three sources gives richer context and better answers.

### Why Index Rather Than Query Real-Time?

- Real-time GitHub API calls are slow and rate-limited
- Pre-built mermaid search index is already maintained by docs team; no redundant work
- Local indexing enables fast semantic search at query time
- Periodic updates (nightly docs, weekly code) are acceptable for help queries

### Why Leverage Existing search_index.json?

The docs website already maintains a search index (https://docs.sky-cms.com/search/search_index.json). Reusing it:
- Avoids duplicate crawling/indexing effort
- Ensures docs index stays in sync with actual site content
- Respects the docs team's existing maintenance workflow
- Requires only fetching and parsing existing JSON, not building new indexer

### Why Semantic Search?

Keyword-only search struggles with synonyms and paraphrasing. If a user asks "How do I prevent race conditions?" but docs say "concurrency control," keyword search might miss it. Semantic embeddings bridge this gap and improve relevance.

### How Does This Relate to ADR 0044 (Editor Context)?

- **ADR 0044** (editor): knowledge context is *entity-specific* and *request-time assembled*
  - Which docs are relevant to editing this article's content field?
  - What constraints apply to this layout's CSS?
  - Selected knowledge is curated for the current editing context
  
- **ADR 0045** (help): knowledge context is *general* and *query-driven*
  - Answer a user's open-ended question about SkyCMS
  - Search across all possible knowledge sources
  - Let the AI synthesize from multiple sources

**Synergy**: Both can reuse the same search_index.json and source code index. The difference is *when* and *how* they query:
- Editor context selects docs relevant to the current entity
- Help context searches broadly for what the user asks about

## Alternatives Considered

### Dump All Docs and Source Code Into Every Prompt

Rejected because:
- Massive token overhead
- Makes it hard for AI to find relevant information in noise
- Impossible to keep up-to-date

### Query Real-Time APIs (GitHub, Docs Search API)

Rejected because:
- Rate limiting and latency issues
- Docs already provide search_index.json; no need to re-crawl
- Real-time API calls are slow for synchronous help queries

### Keyword Search Only (No Embeddings)

Rejected because:
- Synonyms and paraphrasing are missed
- Relevance ranking is crude
- Users expect natural language understanding

### Single Knowledge Source (Docs Only)

Rejected because:
- Source code examples are invaluable for API and architecture questions
- FAQ helps catch common mistakes
- Richer context = better answers

## Consequences

### Positive Outcomes

- Help queries are grounded in authoritative documentation and working code examples
- Knowledge is kept fresh via automated indexing pipelines
- Search results include source attribution (docs URLs, GitHub permalinks)
- Users can follow up by reading full docs or exploring code on GitHub
- Reuse of existing mermaid search index reduces redundant work
- Semantic search improves relevance and handles paraphrasing
- Extensible design supports adding more knowledge sources later

### Constraints Introduced

- Must maintain indexing pipeline (docs fetching, source code parsing, FAQ extraction)
- Search embeddings require compute resources; fallback to keyword search if unavailable
- Indices can become stale if update pipelines fail; monitoring and alerting needed
- Contributors must ensure docs and code comments stay accurate (AI is only as good as sources)
- Help queries are bounded by what's in the indices; real-time questions may get incomplete answers
- GitHub API rate limits apply if fetching source code via API

## Implementation Guidance

### Phase 1: Build Documentation Index

1. **Fetch search_index.json** nightly from https://docs.sky-cms.com/search/search_index.json
2. **Parse mermaid format** (typically JSON with title, location, text fields)
3. **Store in SQLite with FTS** or similar for keyword search
4. **Generate embeddings** for title and text (optional but recommended)
5. **Track update time** to detect stale data

### Phase 2: Build Source Code Index

1. **Clone GitHub repo** or use GitHub API to walk tree
2. **Parse C# files** for:
   - Namespace and class definitions
   - Public method signatures
   - XML documentation comments
3. **Generate GitHub URLs** with line numbers
4. **Store metadata** (file path, class, method, signature, doc comment)
5. **Sync weekly** or on-demand

### Phase 3: Build FAQ Index

1. **Detect FAQ pages** in docs (e.g., pages with "faq" tag or "FAQ" in title)
2. **Extract Q&A pairs** using heading hierarchy (e.g., "## Q: How do I..." / "## A: ...")
3. **Tag by symptom/topic** for better search
4. **Cross-link to full docs pages**
5. **Update when docs index updates**

### Phase 4: Implement Query Service

1. **Accept user query** from help chat
2. **Embed query** using same model as documentation
3. **Search all three indices** (docs, code, FAQ)
4. **Rank and truncate** results to fit token budget
5. **Assemble HelpQueryContext** payload
6. **Return to help chat handler**

### Phase 5: Integrate with Help Chat

- Modify `/Editor/AiHelp` or help chat controller
- On user query, call knowledge search service
- Append HelpQueryContext to system prompt or context
- Send to AI with user message

### Monitoring and Maintenance

- Log index fetch and parse errors; alert if index is stale (> 24hrs old)
- Track search performance (latency, embedding time)
- Periodically verify docs URLs are still valid (some links may rot)
- Monitor source code index for parse failures when repo structure changes

## Evidence

- Existing mermaid-based search index:
  - https://docs.sky-cms.com/search/search_index.json
- Source code repository:
  - https://github.com/CWALabs/SkyCMS
- Help chat interface:
  - Editor/wwwroot/js/editors/ai-help-chat.js
  - Editor/Controllers/AiHelpController.cs (planned)
- Related ADR:
  - ADR 0044 (AI Editor Context Schema) — shows how to structure knowledge context

## Related Documentation

For the complete technical specification including:
- Search index JSON schema
- Embedding model selection
- Query ranking algorithm
- Fallback strategy when indices are unavailable

See: `/Editor/docs/ai-help-knowledge-architecture.md` (to be created)

## Implementation Roadmap

Planned work to realize this ADR:

1. **Build docs indexer** that fetches and parses search_index.json nightly
2. **Build source code indexer** that clones/walks GitHub repo weekly
3. **Implement semantic search** using embeddings or keyword fallback
4. **Create FAQ extractor** that identifies Q&A patterns in docs
5. **Implement HelpQueryContext assembly** service
6. **Integrate with help chat** controller and AI proxy
7. **Add monitoring/alerting** for index staleness and search failures
8. **Document knowledge schema** for future knowledge source integrations

## Integration with ADR 0044

This ADR complements ADR 0044 (AI Editor Context Schema):

- **ADR 0044** defines context for entity-focused editing (what knowledge to send when editing an article)
- **ADR 0045** defines context for knowledge-seeking help (what knowledge to send when user asks a question)

Both use similar knowledge payload shapes but with different assembly strategies:
- Editor context curates knowledge for the current entity
- Help context searches broadly and ranks by relevance

The shared search_index.json and source code index can be reused by both systems, reducing duplication and maintenance burden.
