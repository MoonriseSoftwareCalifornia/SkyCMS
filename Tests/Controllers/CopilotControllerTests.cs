// <copyright file="CopilotControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

#nullable enable

namespace Sky.Tests.Controllers;

using CopilotController = Cosmos.Cms.Editor.Controllers.AiProxyController;
using Cosmos.Cms.Editor.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Sky.Editor.Models;
using Sky.Editor.Services.Copilot;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Tests for <see cref="CopilotController"/>.
/// </summary>
[TestClass]
public class CopilotControllerTests
{
    private const string LiveCopilotEnabledEnvVar = "SKYCMS_COPILOT_LIVE_TESTS";
    private const string LiveCopilotEndpointEnvVar = "SKYCMS_COPILOT_ENDPOINT";
    private const string LiveCopilotTokenEnvVar = "SKYCMS_COPILOT_TOKEN";
    private const string LiveCopilotModelEnvVar = "SKYCMS_COPILOT_MODEL";
    private const string DefaultLiveCopilotEndpoint = "https://models.inference.ai.azure.com/chat/completions";
    private const string DefaultLiveCopilotModel = "gpt-4o-mini";
    private const string UserSecretsId = "c44b0fbc-a20c-4a15-8e5b-1a9eb09e6ac1";

    private Mock<IHttpClientFactory> httpClientFactoryMock = null!;
    private Mock<ICopilotProxyOptionsService> optionsServiceMock = null!;
    private Mock<IAiProviderModelCatalogService> modelCatalogServiceMock = null!;
    private Mock<IAiUserPreferenceService> userPreferenceServiceMock = null!;
    private Mock<IAiDocumentationContextService> documentationContextServiceMock = null!;
    private Mock<IAiLayoutContextService> layoutContextServiceMock = null!;
    private Mock<ILogger<AiProxyController>> loggerMock = null!;
    private AiProxyController controller = null!;

