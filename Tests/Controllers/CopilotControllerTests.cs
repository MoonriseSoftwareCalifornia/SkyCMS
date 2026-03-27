// <copyright file="CopilotControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

#nullable enable

namespace Sky.Tests.Controllers;

using Cosmos.Cms.Editor.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Sky.Editor.Models;
using Sky.Editor.Services.Copilot;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Tests for <see cref="CopilotController"/>.
/// </summary>
[TestClass]
public class CopilotControllerTests
{
    private Mock<IHttpClientFactory> httpClientFactoryMock = null!;
    private Mock<ICopilotProxyOptionsService> optionsServiceMock = null!;
    private Mock<ILogger<CopilotController>> loggerMock = null!;
    private CopilotController controller = null!;

    [TestInitialize]
    public void Setup()
    {
        httpClientFactoryMock = new Mock<IHttpClientFactory>();
        optionsServiceMock = new Mock<ICopilotProxyOptionsService>();
        loggerMock = new Mock<ILogger<CopilotController>>();

        controller = new CopilotController(
            httpClientFactoryMock.Object,
            optionsServiceMock.Object,
            loggerMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
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
        var request = new CopilotController.CopilotCompletionRequest
        {
            Prefix = string.Empty,
        };

        var result = await controller.Complete(request);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        var payload = ok.Value as CopilotController.CopilotCompletionResponse;
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

        var request = new CopilotController.CopilotCompletionRequest
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

        var request = new CopilotController.CopilotCompletionRequest
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

        var request = new CopilotController.CopilotCompletionRequest
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

        var request = new CopilotController.CopilotCompletionRequest
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

        var requestModel = new CopilotController.CopilotCompletionRequest
        {
            Prefix = "if (isValid)\n{\n    ",
            Language = "csharp",
        };

        var result = await controller.Complete(requestModel);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);

        var payload = ok.Value as CopilotController.CopilotCompletionResponse;
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

        var request = new CopilotController.CopilotCompletionRequest
        {
            Prefix = "public class Demo",
        };

        var result = await controller.Complete(request);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        var payload = ok.Value as CopilotController.CopilotCompletionResponse;
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

        var request = new CopilotController.CopilotCompletionRequest
        {
            Prefix = "public class Demo",
        };

        var result = await controller.Complete(request);

        var objectResult = result as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send)
    {
        var handler = new DelegateHttpMessageHandler(send);
        return new HttpClient(handler);
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
