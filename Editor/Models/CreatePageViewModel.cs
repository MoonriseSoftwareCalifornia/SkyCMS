// <copyright file="CreatePageViewModel.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Models
{
    using Cosmos.Cms.Common;
    using Cosmos.Cms.Data;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Create page view model.
    /// </summary>
    public class CreatePageViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CreatePageViewModel"/> class.
        /// </summary>
        public CreatePageViewModel()
        {
            Templates = new List<SelectListItem>();
        }

        /// <summary>
        /// Gets or sets article Number.
        /// </summary>
        public int ArticleNumber { get; set; } = 0;

        /// <summary>
        /// Gets or sets page ID.
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets page title.
        /// </summary>
        [ArticleTitleValidation]
        [Display(Name = "Page Title")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Pages must have a title.")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets page template used.
        /// </summary>
        [Display(Name = "Page template (optional)")]
        public Guid? TemplateId { get; set; }

        /// <summary>
        /// Gets or sets template list.
        /// </summary>
        public List<SelectListItem> Templates { get; set; }

        /// <summary>
        /// Gets or sets the type of article.
        /// </summary>
        public ArticleType ArticleType { get; set; } = ArticleType.General;

        /// <summary>
        /// Gets or sets blog category.
        /// </summary>
        [MaxLength(64)]
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets introduction/summary for blog post.
        /// </summary>
        [MaxLength(512)]
        public string Introduction { get; set; }
    }
}
