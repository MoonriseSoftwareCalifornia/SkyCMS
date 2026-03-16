// <copyright file="CacheDomainEvents.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Domain.Events
{
    using System;

    /// <summary>
    /// Event raised when an article version is unpublished.
    /// </summary>
    public sealed class ArticleUnpublishedEvent : DomainEventBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArticleUnpublishedEvent"/> class.
        /// </summary>
        /// <param name="articleNumber">Logical (stable) article number.</param>
        public ArticleUnpublishedEvent(int articleNumber)
        {
            ArticleNumber = articleNumber;
        }

        /// <summary>
        /// Gets the logical article number that was unpublished.
        /// </summary>
        public int ArticleNumber { get; }
    }

    /// <summary>
    /// Event raised when a layout is published as the default layout.
    /// </summary>
    public sealed class LayoutPublishedEvent : DomainEventBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LayoutPublishedEvent"/> class.
        /// </summary>
        /// <param name="layoutId">The unique identifier of the published layout.</param>
        public LayoutPublishedEvent(Guid layoutId)
        {
            LayoutId = layoutId;
        }

        /// <summary>
        /// Gets the unique identifier of the published layout.
        /// </summary>
        public Guid LayoutId { get; }
    }

    /// <summary>
    /// Event raised when an article catalog entry is updated or created.
    /// </summary>
    public sealed class CatalogEntryUpdatedEvent : DomainEventBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CatalogEntryUpdatedEvent"/> class.
        /// </summary>
        /// <param name="articleNumber">The article number whose catalog entry was updated.</param>
        public CatalogEntryUpdatedEvent(int articleNumber)
        {
            ArticleNumber = articleNumber;
        }

        /// <summary>
        /// Gets the article number whose catalog entry was updated.
        /// </summary>
        public int ArticleNumber { get; }
    }

    /// <summary>
    /// Event raised when an article catalog entry is deleted.
    /// </summary>
    public sealed class CatalogEntryDeletedEvent : DomainEventBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CatalogEntryDeletedEvent"/> class.
        /// </summary>
        /// <param name="articleNumber">The article number whose catalog entry was deleted.</param>
        public CatalogEntryDeletedEvent(int articleNumber)
        {
            ArticleNumber = articleNumber;
        }

        /// <summary>
        /// Gets the article number whose catalog entry was deleted.
        /// </summary>
        public int ArticleNumber { get; }
    }
}
