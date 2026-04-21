// <copyright file="AiProviderMetadataResolverTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Editor.Services.Copilot;

using Sky.Editor.Services.Copilot;

/// <summary>
/// Tests for <see cref="AiProviderMetadataResolver"/>.
/// </summary>
[TestClass]
public class AiProviderMetadataResolverTests
{
    [TestMethod]
    public void ResolveEffectiveModel_WithOpenAiAuto_ReturnsSkyCmsDefaultModel()
    {
        var result = AiProviderMetadataResolver.ResolveEffectiveModel(
            "https://api.openai.com/v1/chat/completions",
            "auto");

        Assert.AreEqual("gpt-4o-mini", result);
    }

    [TestMethod]
    public void ResolveEffectiveModel_WithAzureOpenAiAuto_UsesDeploymentFromEndpoint()
    {
        var result = AiProviderMetadataResolver.ResolveEffectiveModel(
            "https://example.openai.azure.com/openai/deployments/editor-deployment/chat/completions?api-version=2024-10-21",
            "auto");

        Assert.AreEqual("editor-deployment", result);
    }

    [TestMethod]
    public void Describe_WithGitHubModelsEndpoint_SupportsDiscoveryAndUserSelection()
    {
        var metadata = AiProviderMetadataResolver.Describe(
            "https://models.inference.ai.azure.com/chat/completions",
            "auto");

        Assert.AreEqual("github-models", metadata.ProviderKey);
        Assert.AreEqual("GitHub Models", metadata.ProviderDisplayName);
        Assert.IsTrue(metadata.SupportsModelDiscovery);
        Assert.IsTrue(metadata.SupportsUserModelSelection);
        Assert.IsTrue(metadata.SupportsAutoMode);
    }

    [TestMethod]
    public void Describe_WithAzureOpenAiEndpoint_UsesInferredDiscoveryState()
    {
        var metadata = AiProviderMetadataResolver.Describe(
            "https://example.openai.azure.com/openai/deployments/editor-deployment/chat/completions?api-version=2024-10-21",
            "auto");

        Assert.AreEqual("azure-openai", metadata.ProviderKey);
        Assert.AreEqual("Azure OpenAI", metadata.ProviderDisplayName);
        Assert.AreEqual(AiProviderDiscoveryStates.Inferred, metadata.DiscoveryState);
        Assert.IsTrue(metadata.SupportsModelDiscovery);
        Assert.IsFalse(metadata.SupportsUserModelSelection);
    }

    [TestMethod]
    public void Describe_WithFoundryEndpoint_RequiresAdditionalConfiguration()
    {
        var metadata = AiProviderMetadataResolver.Describe(
            "https://example.services.ai.azure.com/models/chat/completions?api-version=2024-05-01-preview",
            "auto");

        Assert.AreEqual("azure-ai-foundry", metadata.ProviderKey);
        Assert.AreEqual("Azure AI Foundry", metadata.ProviderDisplayName);
        Assert.AreEqual(AiProviderDiscoveryStates.NeedsAdditionalConfiguration, metadata.DiscoveryState);
        Assert.IsFalse(metadata.SupportsUserModelSelection);
    }
}