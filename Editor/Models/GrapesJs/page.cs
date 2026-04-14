// <copyright file="page.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Models.GrapesJs
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a project in GrapesJs.
    /// </summary>
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
    public class Project
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Project"/> class.
        /// </summary>
        public Project()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Project"/> class with HTML content.
        /// </summary>
        /// <param name="html">HTML content.</param>
        public Project(string html)
        {
            Pages.Add(new Page(html));
        }

        /// <summary>
        /// Gets or sets the project pages.
        /// </summary>
        public List<Page> Pages { get; set; } = new List<Page>();
    }

    /// <summary>
    /// Represents a page in GrapesJs.
    /// </summary>
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
    public class Page
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Page"/> class.
        /// </summary>
        public Page()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Page"/> class with content.
        /// </summary>
        /// <param name="html">HTML content.</param>
        public Page(string html)
        {
            Component = html;
        }

        /// <summary>
        /// Gets or sets the page component (HTML content).
        /// </summary>
        public string Component { get; set; } = string.Empty;
    }
}
