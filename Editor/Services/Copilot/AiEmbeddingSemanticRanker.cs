// <copyright file="AiEmbeddingSemanticRanker.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sky.Editor.Models;

/// <summary>
/// Optional embedding-based semantic reranker for help knowledge search.
/// </summary>
public sealed class AiEmbeddingSemanticRanker : IAiEmbeddingSemanticRanker
{
    private readonly ICopilotProxyOptionsService copilotProxyOptionsService;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<AiEmbeddingSemanticRanker> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiEmbeddingSemanticRanker"/> class.
    /// </summary>
    /// <param name="copilotProxyOptionsService">Tenant-aware AI options service.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public AiEmbeddingSemanticRanker(
        ICopilotProxyOptionsService copilotProxyOptionsService,
        IHttpClientFactory httpClientFactory,
        ILogger<AiEmbeddingSemanticRanker> logger)
    {
        this.copilotProxyOptionsService = copilotProxyOptionsService;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AiEmbeddingSemanticScore>> ScoreAsync(string query, IReadOnlyList<string> candidates, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || candidates.Count == 0)
        {
            return [];
        }

        CopilotProxyOptions options;
        try
        {
            options = await this.copilotProxyOptionsService.GetOptionsAsync();
        }
        catch (Exception ex)
        {
            this.logger.LogDebug(ex, "Semantic reranking unavailable because AI options could not be loaded.");
            return [];
        }

        if (!options.EnableEmbeddingSemanticRerank || string.IsNullOrWhiteSpace(options.AccessToken))
        {
            return [];
        }

        var provider = AiProviderMetadataResolver.ResolveProviderKey(options.Endpoint, options.Model);
        if (!string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            this.logger.LogDebug("Embedding semantic reranking is enabled but provider {Provider} does not currently support embeddings in this implementation.", provider);
            return [];
        }

        var model = string.IsNullOrWhiteSpace(options.EmbeddingModel) ? "text-embedding-3-small" : options.EmbeddingModel.Trim();
        var input = new List<string>(candidates.Count + 1) { query };
        input.AddRange(candidates);

        try
        {
            var client = this.httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(8);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings")
            {
                Content = JsonContent.Create(new EmbeddingRequest
                {
                    Model = model,
                    Input = input,
                }),
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                this.logger.LogDebug("Embedding semantic reranking request returned {StatusCode}.", response.StatusCode);
                return [];
            }

            var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (payload?.Data == null || payload.Data.Count != input.Count)
            {
                return [];
            }

            var ordered = payload.Data
                .Where(d => d.Embedding != null)
                .OrderBy(d => d.Index)
                .Select(d => d.Embedding!)
                .ToList();

            if (ordered.Count != input.Count)
            {
                return [];
            }

            var queryVector = ordered[0];
            var scores = new List<AiEmbeddingSemanticScore>(candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                var score = ComputeCosineSimilarity(queryVector, ordered[i + 1]);
                scores.Add(new AiEmbeddingSemanticScore(i, score));
            }

            return scores;
        }
        catch (Exception ex)
        {
            this.logger.LogDebug(ex, "Embedding semantic reranking failed; falling back to local hybrid ranking.");
            return [];
        }
    }

    private static double ComputeCosineSimilarity(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        if (left.Count == 0 || right.Count == 0 || left.Count != right.Count)
        {
            return 0;
        }

        var dot = 0d;
        var leftNorm = 0d;
        var rightNorm = 0d;

        for (var i = 0; i < left.Count; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        if (dot <= 0 || leftNorm <= 0 || rightNorm <= 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }

    private sealed class EmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("input")]
        public List<string> Input { get; set; } = [];
    }

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData>? Data { get; set; }
    }

    private sealed class EmbeddingData
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public List<double>? Embedding { get; set; }
    }
}
