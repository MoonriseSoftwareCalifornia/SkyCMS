// <copyright file="ArticleExtensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Common.Extensions;

using Cosmos.Cms.Common.Models;
using Cosmos.Common.Data;
using System;
using System.Text.Json;

/// <summary>
/// Extension methods for working with Article entities, especially SPA-specific operations.
/// </summary>
public static class ArticleExtensions
{
    /// <summary>
    /// Determines whether the article is a Single Page Application (SPA) type.
    /// </summary>
    /// <param name="article">The article to check.</param>
    /// <returns>True if ArticleType is SpaApp, false otherwise.</returns>
    public static bool IsSpaArticle(this Article article)
    {
        return article?.ArticleType == (int)ArticleType.SpaApp;
    }

    /// <summary>
    /// Deserializes and retrieves SPA metadata from the Article.Content property.
    /// </summary>
    /// <param name="article">The SPA article.</param>
    /// <returns>Deserialized SpaMetadata object.</returns>
    /// <exception cref="InvalidOperationException">Thrown when article is not a SPA type.</exception>
    /// <exception cref="JsonException">Thrown when Content is not valid JSON.</exception>
    public static SpaMetadata GetSpaMetadata(this Article article)
    {
        if (!article.IsSpaArticle())
        {
            throw new InvalidOperationException(
                $"Cannot get SPA metadata for article {article.Id}. ArticleType must be SpaApp (3), but was {article.ArticleType}.");
        }

        if (string.IsNullOrWhiteSpace(article.Content))
        {
            return new SpaMetadata(); // Return empty metadata if Content is null
        }

        try
        {
            return JsonSerializer.Deserialize<SpaMetadata>(article.Content)
                   ?? new SpaMetadata();
        }
        catch (JsonException ex)
        {
            throw new JsonException(
                $"Failed to deserialize SPA metadata for article {article.Id}. Content may be corrupt.", ex);
        }
    }

    /// <summary>
    /// Serializes and stores SPA metadata in the Article.Content property.
    /// </summary>
    /// <param name="article">The SPA article.</param>
    /// <param name="metadata">The metadata to store.</param>
    /// <exception cref="InvalidOperationException">Thrown when article is not a SPA type.</exception>
    public static void SetSpaMetadata(this Article article, SpaMetadata metadata)
    {
        if (!article.IsSpaArticle())
        {
            throw new InvalidOperationException(
                $"Cannot set SPA metadata for article {article.Id}. ArticleType must be SpaApp (3), but was {article.ArticleType}.");
        }

        article.Content = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true // Make JSON human-readable for debugging
        });
    }

    /// <summary>
    /// Attempts to retrieve SPA metadata, returning null if the article is not a SPA or parsing fails.
    /// </summary>
    /// <param name="article">The article to check.</param>
    /// <param name="metadata">The deserialized metadata (null if failed).</param>
    /// <returns>True if metadata was successfully retrieved, false otherwise.</returns>
    public static bool TryGetSpaMetadata(this Article article, out SpaMetadata metadata)
    {
        metadata = null;

        if (!article.IsSpaArticle())
        {
            return false;
        }

        try
        {
            metadata = article.GetSpaMetadata();
            return true;
        }
        catch
        {
            return false;
        }
    }
}