// <copyright file="KnowledgeContextProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Services.EditorContext;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Implementation of <see cref="IKnowledgeContextProvider"/> with hardcoded editorial and technical rules.
/// 
/// v1 implementation uses hardcoded preservation rules and constraints.
/// v2 will dynamically fetch documentation from docs.sky-cms.com and source code indices.
/// </summary>
public class KnowledgeContextProvider : IKnowledgeContextProvider
{
    /// <inheritdoc />
    public Task<KnowledgeContext> GetKnowledgeContextAsync(
        DocumentKind documentKind,
        EditorKind editorKind,
        CancellationToken cancellationToken = default)
    {
        return editorKind switch
        {
            EditorKind.Article => GetArticleKnowledgeAsync(cancellationToken),
            EditorKind.Layout => GetLayoutKnowledgeAsync(cancellationToken),
            EditorKind.Template => GetTemplateKnowledgeAsync(cancellationToken),
            _ => throw new NotSupportedException($"Knowledge context not supported for editor kind: {editorKind}"),
        };
    }

    /// <inheritdoc />
    public Task<KnowledgeContext> GetArticleKnowledgeAsync(CancellationToken cancellationToken = default)
    {
        var context = new KnowledgeContext
        {
            RelevantDocumentation = new List<DocumentationReference>
            {
                new()
                {
                    Title = "Creating Articles",
                    Url = "https://docs.sky-cms.com/for-editors/creating-articles",
                    Summary = "Step-by-step guide for creating general pages, blog posts, and blogs in SkyCMS, including initial publish states.",
                    RelatedTopics = new List<string> { "article-creation", "drafts", "publishing" },
                },
                new()
                {
                    Title = "Article Lifecycle Quick Reference",
                    Url = "https://docs.sky-cms.com/for-editors/article-lifecycle-reference",
                    Summary = "Overview of article states (draft, review, published, archived) and state transitions.",
                    RelatedTopics = new List<string> { "lifecycle", "versioning", "publishing" },
                },
                new()
                {
                    Title = "Visual Editor Technical Reference",
                    Url = "https://docs.sky-cms.com/for-editors/visual-editor-technical-reference",
                    Summary = "Implementation details of CKEditor 5 modes (title, simple, standard, advanced), toolbar configuration, and content constraints.",
                    RelatedTopics = new List<string> { "ckeditor", "editor-modes", "html" },
                },
                new()
                {
                    Title = "Visual Editor Toolbar Reference",
                    Url = "https://docs.sky-cms.com/for-editors/visual-editor-toolbar-reference",
                    Summary = "Detailed reference for all toolbar tools available in each editor mode.",
                    RelatedTopics = new List<string> { "ckeditor", "toolbar", "editing" },
                },
                new()
                {
                    Title = "Layouts and Templates",
                    Url = "https://docs.sky-cms.com/for-editors/layouts-and-templates",
                    Summary = "Guide to template-backed pages, editable regions, and how template changes affect articles.",
                    RelatedTopics = new List<string> { "templates", "regions", "content-wrapping" },
                },
                new()
                {
                    Title = "Publishing Modes and Workflows",
                    Url = "https://docs.sky-cms.com/for-editors/publishing-modes",
                    Summary = "Overview of publish now, scheduled publishing, and draft review workflows.",
                    RelatedTopics = new List<string> { "publishing", "scheduling", "workflows" },
                },
                new()
                {
                    Title = "Version History",
                    Url = "https://docs.sky-cms.com/for-editors/version-history",
                    Summary = "How to view, compare, and restore previous article versions.",
                    RelatedTopics = new List<string> { "versioning", "history", "restore" },
                },
                new()
                {
                    Title = "Code Editor Guide",
                    Url = "https://docs.sky-cms.com/for-editors/code-editor",
                    Summary = "Using Monaco code editor for direct HTML/CSS/JavaScript editing with syntax highlighting and validation.",
                    RelatedTopics = new List<string> { "code-editor", "monaco", "html-editing" },
                },
            },

            EditorialConventions = new EditorialConventions
            {
                TitleFormat = "Title Case, 50-70 characters recommended (max 255 characters)",
                ContentGuidelines = new List<string>
                {
                    "Use semantic HTML elements (h1, h2, p, ul, ol, blockquote, etc.)",
                    "Keep paragraphs concise and focused (2-4 sentences max)",
                    "Use descriptive link text (avoid 'click here', 'read more')",
                    "Maintain heading hierarchy: article title = h1, main sections = h2, subsections = h3",
                    "Use bulleted lists for unordered information; numbered lists for steps",
                    "Include an introductory paragraph that summarizes the article",
                    "Add a table of contents for articles longer than 1500 words",
                    "Use blockquotes for important callouts or testimonials",
                    "Include alt text for all images (descriptive, not generic)",
                    "Use tables for structured data; avoid for layout",
                    "Keep code samples short and focused; use syntax highlighting",
                    "Break content into logical sections with clear transitions",
                },
                SeoRules = new List<string>
                {
                    "Title should include primary keyword (preferably in first 50 characters)",
                    "First 160 characters of content become meta description if not set separately",
                    "Use h2 headers for major topics (helps with SEO and readability)",
                    "Internal links should use descriptive anchor text with target keywords",
                    "Keep URLs short, descriptive, lowercase with hyphens (not underscores or spaces)",
                    "Use consistent terminology throughout the article",
                    "Include related articles or 'See Also' sections for internal linking",
                    "Structure data with proper heading hierarchy for featured snippets",
                },
                TemplateRegionGuidelines = new List<string>
                {
                    "Only edit content within template-defined editable regions",
                    "If a section is read-only, the template does not define it as editable",
                    "Region identifiers and structure are controlled by the template designer",
                    "Do not attempt to add new regions; contact your site builder if needed",
                    "When templates are updated, region names or structure may change",
                    "Template changes may affect which fields are editable",
                    "Review template documentation for region constraints and requirements",
                },
                EditorModeGuidelines = new List<string>
                {
                    "Title mode: Use for headline-only regions (bold, italic only)",
                    "Simple mode: Basic formatting for summaries and metadata",
                    "Standard mode: Full feature set for main content (default)",
                    "Advanced mode: Full editing including tables, code blocks, media",
                    "Mode is typically set by the template or region configuration",
                    "Cannot change modes; work within the configured mode constraints",
                },
            },

            TechnicalConstraints = new TechnicalConstraints
            {
                HtmlConstraints = new List<string>
                {
                    "Allowed block elements: h1-h6, p, div, ul, ol, li, blockquote, pre, code, table, thead, tbody, tr, td, th, hr, section, article",
                    "Allowed inline elements: span, a, img, strong, em, code, br, mark, sup, sub",
                    "Forbidden: script, iframe, object, embed, form, input, button, style",
                    "No inline event handlers (onclick, onload, onhover, etc.)",
                    "No <script> tags; use HeaderJavaScript field for scripts",
                    "No embedded forms; use dedicated contact form workflows",
                    "No hardcoded domain names in href/src (use relative paths or CDN URLs)",
                    "Images must reference SkyCMS blob storage (relative paths starting with /media/)",
                    "No <style> tags; use class names from layout-provided stylesheets",
                    "Data attributes allowed (data-*) for JavaScript integration",
                    "id attributes must be unique within the article content",
                    "Reserved class names: page-content, article-content, editable-region (don't override)",
                },
                JsConstraints = new List<string>
                {
                    "HeaderJavaScript: Runs once in document <head> when article loads",
                    "FooterJavaScript: Runs once before </body> (use for DOM manipulation)",
                    "Must not pollute global window namespace; wrap in IIFE or use module pattern",
                    "Must complete before page paint; avoid long-running initialization",
                    "Available object: window.ccmsArticle = { ArticleNumber, Title, UrlPath, Status, Version, LastModified, TemplateId, Category }",
                    "Must not modify document.body structure or layout regions",
                    "Must not use document.write() (breaks async page loading)",
                    "Must not assume jQuery is loaded; use vanilla JS or check first",
                    "Performance: Keep scripts under 50KB; use async/defer for external scripts",
                    "Avoid synchronous XMLHttpRequest; prefer fetch with async/await",
                    "Must not use eval() or Function() constructor",
                    "DOM ready: Scripts run after DOMContentLoaded; DOM is available",
                },
                ContentSizeConstraints = new List<string>
                {
                    "Article content HTML should be kept under 500KB (uncompressed)",
                    "Individual images should be under 5MB (recommend 500KB-2MB for web)",
                    "Use compression and modern formats (WebP, JPEG with optimization)",
                    "Content truncation: Fields over 50KB are truncated with ellipsis indicator",
                    "URLs should be under 2048 characters (some browsers have limits)",
                    "Article title should be under 255 characters (database limit)",
                    "Version history: Old versions are retained indefinitely (archive carefully)",
                },
                CosmosDbConstraints = new List<string>
                {
                    "Queries must support cross-provider compatibility (SQL Server, MySQL, SQLite, Cosmos DB)",
                    "No cross-container joins in queries (Cosmos DB limitation)",
                    "Article partition key: ArticleNumber (used for efficient queries)",
                    "Avoid complex nested queries; use sequential queries with client-side correlation",
                    "Avoid inline casts in query predicates (e.g., (int)SomeEnum.Value); pre-compute locally",
                },
            },

            PreservationRules = new List<string>
            {
                "Preserve HTML structure unless the user explicitly requests restructuring",
                "Preserve template placeholders and comments (<!-- [CONTENT] -->, etc.)",
                "Do not remove or modify existing CSS class attributes (used by layout theme)",
                "Preserve existing event listeners in HeaderJavaScript unless requested for modification",
                "Do not remove access to window.ccmsArticle if referenced in scripts",
                "Preserve article metadata (Title, UrlPath) unless explicitly editing those fields",
                "Preserve data attributes (data-*) used for tracking or analytics",
                "Preserve semantic HTML structure for accessibility (proper heading hierarchy, etc.)",
                "Preserve existing version history and metadata",
                "Preserve any article-specific routing or URL patterns",
            },

            AntiPatterns = new List<string>
            {
                "Do not add oversized elements that exceed content region boundaries or cause layout shift",
                "Do not hardcode domain names; always use relative paths or SkyCMS CDN URLs",
                "Do not move HTML content to HeaderJavaScript; keep content in the Content field",
                "Do not suggest moving user-visible content to script tags",
                "Do not modify or move template region placeholders",
                "Do not add inline styles (style='...') where layout CSS classes can be used",
                "Do not ignore navigation breadcrumbs or layout structure elements",
                "Do not create content that breaks on mobile screens (lack of responsive layout)",
                "Do not use deeply nested divs for layout (max 5 levels in critical paths)",
                "Do not add multiple editors/editors from different systems to same field",
                "Do not create infinite loops in scripts or event handlers",
                "Do not assume external libraries (jQuery, React, etc.) are available",
                "Do not create auto-playing media without user consent",
                "Do not hardcode article-specific values that should come from metadata",
            },

            ApplicableDocVersion = "latest",
            ApplicableSectionKinds = new List<string> { "articles", "blog-posts", "pages", "general" },
        };

        return Task.FromResult(context);
    }

