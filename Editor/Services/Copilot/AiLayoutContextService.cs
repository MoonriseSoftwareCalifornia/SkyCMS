// <copyright file="AiLayoutContextService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Resolves layout/runtime context for AI prompts.
/// </summary>
public sealed class AiLayoutContextService : IAiLayoutContextService
{
    private const int MaxContextLength = 3200;

    private readonly IApplicationDbContext dbContext;
    private readonly ILogger<AiLayoutContextService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiLayoutContextService"/> class.
    /// </summary>
    /// <param name="dbContext">Application database context.</param>
    /// <param name="logger">Logger.</param>
    public AiLayoutContextService(IApplicationDbContext dbContext, ILogger<AiLayoutContextService> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AiLayoutContextResult> GetLayoutContextAsync(AiContextEnrichmentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var layout = await this.ResolveLayoutAsync(request, cancellationToken);
            if (layout == null)
            {
                return new AiLayoutContextResult();
            }

            var combinedLayoutText = $"{layout.Head}\n{layout.HtmlHeader}\n{layout.FooterHtmlContent}";
            var frameworks = DetectFrameworks(combinedLayoutText);
            var assets = ExtractAssets(combinedLayoutText);

            var sb = new StringBuilder();
            sb.AppendLine("Layout runtime context (active website shell):");
            sb.AppendLine($"- Layout name: {layout.LayoutName}");
            sb.AppendLine($"- Layout number: {layout.LayoutNumber}");

            if (!string.IsNullOrWhiteSpace(layout.BodyHtmlAttributes))
            {
                sb.AppendLine($"- Body attributes: {layout.BodyHtmlAttributes}");
            }

            if (frameworks.Count > 0)
            {
                sb.AppendLine($"- Detected frameworks/libraries: {string.Join(", ", frameworks)}");
            }

            if (assets.Count > 0)
            {
                sb.AppendLine($"- Referenced assets: {string.Join(", ", assets)}");
            }

            sb.AppendLine("- Guidance: Treat the following layout excerpts as the currently active site layout context. Use them when validating classes, structure, and compatibility.");

            AppendLayoutSection(sb, "Layout HEAD excerpt", layout.Head, 700);
            AppendLayoutSection(sb, "Layout HEADER excerpt", layout.HtmlHeader, 700);
            AppendLayoutSection(sb, "Layout FOOTER excerpt", layout.FooterHtmlContent, 700);

            var context = sb.ToString();
            if (context.Length > MaxContextLength)
            {
                context = context[..MaxContextLength];
            }

            return new AiLayoutContextResult
            {
                ContextText = context,
            };
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to resolve layout context for AI request.");
            return new AiLayoutContextResult();
        }
    }

    private static void AppendLayoutSection(StringBuilder sb, string heading, string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine($"{heading}:");
        sb.AppendLine(TrimForContext(text, maxLength));
    }

    private static string TrimForContext(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text ?? string.Empty;
        }

        return text[..maxLength];
    }

    private async Task<LayoutSnapshot?> ResolveLayoutAsync(AiContextEnrichmentRequest request, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(request.LayoutId, out var layoutId))
        {
            return await this.GetLayoutByIdAsync(layoutId, cancellationToken);
        }

        if (Guid.TryParse(request.TemplateId, out var templateId))
        {
            return await this.GetLayoutByTemplateIdAsync(templateId, cancellationToken);
        }

        if (int.TryParse(request.ArticleNumber, out var articleNumber))
        {
            var articleTemplateId = await this.dbContext.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .Select(a => a.TemplateId)
                .FirstOrDefaultAsync(cancellationToken);

            if (articleTemplateId.HasValue)
            {
                return await this.GetLayoutByTemplateIdAsync(articleTemplateId.Value, cancellationToken);
            }
        }

        return null;
    }

    private async Task<LayoutSnapshot?> GetLayoutByTemplateIdAsync(Guid templateId, CancellationToken cancellationToken)
    {
        var template = await this.dbContext.Templates
            .Where(t => t.Id == templateId)
            .Select(t => new
            {
                t.LayoutId,
                t.LayoutNumber,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (template == null)
        {
            return null;
        }

        if (template.LayoutId.HasValue)
        {
            var pinnedLayout = await this.GetLayoutByIdAsync(template.LayoutId.Value, cancellationToken);
            if (pinnedLayout != null)
            {
                return pinnedLayout;
            }
        }

        if (template.LayoutNumber <= 0)
        {
            return null;
        }

        return await this.dbContext.Layouts
            .Where(l => l.LayoutNumber == template.LayoutNumber)
            .OrderByDescending(l => l.Published)
            .ThenByDescending(l => l.Version)
            .Select(ToSnapshot())
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<LayoutSnapshot?> GetLayoutByIdAsync(Guid layoutId, CancellationToken cancellationToken)
    {
        return await this.dbContext.Layouts
            .Where(l => l.Id == layoutId)
            .Select(ToSnapshot())
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static System.Linq.Expressions.Expression<Func<Layout, LayoutSnapshot>> ToSnapshot()
    {
        return layout => new LayoutSnapshot
        {
            LayoutName = layout.LayoutName,
            LayoutNumber = layout.LayoutNumber,
            BodyHtmlAttributes = layout.BodyHtmlAttributes,
            Head = layout.Head,
            HtmlHeader = layout.HtmlHeader,
            FooterHtmlContent = layout.FooterHtmlContent,
        };
    }

    private static List<string> DetectFrameworks(string text)
    {
        var frameworks = new List<string>();

        AddFrameworkIfPresent(frameworks, text, "bootstrap", "Bootstrap");
        AddFrameworkIfPresent(frameworks, text, "tailwind", "Tailwind CSS");
        AddFrameworkIfPresent(frameworks, text, "jquery", "jQuery");
        AddFrameworkIfPresent(frameworks, text, "alpine", "Alpine.js");
        AddFrameworkIfPresent(frameworks, text, "react", "React");
        AddFrameworkIfPresent(frameworks, text, "vue", "Vue");

        return frameworks;
    }

    private static void AddFrameworkIfPresent(List<string> frameworks, string text, string marker, string displayName)
    {
        if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
        {
            frameworks.Add(displayName);
        }
    }

    private static List<string> ExtractAssets(string text)
    {
        var matches = Regex.Matches(text, "(?:src|href)\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
        return matches
            .Select(m => m.Groups[1].Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
    }

    private sealed class LayoutSnapshot
    {
        public string LayoutName { get; set; } = string.Empty;

        public int LayoutNumber { get; set; }

        public string BodyHtmlAttributes { get; set; } = string.Empty;

        public string Head { get; set; } = string.Empty;

        public string HtmlHeader { get; set; } = string.Empty;

        public string FooterHtmlContent { get; set; } = string.Empty;
    }
}