    [TestInitialize]
    public void Setup()
    {
        httpClientFactoryMock = new Mock<IHttpClientFactory>();
        optionsServiceMock = new Mock<ICopilotProxyOptionsService>();
        modelCatalogServiceMock = new Mock<IAiProviderModelCatalogService>();
        userPreferenceServiceMock = new Mock<IAiUserPreferenceService>();
        documentationContextServiceMock = new Mock<IAiDocumentationContextService>();
        layoutContextServiceMock = new Mock<IAiLayoutContextService>();
        loggerMock = new Mock<ILogger<AiProxyController>>();

        userPreferenceServiceMock
            .Setup(s => s.GetSelectedModelAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        documentationContextServiceMock
            .Setup(s => s.GetDocumentationContextAsync(It.IsAny<AiContextEnrichmentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiDocumentationContextResult());

        layoutContextServiceMock
            .Setup(s => s.GetLayoutContextAsync(It.IsAny<AiContextEnrichmentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiLayoutContextResult());

        controller = new AiProxyController(
            httpClientFactoryMock.Object,
            optionsServiceMock.Object,
            modelCatalogServiceMock.Object,
            userPreferenceServiceMock.Object,
            documentationContextServiceMock.Object,
            layoutContextServiceMock.Object,
            loggerMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [TestMethod]
    public async Task Status_WithAzureOpenAiEndpoint_ReturnsInferredDiscoveryState()
    {
        optionsServiceMock
            .Setup(s => s.GetOptionsAsync())
            .ReturnsAsync(new CopilotProxyOptions
            {
                Enabled = true,
                Endpoint = "https://example.openai.azure.com/openai/deployments/editor-deployment/chat/completions?api-version=2024-10-21",
                AccessToken = "token",
                Model = "auto",
            });

        var result = await controller.Status();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        var payload = ok.Value as AiProxyController.CopilotProxyStatusResponse;
        Assert.IsNotNull(payload);
        Assert.AreEqual("azure-openai", payload.ProviderKey);
        Assert.AreEqual(AiProviderDiscoveryStates.Inferred, payload.DiscoveryState);
        Assert.AreEqual("editor-deployment", payload.EffectiveModel);
    }

    [TestMethod]
    public async Task Models_WithCatalogDiscoveryState_ReturnsStateAndMessage()
    {
        optionsServiceMock
            .Setup(s => s.GetOptionsAsync())
            .ReturnsAsync(new CopilotProxyOptions
            {
                Enabled = true,
                Endpoint = "https://example.openai.azure.com/openai/deployments/editor-deployment/chat/completions?api-version=2024-10-21",
                AccessToken = "token",
                Model = "auto",
            });

        modelCatalogServiceMock
            .Setup(s => s.GetCatalogAsync(It.IsAny<CopilotProxyOptions>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiProviderModelCatalogResult
            {
                ProviderKey = "azure-openai",
                ProviderDisplayName = "Azure OpenAI",
                SupportsModelDiscovery = true,
                DiscoveryState = AiProviderDiscoveryStates.Inferred,
                DiscoveryStateMessage = "SkyCMS inferred the Azure OpenAI deployment from the configured endpoint.",
                Models =
                [
                    new AiProviderModelOption { Id = "editor-deployment", DisplayName = "editor-deployment", Recommended = true },
                ],
            });

        var result = await controller.Models();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        var payload = ok.Value as AiProxyController.CopilotProxyModelsResponse;
        Assert.IsNotNull(payload);
        Assert.AreEqual(AiProviderDiscoveryStates.Inferred, payload.DiscoveryState);
        Assert.IsTrue(payload.DiscoveryStateMessage.Contains("inferred", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(1, payload.Models.Count);
    }

    [TestMethod]
    public async Task Complete_WithNullRequest_ReturnsBadRequest()
    {
        var result = await controller.Complete(null!);

        Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
    }

    [TestMethod]
    public async Task Complete_WithEmptyPrefix_ReturnsEmptyResponse()
    {
        var request = new AiProxyController.CopilotCompletionRequest
        {
            Prefix = string.Empty,
        };

        var result = await controller.Complete(request);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        var payload = ok.Value as AiProxyController.CopilotCompletionResponse;
        Assert.IsNotNull(payload);
        Assert.IsNull(payload.Completion);
        Assert.AreEqual(0, payload.Completions.Count);
    }

    [TestMethod]
    public async Task Complete_WithDisabledOptions_Returns503()
    {
        optionsServiceMock
            .Setup(s => s.GetOptionsAsync())
            .ReturnsAsync(new CopilotProxyOptions { Enabled = false });

        var request = new AiProxyController.CopilotCompletionRequest
        {
            Prefix = "public class Demo",
        };

        var result = await controller.Complete(request);

        var objectResult = result as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    [TestMethod]
    public async Task Complete_WithMissingEndpointOrToken_Returns503()
    {
        optionsServiceMock
            .Setup(s => s.GetOptionsAsync())
            .ReturnsAsync(new CopilotProxyOptions
            {
                Enabled = true,
                Endpoint = string.Empty,
                AccessToken = string.Empty,
            });

        var request = new AiProxyController.CopilotCompletionRequest
        {
            Prefix = "public class Demo",
        };

        var result = await controller.Complete(request);

        var objectResult = result as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    [TestMethod]
    public async Task Complete_WithUpstreamError_Returns502()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
        };

        optionsServiceMock
            .Setup(s => s.GetOptionsAsync())
            .ReturnsAsync(options);

        var httpClient = CreateHttpClient((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway));
        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var request = new AiProxyController.CopilotCompletionRequest
        {
            Prefix = "public class Demo",
        };

        var result = await controller.Complete(request);

        var objectResult = result as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(StatusCodes.Status502BadGateway, objectResult.StatusCode);
    }

    [TestMethod]
    public async Task Complete_WithOperationCanceled_Returns504()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
            TimeoutMs = 5000,
        };

        optionsServiceMock
            .Setup(s => s.GetOptionsAsync())
            .ReturnsAsync(options);

        var httpClient = CreateHttpClient((_, _) => throw new OperationCanceledException());
        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var request = new AiProxyController.CopilotCompletionRequest
        {
            Prefix = "public class Demo",
        };

        var result = await controller.Complete(request);

        var objectResult = result as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(StatusCodes.Status504GatewayTimeout, objectResult.StatusCode);
    }

    [TestMethod]
    public async Task Complete_WithUpstreamSuccess_ReturnsCompletion()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
            Model = "gpt-4o-mini",
            MaxTokens = 128,
        };

        optionsServiceMock
            .Setup(s => s.GetOptionsAsync())
            .ReturnsAsync(options);

        HttpRequestMessage? capturedRequest = null;
        const string responseJson = "{\"choices\":[{\"message\":{\"content\":\"return true;\\n\"}}]}";

        var httpClient = CreateHttpClient((request, _) =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        });

        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var requestModel = new AiProxyController.CopilotCompletionRequest
        {
            Prefix = "if (isValid)\n{\n    ",
            Language = "csharp",
        };

        var result = await controller.Complete(requestModel);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);

        var payload = ok.Value as AiProxyController.CopilotCompletionResponse;
        Assert.IsNotNull(payload);
        Assert.AreEqual("return true;", payload.Completion);
        Assert.AreEqual(1, payload.Completions.Count);
        Assert.AreEqual("return true;", payload.Completions[0]);

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Post, capturedRequest.Method);
        Assert.AreEqual("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.AreEqual("token", capturedRequest.Headers.Authorization?.Parameter);
    }

    [TestMethod]
    public async Task Complete_WithUpstreamEmptyContent_ReturnsEmptyResponse()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
        };

        optionsServiceMock
            .Setup(s => s.GetOptionsAsync())
            .ReturnsAsync(options);

        const string responseJson = "{\"choices\":[{\"message\":{\"content\":\"   \"}}]}";

        var httpClient = CreateHttpClient((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });

        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var request = new AiProxyController.CopilotCompletionRequest
        {
            Prefix = "public class Demo",
        };

        var result = await controller.Complete(request);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        var payload = ok.Value as AiProxyController.CopilotCompletionResponse;
        Assert.IsNotNull(payload);
        Assert.IsNull(payload.Completion);
        Assert.AreEqual(0, payload.Completions.Count);
    }

