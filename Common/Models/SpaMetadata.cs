// <copyright file="SpaMetadata.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Common.Models;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Metadata for Single Page Application (SPA) articles stored in Article.Content as JSON.
/// Only used when ArticleType = SpaApp.
/// </summary>
/// <remarks>
/// This class is serialized to JSON and stored in the Article.Content property.
/// Contains deployment authentication credentials and deployment history.
/// </remarks>
public class SpaMetadata
{
    /// <summary>
    /// Gets or sets the BCrypt hash of the deployment key (password).
    /// Used to authenticate deployment API requests from CI/CD pipelines.
    /// </summary>
    /// <remarks>
    /// The plaintext deployment key is only shown to the user once upon creation or regeneration.
    /// Use BCrypt.Net.BCrypt.HashPassword() to generate, and BCrypt.Net.BCrypt.Verify() to validate.
    /// </remarks>
    [JsonPropertyName("deploymentKeyHash")]
    public string DeploymentKeyHash { get; set; }

    /// <summary>
    /// Gets or sets the previous deployment key hash (for rotation grace period).
    /// Valid for 24 hours after rotation to allow GitHub Actions to update secrets.
    /// </summary>
    /// <remarks>
    /// When regenerating deployment keys, the old hash is moved here to support a grace period.
    /// After 24 hours, this value should be ignored during authentication.
    /// </remarks>
    [JsonPropertyName("deploymentKeyHashPrevious")]
    public string DeploymentKeyHashPrevious { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the deployment key was last rotated.
    /// Used to enforce grace period expiration (24 hours).
    /// </summary>
    [JsonPropertyName("deploymentKeyRotatedAt")]
    public DateTimeOffset? DeploymentKeyRotatedAt { get; set; }

    /// <summary>
    /// Gets or sets the BCrypt hash of the webhook secret.
    /// Used to verify HMAC-SHA256 signatures from GitHub Actions or Azure DevOps webhooks.
    /// </summary>
    /// <remarks>
    /// The plaintext webhook secret is only shown to the user once upon creation or regeneration.
    /// GitHub sends X-Hub-Signature-256 header; Azure DevOps sends similar verification headers.
    /// </remarks>
    [JsonPropertyName("webhookSecretHash")]
    public string WebhookSecretHash { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last successful deployment.
    /// </summary>
    [JsonPropertyName("lastDeployedAt")]
    public DateTimeOffset? LastDeployedAt { get; set; }

    /// <summary>
    /// Gets or sets the Git commit SHA of the last deployment.
    /// Useful for traceability and debugging. Extracted from webhook payload.
    /// </summary>
    /// <remarks>
    /// Example: "a3f2b1c4d5e6f7g8h9i0j1k2l3m4n5o6p7q8r9s0".
    /// </remarks>
    [JsonPropertyName("lastCommitSha")]
    public string LastCommitSha { get; set; }

    /// <summary>
    /// Gets or sets the repository that last deployed (e.g., "owner/repo-name").
    /// Extracted from webhook payload for audit trail.
    /// </summary>
    /// <remarks>
    /// Example: "MoonriseSoftwareCalifornia/MyReactApp".
    /// </remarks>
    [JsonPropertyName("lastDeployedFrom")]
    public string LastDeployedFrom { get; set; }

    /// <summary>
    /// Gets or sets the deployment count (incremented on each successful deployment).
    /// Useful for monitoring and analytics.
    /// </summary>
    [JsonPropertyName("deploymentCount")]
    public int DeploymentCount { get; set; }

    /// <summary>
    /// Gets or sets additional notes or configuration (optional, for future extensibility).
    /// </summary>
    [JsonPropertyName("notes")]
    public string Notes { get; set; }
}