    /// <inheritdoc />
    public Task<KnowledgeContext> GetLayoutKnowledgeAsync(CancellationToken cancellationToken = default)
    {
        var context = new KnowledgeContext
        {
            RelevantDocumentation = new List<DocumentationReference>
            {
                new()
                {
                    Title = "Layout System Overview",
                    Url = "https://docs.sky-cms.com/for-site-builders/layouts",
                    Summary = "Complete guide to layout system, regions, placeholders, theming, and rendering pipeline.",
                    RelatedTopics = new List<string> { "layouts", "regions", "theming" },
                },
                new()
                {
                    Title = "Layouts and Templates (Editor Guide)",
                    Url = "https://docs.sky-cms.com/for-editors/layouts-and-templates",
                    Summary = "Editor perspective on layouts: template-backed pages, editable regions, and region updates.",
                    RelatedTopics = new List<string> { "templates", "editable-regions", "regions" },
                },
                new()
                {
                    Title = "Creating Editable Regions",
                    Url = "https://docs.sky-cms.com/for-developers/website-launch/CreatingEditableAreas",
                    Summary = "Developer guide to defining editable regions in layouts and templates.",
                    RelatedTopics = new List<string> { "editable-regions", "markup", "attributes" },
                },
                new()
                {
                    Title = "Visual Editor Technical Reference",
                    Url = "https://docs.sky-cms.com/for-editors/visual-editor-technical-reference",
                    Summary = "Technical details of CKEditor integration in layouts.",
                    RelatedTopics = new List<string> { "ckeditor", "regions", "editing" },
                },
            },

            EditorialConventions = new EditorialConventions
            {
                TitleFormat = "PascalCase layout names, 20-50 characters (human-readable identifiers)",
                ContentGuidelines = new List<string>
                {
                    "Name each region clearly and descriptively (e.g., 'Main Content', 'Left Sidebar', 'Header')",
                    "Include HTML comments explaining region purpose and intended content type",
                    "Document which article fields map to which regions",
                    "Document region constraints (required, optional, read-only, field-specific)",
                    "Keep region descriptions concise but specific about expected content",
                    "Provide examples of appropriate content for each region",
                    "Mark read-only regions clearly with comments",
                    "Group related regions logically in the layout",
                },
                RegionNamingConventions = new List<string>
                {
                    "Use PascalCase for internal region identifiers (matches editable-region attribute)",
                    "Use descriptive English names reflecting region position or purpose",
                    "Avoid generic names ('Box1', 'Area2'); use specific descriptors",
                    "Keep region names under 50 characters",
                    "Region names are case-sensitive in editable-region attributes",
                    "Document both internal ID and human-readable label",
                },
                LayoutStructureGuidelines = new List<string>
                {
                    "Design layouts for common viewport sizes (mobile, tablet, desktop)",
                    "Group content semantically (header, main, sidebar, footer)",
                    "Keep layout hierarchy clear and predictable",
                    "Document regions that appear conditionally or dynamically",
                    "Mark regions for specific templates or article types",
                    "Plan for content overflow and responsive text sizing",
                },
            },

            TechnicalConstraints = new TechnicalConstraints
            {
                HtmlConstraints = new List<string>
                {
                    "Use semantic HTML5 structure (header, nav, main, aside, footer, section, article)",
                    "Mark editable regions with: <div editable-region='RegionName'><!-- [RegionName] --></div>",
                    "Use data attributes for targeting and styling: data-layout-region='RegionName'",
                    "Keep structure simple and predictable; avoid deeply nested elements",
                    "Use aria-label for regions without visible labels (accessibility)",
                    "Do not use id selectors for layout structure (use classes instead)",
                    "Do not hardcode article-specific content in layout",
                    "Do not mix content from different regions in nested structures",
                    "Template: Use <!-- [REGIONNAME] --> comments to mark region boundaries",
                    "Document required markup wrappers for region integration",
                },
                CssConstraints = new List<string>
                {
                    "Use CSS custom properties (--layout-*, --color-*, etc.) for theming",
                    "Create layout-specific CSS in separate stylesheets",
                    "Document all custom CSS classes and their purposes",
                    "Use CSS Grid or Flexbox for layout (avoid floats for structure)",
                    "Ensure responsive design: mobile-first approach with media queries",
                    "Avoid !important unless absolutely necessary (document why)",
                    "Test layouts across browsers (Chrome, Firefox, Safari, Edge)",
                    "Use CSS containment (contain: layout) for performance",
                    "Avoid external stylesheet requests that block rendering",
                    "Document color palette, typography, spacing system",
                    "Use CSS classes for styling; avoid inline styles",
                },
                JsConstraints = new List<string>
                {
                    "Initialization scripts run once when layout is rendered",
                    "Provide window.ccmsLayout object with layout metadata (name, version, regions)",
                    "Scripts must be idempotent (safe to run multiple times)",
                    "Must not add article-specific logic or content to layout",
                    "Must not modify article content dynamically",
                    "Must not create global variables; use namespacing or modules",
                    "Document any event listeners or observers attached to layout regions",
                    "Clean up resources (remove listeners, clear timers) if layout is replaced",
                    "Avoid synchronous operations that block layout rendering",
                    "Performance: Keep initialization under 100ms",
                },
                ResponsiveDesignConstraints = new List<string>
                {
                    "Breakpoints: Mobile (320px), Tablet (768px), Desktop (1024px), Large (1440px)",
                    "Regions must reflow gracefully at each breakpoint",
                    "Test with actual mobile devices; emulation is not sufficient",
                    "Use touch-friendly spacing (min 44px for interactive elements)",
                    "Images must scale responsively (use max-width: 100%)",
                    "Test zoom behavior at 200% for accessibility",
                    "Document breakpoint-specific layout changes",
                },
            },

            PreservationRules = new List<string>
            {
                "Preserve existing region definitions and editable-region attributes",
                "Preserve region placeholder comments (<!-- [REGIONNAME] -->)",
                "Preserve stylesheet and script loading order",
                "Preserve layout initialization code and global layout objects",
                "Preserve responsive design breakpoints and media queries",
                "Preserve accessibility attributes (aria-*, role, etc.)",
                "Preserve region structure unless user explicitly requests restructuring",
                "Preserve documented CSS variables and theming system",
                "Preserve region naming conventions (case-sensitive)",
                "Preserve data attributes used for region targeting",
            },

            AntiPatterns = new List<string>
            {
                "Do not create layouts with more than 8 top-level regions (maintenance burden)",
                "Do not hardcode article-specific content or field values in layout",
                "Do not use absolute positioning for main content flow (breaks responsive design)",
                "Do not inline all CSS; use external stylesheets for better caching",
                "Do not load unoptimized images or fonts; use CDN delivery",
                "Do not create circular dependencies between layout regions",
                "Do not use tables for layout (use CSS Grid or Flexbox)",
                "Do not assume JavaScript is enabled without fallback content",
                "Do not use <meta> tags to redirect or modify page behavior without documentation",
                "Do not create regions that auto-populate with article content",
                "Do not use layout-level scripts to modify article content",
                "Do not mix responsive and fixed-width content",
                "Do not create layouts that only work on desktop",
            },

            ApplicableDocVersion = "latest",
            ApplicableSectionKinds = new List<string> { "layouts", "themes", "templates" },
        };

        return Task.FromResult(context);
    }

