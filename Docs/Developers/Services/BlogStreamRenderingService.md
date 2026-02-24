---
title: BlogStreamRenderingService
description: Developer reference for blog stream and post rendering used by static/dynamic publishing paths
keywords: blog, rendering, metadata, stream, snippets, publisher
audience: [developers]
---

# BlogStreamRenderingService

`BlogStreamRenderingService` renders blog stream wrappers and individual blog post snippets for the hybrid static/client-side publishing flow.

- Namespace: `Cosmos.Common.Services.BlogPublishing`
- Interface: `IBlogStreamRenderingService`
- Implementation: `BlogStreamRenderingService`
- Source: `Common/Services/BlogPublishing/BlogStreamRenderingService.cs`

## Purpose

This service supports a hybrid architecture where:

- The stream page is rendered server-side once with embedded metadata.
- Individual post snippets are rendered as standalone `<article>` blocks.
- Client JavaScript (`/js/blog-stream-loader.js`) performs pagination and insertion.

## Dependency

The service depends on:

- `ApplicationDbContext` (constructor-injected) to read blog post records from `Pages`.

## Public API

### `GenerateBlogStreamWrapperAsync(Article article, string blogKey)`

Builds a complete HTML document for a blog stream page.

Behavior:

- Validates `article` and `blogKey`.
- Calls `GenerateBlogPostMetadataJsonAsync(blogKey)`.
- Produces wrapper HTML containing:
  - Header (title, optional introduction, optional banner image)
  - Embedded JSON script block (`<script type="application/json" id="blog-posts-meta">`)
  - Empty post container (`<div id="post-list">`)
  - Pagination container (`<ul id="pagination">`)
  - CSS include (`/css/sky-blog.css`)
  - Loader script include (`/js/blog-stream-loader.js`)

### `GenerateBlogPostMetadataJsonAsync(string blogKey)`

Returns JSON metadata for all currently publishable posts in a stream.

Query/filter rules:

- `BlogKey == blogKey`
- `ArticleType == BlogPost`
- `Published` exists and is `<= UtcNow`
- `Expires` is null or `> UtcNow`

Ordering:

- `Published` descending
- then `Title` ascending (stable tie-break)

Output fields:

- `urlPath`
- `title`
- `published` (offset format)
- `updated` (offset format)
- `introduction`
- `bannerImage`

### `GenerateBlogPostSnippetAsync(Article article)`

Builds a standalone HTML `<article>` snippet for a single post.

Behavior:

- Validates `article`.
- Optionally renders a banner `<figure>`.
- Renders updated time, title, and the pre-rendered `article.Content` payload.
- Returns snippet HTML only (no layout/master wrapping).

## HTML Contract

The client loader expects these IDs in the wrapper output:

- `blog-posts-meta`
- `post-list`
- `pagination`

Changing those IDs requires coordinating updates in `/js/blog-stream-loader.js`.

## Encoding and Safety Notes

The service HTML-encodes:

- Title
- Banner image URL
- Introduction
- Date `datetime` attributes

`article.Content` is injected as pre-rendered HTML by design and is not encoded in this service.

## Exception Behavior

- Throws `ArgumentNullException` when required `Article` arguments are null.
- Throws `ArgumentException` when `blogKey` is null/empty/whitespace.

## Typical Usage

Used by publishing paths that need:

- A full stream wrapper page for blog landing URLs.
- Lightweight, reusable per-post snippets for client-side composition.

## Related Types

- `Common/Services/BlogPublishing/IBlogStreamRenderingService.cs`
- `Cosmos.Common.Data.Article`
- `Cosmos.Cms.Common.ApplicationDbContext`
