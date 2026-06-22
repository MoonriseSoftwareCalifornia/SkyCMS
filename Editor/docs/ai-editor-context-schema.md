# SkyCMS AI Editor Context Schema

## Overview

This document provides the complete technical specification for context payloads sent to AI models during editor interactions in SkyCMS. This is the implementation reference for ADR 0044.

## Table of Contents

1. [Core Context Interfaces](#core-context-interfaces)
2. [Layered Payload Structure](#layered-payload-structure)
3. [Concrete Examples](#concrete-examples)
4. [Implementation Patterns](#implementation-patterns)

---

## Core Context Interfaces

### EditorContextBase (Always Sent)

```typescript
interface EditorContextBase {
  // Editor surface and identity
  editorSurface: 'monaco' | 'ckeditor' | 'help';
  editorKind: 'article' | 'layout' | 'template' | 'blog' | 'settings';
  documentKind: 'article' | 'layout' | 'template' | 'html' | 'css' | 'javascript' | 'unknown';
  
  // Entity identifiers
  articleNumber?: number;
  layoutId?: string;
  templateId?: string;
  
  // Current editing focus
  currentField: string; // e.g., 'Content', 'HeaderJavaScript', 'CustomCss'
  currentFieldValue?: string; // Current text in focused field (truncated if > 50KB)
  currentSelection?: {
    start: number;
    end: number;
    text: string;
  };
  
  // Document metadata
  title?: string;
  urlPath?: string;
  documentStatus?: 'draft' | 'published' | 'archived';
  version?: number;
  
  // Language and mode
  language: 'html' | 'javascript' | 'css' | 'typescript' | 'razor' | 'json' | 'xml' | 'markdown';
  readOnly: boolean;
  
  // Model selection and policy
  selectedModel?: string;
  aiEnabled: boolean;
}
```

### ArticleEntityContext

```typescript
interface ArticleEntityContext {
  type: 'article';
  
  // Basic identity
  articleNumber: number;
  title: string;
  urlPath: string;
  
  // Content fields
  content: string; // Main article content (HTML or markup)
  headerJavaScript?: string;
  footerJavaScript?: string;
  bannerImage?: {
    url: string;
    altText?: string;
    title?: string;
  };
  
  // Template and layout
  templateId?: string;
  layoutId: string;
  
  // Metadata
  category?: string;
  articleType?: 'article' | 'blog' | 'page' | 'blog-entry';
  sectionKind?: string;
  publishedDate?: string;
  author?: string;
  
  // Status and version
  status: 'draft' | 'published' | 'archived';
  version: number;
  lastModified: string;
  
  // Editing context
  isDirty: boolean;
  unsavedChanges?: Array<{
    field: string;
    previousValue: string;
    currentValue: string;
  }>;
}
```

### LayoutEntityContext

```typescript
interface LayoutEntityContext {
  type: 'layout';
  
  // Basic identity
  layoutId: string;
  name: string;
  description?: string;
  
  // Structure
  regions: Array<{
    name: string;
    placeholder: string; // e.g., '<!-- [CONTENT] -->', '{{content}}'
    description?: string;
    required: boolean;
  }>;
  
  // Available insertions
  bodyInsertion?: {
    type: 'direct' | 'region' | 'template-placeholder';
    location: string;
  };
  
  // Scripts and styles
  stylesheets?: Array<{
    url: string;
    inline: boolean;
  }>;
  scripts?: Array<{
    url: string;
    inline: boolean;
    location: 'head' | 'body-end';
  }>;
  
  // Current markup
  layoutMarkup: string; // Full or summarized layout HTML
  
  // Usage info
  articlesUsingThisLayout: number;
  isDefault: boolean;
  version: number;
}
```

### TemplateEntityContext

```typescript
interface TemplateEntityContext {
  type: 'template';
  
  // Basic identity
  templateId: string;
  name: string;
  description?: string;
  
  // Contract
  expectedFields: Array<{
    fieldName: string;
    dataType: 'string' | 'number' | 'boolean' | 'url' | 'date' | 'html';
    required: boolean;
    maxLength?: number;
    description?: string;
  }>;
  
  // Composition model
  compositionType: 'wrapper' | 'partial' | 'composite' | 'custom';
  
  // Rendering rules
  renderingRules?: {
    preserveArticleContent: boolean;
    allowCustomScripts: boolean;
    allowedHtmlElements?: string[]; // e.g., ['div', 'p', 'span', 'a']
    disallowedHtmlElements?: string[];
  };
  
  // Template markup or reference
  templateMarkup?: string;
  templateReference?: string; // e.g., 'path/to/template.html'
  
  // Usage info
  articlesUsingThisTemplate: number;
  version: number;
}
```

### RenderingContext

```typescript
interface RenderingContext {
  // The relationship flow
  renderingFlow: string; // e.g., "Article content → Template wraps → Layout positions"
  
  // Key insertion points
  contentInsertion: {
    field: string; // e.g., 'Content'
    destination: string; // e.g., 'main article region in layout'
    transformation?: string; // e.g., 'wrapped in template'
  };
  
  // Placeholder mapping
  placeholders: Array<{
    placeholder: string; // e.g., '<!-- [TITLE] -->'
    source: string; // e.g., 'Article.Title'
    field: string; // e.g., 'title'
    required: boolean;
  }>;
  
  // Script loading and timing
  scriptLoadingOrder: Array<{
    source: string;
    location: 'head' | 'body-end' | 'inline';
    timing: 'immediate' | 'deferred' | 'async';
  }>;
  
  // Important rendering notes
  notes: string[];
}
```

### KnowledgeContext

```typescript
interface KnowledgeContext {
  // Documentation excerpt
  relevantDocumentation: Array<{
    title: string;
    url: string;
    summary: string; // Short summary, not full article
    relatedTopics?: string[];
  }>;
  
  // Editorial conventions
  editorialConventions?: {
    titleFormat?: string; // e.g., "Title Case, 50-70 characters"
    contentGuidelines?: string[];
    seoRules?: string[];
  };
  
  // Technical constraints
  technicalConstraints?: {
    htmlConstraints?: string[]; // e.g., "No <script> tags outside HeaderJavaScript"
    cssConstraints?: string[]; // e.g., "Use layout-provided CSS variables"
    jsConstraints?: string[]; // e.g., "Must not pollute global namespace"
  };
  
  // Safety and preservation rules
  preservationRules: string[];
  
  // Do-not-do guidance
  antiPatterns: string[];
  
  // Version and scope
  applicableDocVersion?: string;
  applicableToSectionKinds?: string[]; // e.g., ['articles', 'blog-posts']
}
```

### EditingIntentContext

```typescript
interface EditingIntentContext {
  intent: 
    | 'explain'
    | 'fix-syntax'
    | 'improve-selection'
    | 'generate-section'
    | 'convert-selection'
    | 'optimize'
    | 'validate'
    | 'refactor'
    | 'custom';
  
  userQuery?: string;
  emphasize: Array<'accuracy' | 'brevity' | 'completeness' | 'safety' | 'style'>;
  avoid: Array<'breaking-changes' | 'markup-removal' | 'simplification' | 'assumptions'>;
  expectedFormat?: 'code-block' | 'explanation' | 'diff' | 'suggestion' | 'error-list';
}
```

### ValidationContext

```typescript
interface ValidationContext {
  hasErrors: boolean;
  
  errors?: Array<{
    field: string;
    line?: number;
    column?: number;
    message: string;
    ruleId?: string;
  }>;
  
  warnings?: Array<{
    field: string;
    line?: number;
    message: string;
  }>;
  
  validationStatus: {
    field: string;
    status: 'valid' | 'invalid' | 'warning' | 'unknown';
  }[];
}
```

### RecentChangesContext

```typescript
interface RecentChangesContext {
  recentEdits: Array<{
    timestamp: string;
    field: string;
    changeType: 'addition' | 'modification' | 'deletion';
    beforeSnippet?: string;
    afterSnippet?: string;
    userMessage?: string;
  }>;
  
  summary: string; // e.g., "User added two paragraphs to Content and updated HeaderJavaScript"
}
```

---

## Layered Payload Structure

### StartupPayload

Sent when editor initializes or chat is opened.

```typescript
interface StartupPayload {
  editorContext: EditorContextBase;
  entityContext: ArticleEntityContext | LayoutEntityContext | TemplateEntityContext;
  renderingContext?: RenderingContext;
  knowledgeContext?: KnowledgeContext;
  validationContext?: ValidationContext;
}
```

### ActionPayload

Sent when user submits chat or requests AI assistance.

```typescript
interface ActionPayload {
  editorContext: EditorContextBase;
  editingIntent: EditingIntentContext;
  entityContext: ArticleEntityContext | LayoutEntityContext | TemplateEntityContext;
  
  renderingContext?: RenderingContext;
  knowledgeContext?: KnowledgeContext;
  validationContext?: ValidationContext;
  recentChanges?: RecentChangesContext;
  
  userMessage: string;
  userSelection?: {
    start: number;
    end: number;
    text: string;
  };
}
```

---

## Concrete Examples

### Example 1: Article Content Editing (Startup)

```json
{
  "editorContext": {
    "editorSurface": "monaco",
    "editorKind": "article",
    "documentKind": "article",
    "articleNumber": 42,
    "currentField": "Content",
    "currentFieldValue": "<h1>Welcome</h1>\n<p>This is the main content...</p>",
    "title": "Getting Started with SkyCMS",
    "urlPath": "getting-started",
    "language": "html",
    "readOnly": false,
    "aiEnabled": true,
    "selectedModel": "gpt-4-turbo"
  },
  "entityContext": {
    "type": "article",
    "articleNumber": 42,
    "title": "Getting Started with SkyCMS",
    "urlPath": "getting-started",
    "content": "<h1>Welcome</h1>\n<p>This is the main content...</p>",
    "headerJavaScript": "",
    "footerJavaScript": "",
    "templateId": null,
    "layoutId": "default-article",
    "category": "documentation",
    "articleType": "article",
    "status": "published",
    "version": 3,
    "lastModified": "2026-06-22T14:30:00Z",
    "isDirty": false
  },
  "renderingContext": {
    "renderingFlow": "Article content → Layout positions in main region",
    "contentInsertion": {
      "field": "Content",
      "destination": "main article region in layout",
      "transformation": null
    },
    "placeholders": [
      {
        "placeholder": "<!-- [TITLE] -->",
        "source": "Article.Title",
        "field": "title",
        "required": true
      },
      {
        "placeholder": "<!-- [CONTENT] -->",
        "source": "Article.Content",
        "field": "content",
        "required": true
      }
    ]
  },
  "knowledgeContext": {
    "relevantDocumentation": [
      {
        "title": "Article Content Guidelines",
        "url": "https://docs.sky-cms.com/articles/content-guidelines",
        "summary": "Best practices for writing article content in SkyCMS, including SEO rules and HTML element support."
      }
    ],
    "editorialConventions": {
      "titleFormat": "Title Case, 50-70 characters recommended",
      "contentGuidelines": [
        "Use semantic HTML (h1, p, ul, ol, etc.)",
        "Keep paragraphs concise",
        "Use descriptive link text"
      ],
      "seoRules": [
        "Title should include primary keyword",
        "First paragraph should summarize the content",
        "Use heading hierarchy consistently"
      ]
    },
    "technicalConstraints": {
      "htmlConstraints": [
        "Allowed tags: h1-h6, p, div, span, a, img, ul, ol, li, table, thead, tbody, tr, td, th",
        "No <script> tags in content; use HeaderJavaScript field instead",
        "No inline event handlers (onclick, etc.)"
      ]
    },
    "preservationRules": [
      "Preserve existing HTML structure unless restructuring is the user's intent",
      "Keep SkyCMS placeholder comments unchanged",
      "Do not remove existing class attributes used by layout"
    ],
    "antiPatterns": [
      "Do not suggest breaking the layout by adding elements that exceed region boundaries",
      "Do not introduce hardcoded paths; suggest relative or SkyCMS-provided paths",
      "Do not suggest moving content to script tags; suggest HeaderJavaScript field instead"
    ]
  }
}
```

### Example 2: HeaderJavaScript Field (Action Request - Fix Syntax)

```json
{
  "editorContext": {
    "editorSurface": "monaco",
    "editorKind": "article",
    "documentKind": "article",
    "articleNumber": 42,
    "currentField": "HeaderJavaScript",
    "currentFieldValue": "console.log('hello'\nvar x = 5;",
    "currentSelection": {
      "start": 0,
      "end": 18,
      "text": "console.log('hello'"
    },
    "language": "javascript",
    "readOnly": false,
    "aiEnabled": true
  },
  "editingIntent": {
    "intent": "fix-syntax",
    "emphasize": ["accuracy", "safety"],
    "avoid": ["breaking-changes", "assumptions"],
    "expectedFormat": "code-block"
  },
  "entityContext": {
    "type": "article",
    "articleNumber": 42,
    "title": "Getting Started with SkyCMS",
    "currentField": "HeaderJavaScript",
    "headerJavaScript": "console.log('hello'\nvar x = 5;",
    "status": "draft",
    "isDirty": true,
    "unsavedChanges": [
      {
        "field": "HeaderJavaScript",
        "previousValue": "// Initial header script",
        "currentValue": "console.log('hello'\nvar x = 5;"
      }
    ]
  },
  "knowledgeContext": {
    "technicalConstraints": {
      "jsConstraints": [
        "Script runs in the document <head>",
        "Must not pollute global window namespace; use IIFE or module pattern",
        "Must complete before page paint; avoid blocking operations",
        "Can access window.ccmsArticle object for article metadata"
      ]
    },
    "preservationRules": [
      "Preserve any existing event listeners or initialization code",
      "Do not remove access to window.ccmsArticle if it's referenced"
    ],
    "antiPatterns": [
      "Do not suggest moving HTML into HeaderJavaScript; keep HTML in Content field",
      "Do not suggest modifying document.body or other layout regions",
      "Do not use document.write(); it will break in async contexts"
    ]
  },
  "validationContext": {
    "hasErrors": true,
    "errors": [
      {
        "field": "HeaderJavaScript",
        "line": 1,
        "column": 18,
        "message": "Unexpected newline; missing closing parenthesis",
        "ruleId": "syntax-error"
      }
    ]
  },
  "userMessage": "Fix the syntax error in my header script.",
  "userSelection": {
    "start": 0,
    "end": 18,
    "text": "console.log('hello'"
  }
}
```

---

## Implementation Patterns

### Context Builder Service

```csharp
// Example C# service for building context payloads

public interface IEditorContextBuilder
{
    Task<ArticleEntityContext> BuildArticleContextAsync(int articleNumber);
    Task<LayoutEntityContext> BuildLayoutContextAsync(string layoutId);
    Task<TemplateEntityContext> BuildTemplateContextAsync(string templateId);
    Task<RenderingContext> BuildRenderingContextAsync(int articleNumber);
    Task<KnowledgeContext> BuildKnowledgeContextAsync(string documentKind);
}

public class EditorContextBuilder : IEditorContextBuilder
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDocumentationService _docsService;
    private readonly IValidationService _validationService;
    
    public async Task<ArticleEntityContext> BuildArticleContextAsync(int articleNumber)
    {
        var article = await _dbContext.Articles
            .FirstOrDefaultAsync(a => a.ArticleNumber == articleNumber);
            
        if (article == null)
            throw new KeyNotFoundException($"Article {articleNumber} not found");
            
        return new ArticleEntityContext
        {
            Type = "article",
            ArticleNumber = article.ArticleNumber,
            Title = article.Title,
            UrlPath = article.UrlPath,
            Content = TruncateIfNeeded(article.Content, 50_000),
            HeaderJavaScript = article.HeaderJavaScript,
            FooterJavaScript = article.FooterJavaScript,
            TemplateId = article.TemplateId,
            LayoutId = article.LayoutId,
            Status = article.Status.ToString().ToLowerInvariant(),
            Version = article.Version,
            LastModified = article.LastModified.ToString("O"),
            IsDirty = false // Set based on form state
        };
    }
    
    private string TruncateIfNeeded(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
            
        return text.Substring(0, maxLength) + "\n\n... (truncated)";
    }
}
```

### Token Budget Management

```csharp
// Manage token usage within budget

public class TokenBudgetManager
{
    private const int MAX_TOKENS_PER_PAYLOAD = 4000;
    
    public StartupPayload TruncateForTokenBudget(StartupPayload payload)
    {
        var estimatedTokens = EstimateTokens(payload);
        
        if (estimatedTokens <= MAX_TOKENS_PER_PAYLOAD)
            return payload;
            
        // Remove optional contexts to reduce tokens
        if (payload.RecentChanges != null)
            payload.RecentChanges = null;
            
        if (payload.RenderingContext != null && payload.RenderingContext.Notes.Count > 3)
            payload.RenderingContext.Notes = payload.RenderingContext.Notes.Take(3).ToList();
            
        if (payload.KnowledgeContext?.RelevantDocumentation.Count > 2)
            payload.KnowledgeContext.RelevantDocumentation = 
                payload.KnowledgeContext.RelevantDocumentation.Take(2).ToList();
                
        return payload;
    }
    
    private int EstimateTokens(object payload)
    {
        // Rough estimation: ~1 token per 4 characters
        var json = JsonSerializer.Serialize(payload);
        return json.Length / 4;
    }
}
```

### Validation Before Sending

```csharp
// Validate payload structure before sending to AI

public class ContextPayloadValidator
{
    public ValidationResult Validate(StartupPayload payload)
    {
        var errors = new List<string>();
        
        if (payload.EditorContext == null)
            errors.Add("EditorContext is required");
            
        if (payload.EntityContext == null)
            errors.Add("EntityContext is required");
            
        if (payload.EditorContext?.CurrentFieldValue?.Length > 50_000)
            errors.Add("CurrentFieldValue exceeds 50KB limit");
            
        if (payload.EntityContext is ArticleEntityContext article)
        {
            if (string.IsNullOrWhiteSpace(article.Title))
                errors.Add("Article title is required");
                
            if (article.ArticleNumber <= 0)
                errors.Add("Article number must be positive");
        }
        
        return errors.Any()
            ? ValidationResult.Invalid(errors)
            : ValidationResult.Valid();
    }
}
```

---

## Best Practices

1. **Token Management**: Truncate field values > 50KB and provide summaries
2. **Caching**: Cache entity context during a session to reduce redundant API calls
3. **Privacy**: Exclude API keys, internal fields, and sensitive data
4. **Validation**: Validate payload structure before sending to AI
5. **Monitoring**: Log context sizes to track token efficiency
6. **Extensibility**: Reserve space for new context types (componentContext, globalSettings, etc.)

---

## References

- ADR 0044: AI Editor Context Schema with Layered Delivery and Entity Awareness
- ADR 0045: AI Help Knowledge System with Documentation and Source Indexing