    [TestMethod]
    public async Task Complete_WithUnexpectedException_Returns500()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
        };

        optionsServiceMock
            .Setup(s => s.GetOptionsAsync())
            .ReturnsAsync(options);

        var httpClient = CreateHttpClient((_, _) => throw new InvalidOperationException("boom"));
        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var request = new AiProxyController.CopilotCompletionRequest
        {
            Prefix = "public class Demo",
        };

        var result = await controller.Complete(request);

        var objectResult = result as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    [TestMethod]
    public async Task Chat_WithCkeditorRequest_UsesRichTextSystemPrompt()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
            Model = "gpt-4o-mini",
            MaxTokens = 512,
        };

        optionsServiceMock
            .Setup(s => s.GetOptionsAsync())
            .ReturnsAsync(options);

        string? capturedJson = null;

        var httpClient = CreateHttpClient((request, _) =>
        {
            capturedJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Here is an improved version.\"}}]}", Encoding.UTF8, "application/json"),
            };
        });

        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var requestModel = new AiProxyController.CopilotChatRequest
        {
            EditorKind = "ckeditor",
            Action = "improve-selection",
            Message = "Improve this paragraph.",
            Selection = "<p>This are rough copy.</p>",
            CurrentCode = "<p>This are rough copy.</p><p>Second paragraph.</p>",
            Language = "html",
            FieldName = "Body",
            Title = "Demo",
            ArticleNumber = "123",
        };

        var result = await controller.Chat(requestModel);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.IsFalse(string.IsNullOrWhiteSpace(capturedJson));

        using var document = JsonDocument.Parse(capturedJson!);
        var messages = document.RootElement.GetProperty("messages");
        var systemPrompt = messages[0].GetProperty("content").GetString();
        var userPrompt = messages[1].GetProperty("content").GetString();

        Assert.IsNotNull(systemPrompt);
        Assert.IsTrue(systemPrompt.Contains("AI writing assistant", StringComparison.Ordinal));
        Assert.IsTrue(systemPrompt.Contains("```html```", StringComparison.Ordinal));

        Assert.IsNotNull(userPrompt);
        Assert.IsTrue(userPrompt.Contains("EditorKind: ckeditor", StringComparison.Ordinal));
        Assert.IsTrue(userPrompt.Contains("Selected HTML fragment", StringComparison.Ordinal));
        Assert.IsTrue(userPrompt.Contains("Current editor HTML fragment", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Chat_WithMonacoRequest_KeepsCodingPrompt()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
            Model = "gpt-4o-mini",
            MaxTokens = 512,
        };

        optionsServiceMock
            .Setup(s => s.GetOptionsAsync())
            .ReturnsAsync(options);

        string? capturedJson = null;

        var httpClient = CreateHttpClient((request, _) =>
        {
            capturedJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Use a guard clause.\"}}]}", Encoding.UTF8, "application/json"),
            };
        });

        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await controller.Chat(new AiProxyController.CopilotChatRequest
        {
            Action = "fix-syntax",
            Message = "Fix this method.",
            CurrentCode = "public void Run() {",
            Language = "csharp",
        });

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.IsFalse(string.IsNullOrWhiteSpace(capturedJson));

        using var document = JsonDocument.Parse(capturedJson!);
        var systemPrompt = document.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();

        Assert.IsNotNull(systemPrompt);
        Assert.IsTrue(systemPrompt.Contains("AI coding assistant", StringComparison.Ordinal));
        Assert.IsFalse(systemPrompt.Contains("single rich-text editor region only", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Complete_WithLiveGitHubCopilotConnection_ReturnsCompletion()
    {
        var payload = await ExecuteLiveCompletionAsync(new AiProxyController.CopilotCompletionRequest
        {
            Prefix = "public static int Add(int a, int b)\n{\n    ",
            Language = "csharp",
            FieldId = "live-test-basic",
        });

        Assert.IsFalse(string.IsNullOrWhiteSpace(payload.Completion));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Complete_WithLiveGitHubCopilotRazorScenario_ReturnsCompletion()
    {
        var payload = await ExecuteLiveCompletionAsync(new AiProxyController.CopilotCompletionRequest
        {
            Prefix = "@if (Model.Items?.Any() == true)\n{\n    <ul>\n        ",
            Suffix = "\n    </ul>\n}",
            Language = "razor",
            FieldId = "live-test-razor",
        });

        Assert.IsTrue(payload.Completion!.Contains("<", StringComparison.Ordinal)
            || payload.Completion.Contains("@", StringComparison.Ordinal)
            || payload.Completion.Contains("li", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Complete_WithLiveGitHubCopilotUnitTestScenario_ReturnsCompletion()
    {
        var payload = await ExecuteLiveCompletionAsync(new AiProxyController.CopilotCompletionRequest
        {
            Prefix = "[TestMethod]\npublic void Add_WhenPositiveValues_ReturnsSum()\n{\n    var result = Add(2, 3);\n    ",
            Language = "csharp",
            FieldId = "live-test-unit",
        });

        Assert.IsTrue(payload.Completion!.Contains("Assert", StringComparison.OrdinalIgnoreCase)
            || payload.Completion.Contains("AreEqual", StringComparison.OrdinalIgnoreCase)
            || payload.Completion.Contains("IsTrue", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Complete_WithLiveGitHubCopilotNullSafetyScenario_ReturnsCompletion()
    {
        var payload = await ExecuteLiveCompletionAsync(new AiProxyController.CopilotCompletionRequest
        {
            Prefix = "public static string GetDisplayName(User? user)\n{\n    ",
            Language = "csharp",
            FieldId = "live-test-null-safety",
        });

        Assert.IsTrue(payload.Completion!.Contains("user", StringComparison.OrdinalIgnoreCase)
            || payload.Completion.Contains("null", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Complete_WithLiveGitHubCopilotSuffixAwareScenario_ReturnsCompletion()
    {
        const string suffixMarker = "/* END_MARKER */";

        var payload = await ExecuteLiveCompletionAsync(new AiProxyController.CopilotCompletionRequest
        {
            Prefix = "public static int Multiply(int x, int y)\n{\n    var result = x * y;\n    ",
            Suffix = $"\n{suffixMarker}\n}}",
            Language = "csharp",
            FieldId = "live-test-suffix",
        });

        Assert.IsFalse(payload.Completion!.Contains(suffixMarker, StringComparison.Ordinal));
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send)
    {
        var handler = new DelegateHttpMessageHandler(send);
        return new HttpClient(handler);
    }

    private async Task<AiProxyController.CopilotCompletionResponse> ExecuteLiveCompletionAsync(
        AiProxyController.CopilotCompletionRequest request)
    {
        if (!IsLiveTestEnabled())
        {
            Assert.Inconclusive($"Set {LiveCopilotEnabledEnvVar}=true to run live Copilot integration tests.");
        }

        var token = ResolveLiveCopilotToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            Assert.Inconclusive(
                $"Live token not found. Set {LiveCopilotTokenEnvVar} or CoPilotAccessToken in user secrets.");
        }

        optionsServiceMock
            .Setup(s => s.GetOptionsAsync())
            .ReturnsAsync(new CopilotProxyOptions
            {
                Enabled = true,
                Endpoint = ResolveLiveCopilotEndpoint(),
                AccessToken = token,
                Model = ResolveLiveCopilotModel(),
                TimeoutMs = 30000,
                MaxTokens = 96,
                Temperature = 0.1,
            });

        httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());

        var result = await controller.Complete(request);

        if (result is ObjectResult error && error.StatusCode >= StatusCodes.Status400BadRequest)
        {
            Assert.Fail($"Live Copilot request failed with status {error.StatusCode}.");
        }

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);

        var payload = ok.Value as AiProxyController.CopilotCompletionResponse;
        Assert.IsNotNull(payload);

        AssertLiveCompletionShape(payload);
        return payload;
    }

    private static void AssertLiveCompletionShape(AiProxyController.CopilotCompletionResponse payload)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(payload.Completion));
        Assert.IsTrue(payload.Completions.Count > 0);
        Assert.AreEqual(payload.Completion, payload.Completions[0]);
        Assert.IsFalse(payload.Completion!.Contains("```", StringComparison.Ordinal));
    }

    private static bool IsLiveTestEnabled()
    {
        var value = Environment.GetEnvironmentVariable(LiveCopilotEnabledEnvVar);
        return value?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string ResolveLiveCopilotEndpoint()
    {
        return Environment.GetEnvironmentVariable(LiveCopilotEndpointEnvVar) ?? DefaultLiveCopilotEndpoint;
    }

    private static string ResolveLiveCopilotModel()
    {
        return Environment.GetEnvironmentVariable(LiveCopilotModelEnvVar) ?? DefaultLiveCopilotModel;
    }

    private static string? ResolveLiveCopilotToken()
    {
        var token = Environment.GetEnvironmentVariable(LiveCopilotTokenEnvVar);
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        return TryReadTokenFromUserSecrets();
    }

    private static string? TryReadTokenFromUserSecrets()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var secretsPath = Path.Combine(appData, "Microsoft", "UserSecrets", UserSecretsId, "secrets.json");
            if (!File.Exists(secretsPath))
            {
                return null;
            }

            using var stream = File.OpenRead(secretsPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("CoPilotAccessToken", out var tokenElement))
            {
                var token = tokenElement.GetString();
                return string.IsNullOrWhiteSpace(token) ? null : token;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    [TestMethod]
    public async Task Chat_WithLayoutHeadContext_UsesLayoutHeadSystemPrompt()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
            Model = "gpt-4o-mini",
            MaxTokens = 512,
        };

        optionsServiceMock.Setup(s => s.GetOptionsAsync()).ReturnsAsync(options);

        string? capturedJson = null;
        var httpClient = CreateHttpClient((request, _) =>
        {
            capturedJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Add Open Graph tags.\"}}]}", Encoding.UTF8, "application/json"),
            };
        });

        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await controller.Chat(new AiProxyController.CopilotChatRequest
        {
            Action = "chat",
            Message = "Help with the head section.",
            CurrentCode = "<meta charset=\"utf-8\">",
            Language = "html",
            DocumentKind = "layout",
            SectionKind = "layout-head",
        });

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);

        using var document = JsonDocument.Parse(capturedJson!);
        var systemPrompt = document.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();

        Assert.IsNotNull(systemPrompt);
        Assert.IsTrue(systemPrompt.Contains("layout", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(systemPrompt.Contains("head", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(systemPrompt.Contains("AI writing assistant", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Chat_WithLayoutBodyStartContext_UsesBodyStartSystemPrompt()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
        };

        optionsServiceMock.Setup(s => s.GetOptionsAsync()).ReturnsAsync(options);

        string? capturedJson = null;
        var httpClient = CreateHttpClient((request, _) =>
        {
            capturedJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Add a nav element.\"}}]}", Encoding.UTF8, "application/json"),
            };
        });

        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await controller.Chat(new AiProxyController.CopilotChatRequest
        {
            Action = "chat",
            Message = "Help with the header region.",
            DocumentKind = "layout",
            SectionKind = "layout-body-start",
        });

        Assert.IsInstanceOfType(result, typeof(OkObjectResult));

        using var document = JsonDocument.Parse(capturedJson!);
        var systemPrompt = document.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();

        Assert.IsNotNull(systemPrompt);
        Assert.IsTrue(systemPrompt.Contains("body-start", StringComparison.OrdinalIgnoreCase)
            || systemPrompt.Contains("header", StringComparison.OrdinalIgnoreCase)
            || systemPrompt.Contains("navigation", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Chat_WithTemplateContext_UsesTemplateSystemPrompt()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
        };

        optionsServiceMock.Setup(s => s.GetOptionsAsync()).ReturnsAsync(options);

        string? capturedJson = null;
        var httpClient = CreateHttpClient((request, _) =>
        {
            capturedJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Add a section placeholder.\"}}]}", Encoding.UTF8, "application/json"),
            };
        });

        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await controller.Chat(new AiProxyController.CopilotChatRequest
        {
            Action = "chat",
            Message = "Add a content placeholder.",
            DocumentKind = "template",
            SectionKind = "template-content",
        });

        Assert.IsInstanceOfType(result, typeof(OkObjectResult));

        using var document = JsonDocument.Parse(capturedJson!);
        var systemPrompt = document.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();

        Assert.IsNotNull(systemPrompt);
        Assert.IsTrue(systemPrompt.Contains("template", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(systemPrompt.Contains("AI writing assistant", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Chat_WithBlogCkeditorContext_IncludesBlogGuidanceInSystemPrompt()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
        };

        optionsServiceMock.Setup(s => s.GetOptionsAsync()).ReturnsAsync(options);

        string? capturedJson = null;
        var httpClient = CreateHttpClient((request, _) =>
        {
            capturedJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Here is a punchy intro.\"}}]}", Encoding.UTF8, "application/json"),
            };
        });

        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await controller.Chat(new AiProxyController.CopilotChatRequest
        {
            EditorKind = "ckeditor",
            Action = "improve-selection",
            Message = "Make this intro more engaging.",
            Selection = "<p>Welcome to our blog.</p>",
            CurrentCode = "<p>Welcome to our blog.</p>",
            Language = "html",
            DocumentKind = "blog",
            SectionKind = "blog-content",
            ArticleType = "BlogPost",
            Category = "News",
        });

        Assert.IsInstanceOfType(result, typeof(OkObjectResult));

        using var document = JsonDocument.Parse(capturedJson!);
        var systemPrompt = document.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();
        var userPrompt = document.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();

        Assert.IsNotNull(systemPrompt);
        Assert.IsTrue(systemPrompt.Contains("AI writing assistant", StringComparison.Ordinal));
        Assert.IsTrue(systemPrompt.Contains("blog post", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(userPrompt);
        Assert.IsTrue(userPrompt.Contains("DocumentKind: blog", StringComparison.Ordinal));
        Assert.IsTrue(userPrompt.Contains("ArticleType: BlogPost", StringComparison.Ordinal));
        Assert.IsTrue(userPrompt.Contains("Category: News", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Chat_WithArticleContentContext_IncludesContextInUserPrompt()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
        };

        optionsServiceMock.Setup(s => s.GetOptionsAsync()).ReturnsAsync(options);

        string? capturedJson = null;
        var httpClient = CreateHttpClient((request, _) =>
        {
            capturedJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Fixed.\"}}]}", Encoding.UTF8, "application/json"),
            };
        });

        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await controller.Chat(new AiProxyController.CopilotChatRequest
        {
            Action = "fix-syntax",
            Message = "Fix the markup.",
            CurrentCode = "<div><p>Hello</div>",
            Language = "html",
            DocumentKind = "article",
            SectionKind = "article-content",
            UrlPath = "about/team",
        });

        Assert.IsInstanceOfType(result, typeof(OkObjectResult));

        using var document = JsonDocument.Parse(capturedJson!);
        var userPrompt = document.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();

        Assert.IsNotNull(userPrompt);
        Assert.IsTrue(userPrompt.Contains("DocumentKind: article", StringComparison.Ordinal));
        Assert.IsTrue(userPrompt.Contains("SectionKind: article-content", StringComparison.Ordinal));
        Assert.IsTrue(userPrompt.Contains("UrlPath: about/team", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Chat_WithNoContext_FallsBackToDefaultSystemPrompt()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
        };

        optionsServiceMock.Setup(s => s.GetOptionsAsync()).ReturnsAsync(options);

        string? capturedJson = null;
        var httpClient = CreateHttpClient((request, _) =>
        {
            capturedJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Done.\"}}]}", Encoding.UTF8, "application/json"),
            };
        });

        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await controller.Chat(new AiProxyController.CopilotChatRequest
        {
            Action = "chat",
            Message = "Help with this code.",
            CurrentCode = "function hello() {}",
            Language = "javascript",
        });

        Assert.IsInstanceOfType(result, typeof(OkObjectResult));

        using var document = JsonDocument.Parse(capturedJson!);
        var systemPrompt = document.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();

        Assert.IsNotNull(systemPrompt);
        Assert.IsTrue(systemPrompt.Contains("AI coding assistant", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Chat_WithGeneralHelpMode_UsesGeneralHelpSystemPrompt()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
            Model = "gpt-4o-mini",
            MaxTokens = 512,
        };

        optionsServiceMock
            .Setup(s => s.GetOptionsAsync())
            .ReturnsAsync(options);

        string? capturedJson = null;

        var httpClient = CreateHttpClient((request, _) =>
        {
            capturedJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Use the Editor menu to manage pages.\"}}]}", Encoding.UTF8, "application/json"),
            };
        });

        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await controller.Chat(new AiProxyController.CopilotChatRequest
        {
            EditorKind = "help",
            ChatMode = "general-help",
            Action = "chat",
            Message = "How do I manage page drafts in SkyCMS?",
        });

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.IsFalse(string.IsNullOrWhiteSpace(capturedJson));

        using var document = JsonDocument.Parse(capturedJson!);
        var systemPrompt = document.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();

        Assert.IsNotNull(systemPrompt);
        Assert.IsTrue(systemPrompt.Contains("AI help assistant", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(systemPrompt.Contains("non-editor chat", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(systemPrompt.Contains("AI coding assistant", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Chat_WithSiteHelpMode_UsesSiteAwareHelpSystemPrompt()
    {
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            AccessToken = "token",
            Model = "gpt-4o-mini",
            MaxTokens = 512,
        };

        optionsServiceMock
            .Setup(s => s.GetOptionsAsync())
            .ReturnsAsync(options);

        string? capturedJson = null;

        var httpClient = CreateHttpClient((request, _) =>
        {
            capturedJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Review your /about/team page hierarchy first.\"}}]}", Encoding.UTF8, "application/json"),
            };
        });

        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await controller.Chat(new AiProxyController.CopilotChatRequest
        {
            EditorKind = "help",
            ChatMode = "site-help",
            Action = "site-help",
            Message = "Where should team bios live on this site?",
            UrlPath = "about/team",
            DocumentKind = "article",
        });

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.IsFalse(string.IsNullOrWhiteSpace(capturedJson));

        using var document = JsonDocument.Parse(capturedJson!);
        var systemPrompt = document.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();
        var userPrompt = document.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();

        Assert.IsNotNull(systemPrompt);
        Assert.IsTrue(systemPrompt.Contains("website teams", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(systemPrompt.Contains("site and page context", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(userPrompt);
        Assert.IsTrue(userPrompt.Contains("ChatMode: site-help", StringComparison.Ordinal));
        Assert.IsTrue(userPrompt.Contains("UrlPath: about/team", StringComparison.Ordinal));
    }

    private sealed class DelegateHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send;

        public DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send)
        {
            this.send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(send(request, cancellationToken));
        }
    }
}
