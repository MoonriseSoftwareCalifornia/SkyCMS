// <copyright file="IEditorContextBuilder.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Services.EditorContext;

/// <summary>
/// Service interface for building typed, layered context payloads for AI editor assistance.
/// Implements ADR 0044: AI Editor Context Schema with Layered Delivery and Entity Awareness.
/// 
/// <remarks>
/// **Design Principle: Dual-Client Reusability**
/// 
/// This service is designed to be reused by both:
/// - Web Editor (Monaco/CKEditor in Sky.Editor)
/// - VS Code Extension (VsCodeController API endpoints)
/// 
/// This ensures:
/// - Single source of truth for context assembly
/// - Consistent AI behavior across both editing surfaces
/// - DRY principle: no duplicate context-building logic between clients
/// - Easy maintenance: update context schema once, both clients automatically benefit
/// 
/// All builders must remain client-agnostic and should not depend on HttpContext,
/// controller routing, or web-specific infrastructure.
/// </remarks>
/// </summary>
public interface IEditorContextBuilder
{
    /// <summary>
    /// Builds editor context for an article.
    /// </summary>
    /// <param name="articleNumber">The article number to build context for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation, returning article entity context.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the article does not exist.</exception>
    Task<ArticleEntityContext> BuildArticleContextAsync(
        int articleNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds editor context for a layout.
    /// </summary>
    /// <param name="layoutId">The layout ID to build context for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation, returning layout entity context.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the layout does not exist.</exception>
    Task<LayoutEntityContext> BuildLayoutContextAsync(
        string layoutId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds editor context for a template.
    /// </summary>
    /// <param name="templateId">The template ID to build context for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation, returning template entity context.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the template does not exist.</exception>
    Task<TemplateEntityContext> BuildTemplateContextAsync(
        string templateId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds rendering context that traces article → layout → template flow.
    /// </summary>
    /// <param name="articleNumber">The article number to build rendering context for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation, returning rendering context.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the article, layout, or template does not exist.</exception>
    Task<RenderingContext> BuildRenderingContextAsync(
        int articleNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds base editor context with surface and editor metadata.
    /// </summary>
    /// <param name="surface">The editor surface (monaco, ckeditor, help).</param>
    /// <param name="kind">The editor kind (article, layout, template).</param>
    /// <param name="documentKind">The document kind (article, html, javascript, etc.).</param>
    /// <param name="currentField">The currently focused field name.</param>
    /// <param name="language">The language of the current field.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation, returning editor context base.</returns>
    Task<EditorContextBase> BuildEditorContextBaseAsync(
        EditorSurface surface,
        EditorKind kind,
        DocumentKind documentKind,
        string currentField,
        LanguageKind language,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds knowledge context with documentation, constraints, and editorial rules.
    /// </summary>
    /// <param name="documentKind">The document kind to get knowledge for.</param>
    /// <param name="editorKind">The editor kind to get knowledge for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation, returning knowledge context.</returns>
    Task<KnowledgeContext> BuildKnowledgeContextAsync(
        DocumentKind documentKind,
        EditorKind editorKind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds validation context from any errors or warnings in the document.
    /// </summary>
    /// <param name="articleNumber">The article number (if applicable).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation, returning validation context.</returns>
    Task<ValidationContext> BuildValidationContextAsync(
        int? articleNumber = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Editor surface identifier.
/// </summary>
public enum EditorSurface
{
    /// <summary>
    /// Monaco editor for code editing.
    /// </summary>
    Monaco,

    /// <summary>
    /// CKEditor for rich text editing.
    /// </summary>
    CKEditor,

    /// <summary>
    /// Help chat surface.
    /// </summary>
    Help,
}

/// <summary>
/// Editor kind identifier.
/// </summary>
public enum EditorKind
{
    /// <summary>
    /// Article editing.
    /// </summary>
    Article,

    /// <summary>
    /// Layout editing.
    /// </summary>
    Layout,

    /// <summary>
    /// Template editing.
    /// </summary>
    Template,

    /// <summary>
    /// Blog editing.
    /// </summary>
    Blog,

    /// <summary>
    /// Settings editing.
    /// </summary>
    Settings,
}

/// <summary>
/// Document kind identifier.
/// </summary>
public enum DocumentKind
{
    /// <summary>
    /// Article document.
    /// </summary>
    Article,

    /// <summary>
    /// Layout document.
    /// </summary>
    Layout,

    /// <summary>
    /// Template document.
    /// </summary>
    Template,

    /// <summary>
    /// HTML document.
    /// </summary>
    Html,

    /// <summary>
    /// CSS document.
    /// </summary>
    Css,

    /// <summary>
    /// JavaScript document.
    /// </summary>
    JavaScript,

    /// <summary>
    /// TypeScript document.
    /// </summary>
    TypeScript,

    /// <summary>
    /// Razor document.
    /// </summary>
    Razor,

    /// <summary>
    /// JSON document.
    /// </summary>
    Json,

    /// <summary>
    /// XML document.
    /// </summary>
    Xml,

    /// <summary>
    /// Markdown document.
    /// </summary>
    Markdown,

    /// <summary>
    /// Unknown document kind.
    /// </summary>
    Unknown,
}

/// <summary>
/// Language kind for code editing.
/// </summary>
public enum LanguageKind
{
    /// <summary>
    /// HTML language.
    /// </summary>
    Html,

    /// <summary>
    /// JavaScript language.
    /// </summary>
    JavaScript,

    /// <summary>
    /// CSS language.
    /// </summary>
    Css,

    /// <summary>
    /// TypeScript language.
    /// </summary>
    TypeScript,

    /// <summary>
    /// Razor language.
    /// </summary>
    Razor,

    /// <summary>
    /// JSON language.
    /// </summary>
    Json,

    /// <summary>
    /// XML language.
    /// </summary>
    Xml,

    /// <summary>
    /// Markdown language.
    /// </summary>
    Markdown,
}

/// <summary>
/// Base editor context sent with every AI request.
/// Implements the always-send layer from ADR 0044.
/// </summary>
public class EditorContextBase
{
    /// <summary>
    /// Gets or sets the editor surface.
    /// </summary>
    public required EditorSurface EditorSurface { get; set; }

    /// <summary>
    /// Gets or sets the editor kind.
    /// </summary>
    public required EditorKind EditorKind { get; set; }

    /// <summary>
    /// Gets or sets the document kind.
    /// </summary>
    public required DocumentKind DocumentKind { get; set; }

    /// <summary>
    /// Gets or sets the article number (if editing an article).
    /// </summary>
    public int? ArticleNumber { get; set; }

    /// <summary>
    /// Gets or sets the layout ID (if editing a layout).
    /// </summary>
    public string? LayoutId { get; set; }

    /// <summary>
    /// Gets or sets the template ID (if editing a template).
    /// </summary>
    public string? TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the currently focused field name.
    /// </summary>
    public required string CurrentField { get; set; }

    /// <summary>
    /// Gets or sets the current field value (truncated if > 50KB).
    /// </summary>
    public string? CurrentFieldValue { get; set; }

    /// <summary>
    /// Gets or sets the current selection in the editor.
    /// </summary>
    public SelectionRange? CurrentSelection { get; set; }

    /// <summary>
    /// Gets or sets the document title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the document URL path.
    /// </summary>
    public string? UrlPath { get; set; }

    /// <summary>
    /// Gets or sets the document status.
    /// </summary>
    public DocumentStatus? DocumentStatus { get; set; }

    /// <summary>
    /// Gets or sets the document version number.
    /// </summary>
    public int? Version { get; set; }

    /// <summary>
    /// Gets or sets the language of the current field.
    /// </summary>
    public required LanguageKind Language { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the document is read-only.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Gets or sets the selected AI model (if user-configurable).
    /// </summary>
    public string? SelectedModel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether AI is enabled for this surface.
    /// </summary>
    public bool AiEnabled { get; set; }
}

/// <summary>
/// Selection range in an editor.
/// </summary>
public class SelectionRange
{
    /// <summary>
    /// Gets or sets the start position of the selection.
    /// </summary>
    public required int Start { get; set; }

    /// <summary>
    /// Gets or sets the end position of the selection.
    /// </summary>
    public required int End { get; set; }

    /// <summary>
    /// Gets or sets the selected text.
    /// </summary>
    public required string Text { get; set; }
}

/// <summary>
/// Document status enumeration.
/// </summary>
public enum DocumentStatus
{
    /// <summary>
    /// Draft status.
    /// </summary>
    Draft,

    /// <summary>
    /// Published status.
    /// </summary>
    Published,

    /// <summary>
    /// Archived status.
    /// </summary>
    Archived,
}

/// <summary>
/// Article entity context.
/// </summary>
public class ArticleEntityContext
{
    /// <summary>
    /// Gets or sets the context type.
    /// </summary>
    public string Type => "article";

    /// <summary>
    /// Gets or sets the article number.
    /// </summary>
    public required int ArticleNumber { get; set; }

    /// <summary>
    /// Gets or sets the article title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the article URL path.
    /// </summary>
    public required string UrlPath { get; set; }

    /// <summary>
    /// Gets or sets the main article content (HTML or markup).
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Gets or sets the header JavaScript.
    /// </summary>
    public string? HeaderJavaScript { get; set; }

    /// <summary>
    /// Gets or sets the footer JavaScript.
    /// </summary>
    public string? FooterJavaScript { get; set; }

    /// <summary>
    /// Gets or sets the banner image metadata.
    /// </summary>
    public BannerImage? BannerImage { get; set; }

    /// <summary>
    /// Gets or sets the template ID.
    /// </summary>
    public string? TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the layout ID.
    /// </summary>
    public required string LayoutId { get; set; }

    /// <summary>
    /// Gets or sets the article category.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the article type.
    /// </summary>
    public string? ArticleType { get; set; }

    /// <summary>
    /// Gets or sets the section kind.
    /// </summary>
    public string? SectionKind { get; set; }

    /// <summary>
    /// Gets or sets the publish date.
    /// </summary>
    public string? PublishedDate { get; set; }

    /// <summary>
    /// Gets or sets the author.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Gets or sets the article status.
    /// </summary>
    public required string Status { get; set; } // 'draft', 'published', 'archived'

    /// <summary>
    /// Gets or sets the article version number.
    /// </summary>
    public required int Version { get; set; }

    /// <summary>
    /// Gets or sets the last modified timestamp.
    /// </summary>
    public required string LastModified { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the article has unsaved changes.
    /// </summary>
    public bool IsDirty { get; set; }

    /// <summary>
    /// Gets or sets the list of unsaved changes.
    /// </summary>
    public List<UnsavedChange>? UnsavedChanges { get; set; }
}

/// <summary>
/// Banner image metadata.
/// </summary>
public class BannerImage
{
    /// <summary>
    /// Gets or sets the image URL.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Gets or sets the alt text.
    /// </summary>
    public string? AltText { get; set; }

    /// <summary>
    /// Gets or sets the image title.
    /// </summary>
    public string? Title { get; set; }
}

/// <summary>
/// Unsaved change information.
/// </summary>
public class UnsavedChange
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public required string Field { get; set; }

    /// <summary>
    /// Gets or sets the previous value.
    /// </summary>
    public required string PreviousValue { get; set; }

    /// <summary>
    /// Gets or sets the current value.
    /// </summary>
    public required string CurrentValue { get; set; }
}

/// <summary>
/// Layout entity context.
/// </summary>
public class LayoutEntityContext
{
    /// <summary>
    /// Gets or sets the context type.
    /// </summary>
    public string Type => "layout";

    /// <summary>
    /// Gets or sets the layout ID.
    /// </summary>
    public required string LayoutId { get; set; }

    /// <summary>
    /// Gets or sets the layout name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the layout description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the layout regions.
    /// </summary>
    public required List<LayoutRegion> Regions { get; set; }

    /// <summary>
    /// Gets or sets the body insertion information.
    /// </summary>
    public BodyInsertion? BodyInsertion { get; set; }

    /// <summary>
    /// Gets or sets the stylesheets.
    /// </summary>
    public List<LayoutStylesheet>? Stylesheets { get; set; }

    /// <summary>
    /// Gets or sets the scripts.
    /// </summary>
    public List<LayoutScript>? Scripts { get; set; }

    /// <summary>
    /// Gets or sets the layout markup (full or summarized).
    /// </summary>
    public required string LayoutMarkup { get; set; }

    /// <summary>
    /// Gets or sets the count of articles using this layout.
    /// </summary>
    public int ArticlesUsingThisLayout { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the default layout.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the layout version.
    /// </summary>
    public required int Version { get; set; }
}

/// <summary>
/// Layout region definition.
/// </summary>
public class LayoutRegion
{
    /// <summary>
    /// Gets or sets the region name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the placeholder.
    /// </summary>
    public required string Placeholder { get; set; }

    /// <summary>
    /// Gets or sets the region description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this region is required.
    /// </summary>
    public bool Required { get; set; }
}

/// <summary>
/// Body insertion information.
/// </summary>
public class BodyInsertion
{
    /// <summary>
    /// Gets or sets the insertion type.
    /// </summary>
    public required string Type { get; set; } // 'direct', 'region', 'template-placeholder'

    /// <summary>
    /// Gets or sets the insertion location.
    /// </summary>
    public required string Location { get; set; }
}

/// <summary>
/// Layout stylesheet information.
/// </summary>
public class LayoutStylesheet
{
    /// <summary>
    /// Gets or sets the stylesheet URL.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the stylesheet is inline.
    /// </summary>
    public bool Inline { get; set; }
}

/// <summary>
/// Layout script information.
/// </summary>
public class LayoutScript
{
    /// <summary>
    /// Gets or sets the script URL.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the script is inline.
    /// </summary>
    public bool Inline { get; set; }

    /// <summary>
    /// Gets or sets the script location.
    /// </summary>
    public required string Location { get; set; } // 'head', 'body-end'
}

/// <summary>
/// Template entity context.
/// </summary>
public class TemplateEntityContext
{
    /// <summary>
    /// Gets or sets the context type.
    /// </summary>
    public string Type => "template";

    /// <summary>
    /// Gets or sets the template ID.
    /// </summary>
    public required string TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the template name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the template description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the expected fields.
    /// </summary>
    public required List<TemplateField> ExpectedFields { get; set; }

    /// <summary>
    /// Gets or sets the composition type.
    /// </summary>
    public required string CompositionType { get; set; } // 'wrapper', 'partial', 'composite', 'custom'

    /// <summary>
    /// Gets or sets the rendering rules.
    /// </summary>
    public RenderingRules? RenderingRules { get; set; }

    /// <summary>
    /// Gets or sets the template markup.
    /// </summary>
    public string? TemplateMarkup { get; set; }

    /// <summary>
    /// Gets or sets the template reference.
    /// </summary>
    public string? TemplateReference { get; set; }

    /// <summary>
    /// Gets or sets the count of articles using this template.
    /// </summary>
    public int ArticlesUsingThisTemplate { get; set; }

    /// <summary>
    /// Gets or sets the template version.
    /// </summary>
    public required int Version { get; set; }
}

/// <summary>
/// Template field definition.
/// </summary>
public class TemplateField
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public required string FieldName { get; set; }

    /// <summary>
    /// Gets or sets the data type.
    /// </summary>
    public required string DataType { get; set; } // 'string', 'number', 'boolean', 'url', 'date', 'html'

    /// <summary>
    /// Gets or sets a value indicating whether this field is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the maximum length.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Gets or sets the field description.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Rendering rules for templates.
/// </summary>
public class RenderingRules
{
    /// <summary>
    /// Gets or sets a value indicating whether to preserve article content.
    /// </summary>
    public bool PreserveArticleContent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to allow custom scripts.
    /// </summary>
    public bool AllowCustomScripts { get; set; }

    /// <summary>
    /// Gets or sets the allowed HTML elements.
    /// </summary>
    public List<string>? AllowedHtmlElements { get; set; }

    /// <summary>
    /// Gets or sets the disallowed HTML elements.
    /// </summary>
    public List<string>? DisallowedHtmlElements { get; set; }
}

/// <summary>
/// Rendering context that traces article → layout → template flow.
/// </summary>
public class RenderingContext
{
    /// <summary>
    /// Gets or sets the rendering flow description.
    /// </summary>
    public required string RenderingFlow { get; set; }

    /// <summary>
    /// Gets or sets the content insertion information.
    /// </summary>
    public required ContentInsertion ContentInsertion { get; set; }

    /// <summary>
    /// Gets or sets the placeholder mappings.
    /// </summary>
    public required List<PlaceholderMapping> Placeholders { get; set; }

    /// <summary>
    /// Gets or sets the script loading order.
    /// </summary>
    public List<ScriptLoadingOrder>? ScriptLoadingOrder { get; set; }

    /// <summary>
    /// Gets or sets important rendering notes.
    /// </summary>
    public List<string>? Notes { get; set; }
}

/// <summary>
/// Content insertion information.
/// </summary>
public class ContentInsertion
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public required string Field { get; set; }

    /// <summary>
    /// Gets or sets the insertion destination.
    /// </summary>
    public required string Destination { get; set; }

    /// <summary>
    /// Gets or sets any transformation applied.
    /// </summary>
    public string? Transformation { get; set; }
}

/// <summary>
/// Placeholder mapping in layout/template.
/// </summary>
public class PlaceholderMapping
{
    /// <summary>
    /// Gets or sets the placeholder string.
    /// </summary>
    public required string Placeholder { get; set; }

    /// <summary>
    /// Gets or sets the source entity.
    /// </summary>
    public required string Source { get; set; }

    /// <summary>
    /// Gets or sets the source field.
    /// </summary>
    public required string Field { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this placeholder is required.
    /// </summary>
    public bool Required { get; set; }
}

/// <summary>
/// Script loading order information.
/// </summary>
public class ScriptLoadingOrder
{
    /// <summary>
    /// Gets or sets the script source.
    /// </summary>
    public required string Source { get; set; }

    /// <summary>
    /// Gets or sets the script location.
    /// </summary>
    public required string Location { get; set; } // 'head', 'body-end', 'inline'

    /// <summary>
    /// Gets or sets the script timing.
    /// </summary>
    public required string Timing { get; set; } // 'immediate', 'deferred', 'async'
}

/// <summary>
/// Knowledge context with documentation and constraints.
/// </summary>
public class KnowledgeContext
{
    /// <summary>
    /// Gets or sets the relevant documentation.
    /// </summary>
    public List<DocumentationReference>? RelevantDocumentation { get; set; }

    /// <summary>
    /// Gets or sets the editorial conventions.
    /// </summary>
    public EditorialConventions? EditorialConventions { get; set; }

    /// <summary>
    /// Gets or sets the technical constraints.
    /// </summary>
    public TechnicalConstraints? TechnicalConstraints { get; set; }

    /// <summary>
    /// Gets or sets the preservation rules.
    /// </summary>
    public required List<string> PreservationRules { get; set; }

    /// <summary>
    /// Gets or sets the anti-patterns.
    /// </summary>
    public required List<string> AntiPatterns { get; set; }

    /// <summary>
    /// Gets or sets the applicable documentation version.
    /// </summary>
    public string? ApplicableDocVersion { get; set; }

    /// <summary>
    /// Gets or sets the applicable section kinds.
    /// </summary>
    public List<string>? ApplicableSectionKinds { get; set; }
}

/// <summary>
/// Documentation reference.
/// </summary>
public class DocumentationReference
{
    /// <summary>
    /// Gets or sets the documentation title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the documentation URL.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Gets or sets the summary.
    /// </summary>
    public required string Summary { get; set; }

    /// <summary>
    /// Gets or sets the related topics.
    /// </summary>
    public List<string>? RelatedTopics { get; set; }
}

/// <summary>
/// Editorial conventions.
/// </summary>
public class EditorialConventions
{
    /// <summary>
    /// Gets or sets the title format guidelines.
    /// </summary>
    public string? TitleFormat { get; set; }

    /// <summary>
    /// Gets or sets the content guidelines.
    /// </summary>
    public List<string>? ContentGuidelines { get; set; }

    /// <summary>
    /// Gets or sets the SEO rules.
    /// </summary>
    public List<string>? SeoRules { get; set; }

    /// <summary>
    /// Gets or sets the template region guidelines (for article contexts).
    /// </summary>
    public List<string>? TemplateRegionGuidelines { get; set; }

    /// <summary>
    /// Gets or sets the editor mode guidelines (e.g., title, simple, standard, advanced modes).
    /// </summary>
    public List<string>? EditorModeGuidelines { get; set; }

    /// <summary>
    /// Gets or sets the region naming conventions (for layout contexts).
    /// </summary>
    public List<string>? RegionNamingConventions { get; set; }

    /// <summary>
    /// Gets or sets the layout structure guidelines (for layout contexts).
    /// </summary>
    public List<string>? LayoutStructureGuidelines { get; set; }

    /// <summary>
    /// Gets or sets the template field documentation guidelines (for template contexts).
    /// </summary>
    public List<string>? TemplateFieldDocumentation { get; set; }

    /// <summary>
    /// Gets or sets the template composition rules (for template contexts).
    /// </summary>
    public List<string>? TemplateCompositionRules { get; set; }
}

/// <summary>
/// Technical constraints.
/// </summary>
public class TechnicalConstraints
{
    /// <summary>
    /// Gets or sets the HTML constraints.
    /// </summary>
    public List<string>? HtmlConstraints { get; set; }

    /// <summary>
    /// Gets or sets the CSS constraints.
    /// </summary>
    public List<string>? CssConstraints { get; set; }

    /// <summary>
    /// Gets or sets the JavaScript constraints.
    /// </summary>
    public List<string>? JsConstraints { get; set; }

    /// <summary>
    /// Gets or sets the content size constraints (for article contexts).
    /// </summary>
    public List<string>? ContentSizeConstraints { get; set; }

    /// <summary>
    /// Gets or sets the Cosmos DB constraints (cross-provider database compatibility).
    /// </summary>
    public List<string>? CosmosDbConstraints { get; set; }

    /// <summary>
    /// Gets or sets the responsive design constraints (for layout contexts).
    /// </summary>
    public List<string>? ResponsiveDesignConstraints { get; set; }

    /// <summary>
    /// Gets or sets the composition constraints (for template contexts).
    /// </summary>
    public List<string>? CompositionConstraints { get; set; }

    /// <summary>
    /// Gets or sets the field binding constraints (for template contexts).
    /// </summary>
    public List<string>? FieldBindingConstraints { get; set; }
}

/// <summary>
/// Validation context with errors and warnings.
/// </summary>
public class ValidationContext
{
    /// <summary>
    /// Gets or sets a value indicating whether there are errors.
    /// </summary>
    public bool HasErrors { get; set; }

    /// <summary>
    /// Gets or sets the list of errors.
    /// </summary>
    public List<ValidationError>? Errors { get; set; }

    /// <summary>
    /// Gets or sets the list of warnings.
    /// </summary>
    public List<ValidationWarning>? Warnings { get; set; }

    /// <summary>
    /// Gets or sets the validation status per field.
    /// </summary>
    public required List<FieldValidationStatus> ValidationStatus { get; set; }
}

/// <summary>
/// Validation error.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public required string Field { get; set; }

    /// <summary>
    /// Gets or sets the line number.
    /// </summary>
    public int? Line { get; set; }

    /// <summary>
    /// Gets or sets the column number.
    /// </summary>
    public int? Column { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the rule ID.
    /// </summary>
    public string? RuleId { get; set; }
}

/// <summary>
/// Validation warning.
/// </summary>
public class ValidationWarning
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public required string Field { get; set; }

    /// <summary>
    /// Gets or sets the line number.
    /// </summary>
    public int? Line { get; set; }

    /// <summary>
    /// Gets or sets the warning message.
    /// </summary>
    public required string Message { get; set; }
}

/// <summary>
/// Field validation status.
/// </summary>
public class FieldValidationStatus
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public required string Field { get; set; }

    /// <summary>
    /// Gets or sets the validation status.
    /// </summary>
    public required string Status { get; set; } // 'valid', 'invalid', 'warning', 'unknown'
}
