// <copyright file="CreateHomePageCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Editor.Features.Articles.CreateHomePage
{
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to reassign which article is the home page (root).
    /// </summary>
    public class CreateHomePageCommand : ICommand<CommandResult<Unit>>
    {
        /// <summary>
        /// Gets or sets the article number that will become the new home page.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the title of the new home page.
        /// </summary>
        public string Title { get; set; }
    }
}