    /// <inheritdoc />
    public Task<KnowledgeContext> GetTemplateKnowledgeAsync(CancellationToken cancellationToken = default)
    {
        var context = new KnowledgeContext
        {
            RelevantDocumentation = new List<DocumentationReference>
            {
                new()
                {
                    Title = "Template System Overview",
                    Url = "https://docs.sky-cms.com/for-site-builders/templates",
                    Summary = "Comprehensive guide to SkyCMS template system, composition, data binding, and field mapping.",
                    RelatedTopics = new List<string> { "templates", "composition", "fields" },
                },
                new()
                {
                    Title = "Layouts and Templates (Editor Guide)",
                    Url = "https://docs.sky-cms.com/for-editors/layouts-and-templates",
                    Summary = "How templates affect article editing experience and content regions.",
                    RelatedTopics = new List<string> { "templates", "regions", "articles" },
                },
                new()
                {
                    Title = "Template Markup Reference",
                    Url = "https://docs.sky-cms.com/for-site-builders/template-markup",
                    Summary = "Complete reference for template placeholder syntax and field binding patterns.",
                    RelatedTopics = new List<string> { "markup", "syntax", "fields" },
                },
            },

            EditorialConventions = new EditorialConventions
            {
                TitleFormat = "PascalCase template names, 20-50 characters (descriptive, specific)",
                ContentGuidelines = new List<string>
                {
                    "Document all expected data fields clearly with types and constraints",
                    "Provide concrete examples of field values and rendering",
                    "Describe rendering order and field dependencies",
                    "Explain conditional rendering logic (if field exists, if value equals X, etc.)",
                    "Document which fields are required vs. optional",
                    "Document field length limits and validation rules",
                    "Explain how missing/empty fields are handled (defaults, fallbacks, omission)",
                    "Mark fields that come from layout vs. article vs. computed",
                    "Include examples of template usage with sample article data",
                },
                TemplateFieldDocumentation = new List<string>
                {
                    "Field Name: Exact name used in template placeholder",
                    "Type: String, Number, Date, Boolean, Object (array if list)",
                    "Source: Article, Layout, Computed (calculated from other fields)",
                    "Required: Yes/No (what happens if missing)",
                    "Length: Max characters (or -1 for unlimited)",
                    "Format: If applicable (date format, number format, etc.)",
                    "Example: Actual sample value",
                    "Used in regions: Which article regions use this field",
                    "Transformation: Any processing applied to field value",
                },
                TemplateCompositionRules = new List<string>
                {
                    "Keep templates focused on a single content type or layout pattern",
                    "Do not create templates that duplicate other templates (DRY principle)",
                    "Document inheritance and composition relationships",
                    "Avoid circular template dependencies",
                    "Keep template markup concise and maintainable",
                    "Separate presentation logic from business logic",
                    "Use consistent placeholder naming convention across all templates",
                    "Document versioning if templates evolve",
                },
            },

            TechnicalConstraints = new TechnicalConstraints
            {
                HtmlConstraints = new List<string>
                {
                    "Use template placeholder syntax: {{fieldName}} or <!-- [FIELDNAME] -->",
                    "Keep template markup simple and reusable across different articles",
                    "Do not embed article content directly; always use placeholders",
                    "Use semantic HTML for template structure",
                    "Do not hardcode article-specific values in template",
                    "Do not use placeholder names that conflict with reserved words",
                    "Document all custom HTML attributes used by template",
                    "Use data attributes for template-specific configuration: data-template-field='fieldName'",
                    "Keep template nesting depth reasonable (max 5 levels)",
                },
                FieldBindingConstraints = new List<string>
                {
                    "Field references must match exact field names (case-sensitive)",
                    "Use dot notation for nested objects: {{article.author.name}}",
                    "Use array indexing for lists: {{tags[0]}}",
                    "Conditional rendering: {{#if field}} content {{/if}}",
                    "Looping: {{#each items}} item {{/each}}",
                    "Fallback values: {{field || 'default value'}}",
                    "Formatting filters: {{field | uppercase}} (if supported)",
                    "Date formatting: Use ISO 8601 or documented format",
                    "URL encoding for field values in href/src attributes",
                    "Escape user-generated content to prevent XSS",
                },
                JsConstraints = new List<string>
                {
                    "Template initialization code runs when template is applied to article",
                    "Provide window.ccmsTemplate object with template metadata",
                    "Can access article data via window.ccmsArticle",
                    "Keep scripts stateless and idempotent",
                    "Must not assume execution context (inline vs. external script)",
                    "Document any side effects or external dependencies",
                    "Clean up temporary state if template is re-applied",
                    "Performance: Keep template initialization under 50ms",
                },
                CompositionConstraints = new List<string>
                {
                    "Do not compose templates in a way that creates circular references",
                    "Document template inheritance chain (Template A extends Template B)",
                    "Limit composition depth to 3 levels for maintainability",
                    "Clearly mark which parts are overrideable vs. fixed",
                    "Test all composition paths with different article types",
                    "Document required field contracts for composed templates",
                },
            },

            PreservationRules = new List<string>
            {
                "Preserve template placeholder names and structure",
                "Preserve field binding logic and conditional rendering",
                "Preserve template composition contract and inheritance",
                "Preserve rendering order and field dependencies",
                "Preserve initialization code and global template objects",
                "Preserve documented data transformations",
                "Preserve template metadata and configuration",
                "Preserve placeholder syntax ({{}} or <!-- --> style)",
                "Preserve field naming conventions (case-sensitive)",
            },

            AntiPatterns = new List<string>
            {
                "Do not create templates that are only slightly different from existing ones",
                "Do not hardcode article-specific values in templates",
                "Do not use templates for article-specific content (use article Content field instead)",
                "Do not create circular template dependencies",
                "Do not use template placeholders for CSS class names",
                "Do not mix different placeholder syntaxes in same template",
                "Do not create templates with more than 20 field dependencies",
                "Do not assume field values follow specific format without validation",
                "Do not create templates that fail silently when fields are missing",
                "Do not use global state in template initialization",
                "Do not create templates that modify other templates",
                "Do not hardcode CDN or external resource URLs in templates",
            },

            ApplicableDocVersion = "latest",
            ApplicableSectionKinds = new List<string> { "templates", "compositions" },
        };

        return Task.FromResult(context);
    }
}
