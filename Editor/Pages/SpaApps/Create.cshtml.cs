// <copyright file="Create.cshtml.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Pages.SpaApps;

using BCrypt.Net;
using Cosmos.Cms.Common;
using Cosmos.Cms.Common.Models;
using Cosmos.Cms.Common.Utilities;
using Cosmos.Common.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

/// <summary>
/// Page model for creating SPA applications.
/// </summary>
[Authorize(Roles = "Administrators, Editors")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateModel"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    public CreateModel(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <summary>
    /// Gets or sets the input model.
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; }

    /// <summary>
    /// Gets or sets the article ID (after creation).
    /// </summary>
    public Guid ArticleId { get; set; }

    /// <summary>
    /// Gets or sets the deployment key (shown once after creation).
    /// </summary>
    public string DeploymentKey { get; set; }

    /// <summary>
    /// Gets or sets the webhook secret (shown once after creation).
    /// </summary>
    public string WebhookSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show secrets.
    /// </summary>
    public bool ShowingSecrets { get; set; }

    /// <summary>
    /// GET handler.
    /// </summary>
    public void OnGet()
    {
        ShowingSecrets = false;
    }

    /// <summary>
    /// POST handler - creates the SPA article.
    /// </summary>
    /// <returns>A task result.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Check for duplicate URL path
        var existingArticle = await dbContext.Pages
            .FirstOrDefaultAsync(p => p.UrlPath == Input.UrlPath);

        if (existingArticle != null)
        {
            ModelState.AddModelError("Input.UrlPath", "This URL path is already in use.");
            return Page();
        }

        // Generate secure secrets
        DeploymentKey = SecurePasswordGenerator.GeneratePassword(32, includeSpecialChars: true);
        WebhookSecret = SecurePasswordGenerator.GeneratePassword(32, includeSpecialChars: true);

        // Create SPA metadata
        var metadata = new SpaMetadata
        {
            DeploymentKeyHash = BCrypt.HashPassword(DeploymentKey),
            WebhookSecretHash = BCrypt.HashPassword(WebhookSecret),
            DeploymentCount = 0
        };

        // Create the article
        var article = new PublishedPage
        {
            Id = Guid.NewGuid(),
            ArticleNumber = await GetNextArticleNumberAsync(),
            Title = Input.Title,
            UrlPath = Input.UrlPath.TrimStart('/'),
            ArticleType = (int)ArticleType.SpaApp,
            Content = System.Text.Json.JsonSerializer.Serialize(metadata),
            Published = null, // Not published until first deployment
            StatusCode = 0,   // Active
            Updated = DateTimeOffset.UtcNow,
            VersionNumber = 1
        };

        dbContext.Pages.Add(article);
        await dbContext.SaveChangesAsync();

        // Show secrets page
        ArticleId = article.Id;
        ShowingSecrets = true;

        return Page();
    }

    /// <summary>
    /// Gets the next available article number.
    /// </summary>
    /// <returns>Next article number.</returns>
    private async Task<int> GetNextArticleNumberAsync()
    {
        var maxArticleNumber = await dbContext.Pages
            .MaxAsync(p => (int?)p.ArticleNumber) ?? 0;
        return maxArticleNumber + 1;
    }

    /// <summary>
    /// Input model for SPA creation.
    /// </summary>
    public class InputModel
    {
        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        [Required]
        [MaxLength(254)]
        [Display(Name = "Application Title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the URL path.
        /// </summary>
        [Required]
        [MaxLength(255)]
        [RegularExpression(@"^/[a-z0-9\-]+$", ErrorMessage = "URL must start with / and contain only lowercase letters, numbers, and hyphens")]
        [Display(Name = "URL Path")]
        public string UrlPath { get; set; }
    }
}