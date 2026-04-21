// <copyright file="AiUserPreferenceServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

#nullable enable

namespace Sky.Tests.Editor.Services.Copilot;

using System;
using System.Security.Claims;
using System.Text.Json;
using Cosmos.Common.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Sky.Editor.Services.Copilot;

[TestClass]
public class AiUserPreferenceServiceTests
{
    private ApplicationDbContext dbContext = null!;
    private AiUserPreferenceService service = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"AiUserPreferenceServiceTests_{Guid.NewGuid()}")
            .Options;

        dbContext = new ApplicationDbContext(options);
        service = new AiUserPreferenceService(dbContext, Mock.Of<ILogger<AiUserPreferenceService>>());
    }

    [TestCleanup]
    public void Cleanup()
    {
        dbContext.Dispose();
    }

    [TestMethod]
    public async Task SaveSelectedModelAsync_PersistsAndLoadsModelPreference()
    {
        var user = CreateUser("user-123", "editor@example.com");

        await service.SaveSelectedModelAsync(user, "openai", "monaco", "article", "gpt-4.1");
        var selectedModel = await service.GetSelectedModelAsync(user, "openai", "monaco", "article");

        Assert.AreEqual("gpt-4.1", selectedModel);
        var setting = await dbContext.Settings.FirstOrDefaultAsync(s => s.Group == AiUserPreferenceService.GroupName);
        Assert.IsNotNull(setting);
        Assert.IsTrue(setting!.Name.StartsWith("v1:model:", StringComparison.Ordinal));
        Assert.IsTrue(setting.Name.Contains("openai", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task SaveSelectedModelAsync_WithBlankModel_RemovesExistingPreference()
    {
        var user = CreateUser("user-123", "editor@example.com");

        await service.SaveSelectedModelAsync(user, "openai", "monaco", "article", "gpt-4.1");
        await service.SaveSelectedModelAsync(user, "openai", "monaco", "article", null);

        var selectedModel = await service.GetSelectedModelAsync(user, "openai", "monaco", "article");
        var settingsCount = await dbContext.Settings.CountAsync(s => s.Group == AiUserPreferenceService.GroupName);

        Assert.IsNull(selectedModel);
        Assert.AreEqual(0, settingsCount);
    }

    [TestMethod]
    public async Task GetSelectedModelAsync_UsesContextSpecificSettingName()
    {
        var user = CreateUser("user-123", "editor@example.com");

        await service.SaveSelectedModelAsync(user, "openai", "monaco", "article", "gpt-4.1");
        await service.SaveSelectedModelAsync(user, "openai", "ckeditor", "blog", "gpt-4o-mini");

        var monacoModel = await service.GetSelectedModelAsync(user, "openai", "monaco", "article");
        var ckeditorModel = await service.GetSelectedModelAsync(user, "openai", "ckeditor", "blog");

        Assert.AreEqual("gpt-4.1", monacoModel);
        Assert.AreEqual("gpt-4o-mini", ckeditorModel);
    }

    [TestMethod]
    public async Task GetSelectedModelAsync_IgnoresLegacySettingNames()
    {
        var user = CreateUser("user-123", "editor@example.com");

        dbContext.Settings.Add(new Setting
        {
            Group = AiUserPreferenceService.GroupName,
            Name = "model-selection:openai:monaco:article:user-123",
            Value = JsonSerializer.Serialize(new
            {
                Version = 1,
                UserKey = "user-123",
                ProviderKey = "openai",
                EditorKind = "monaco",
                DocumentKind = "article",
                SelectedModel = "legacy-model",
                UpdatedUtc = DateTime.UtcNow,
            }),
        });
        await dbContext.SaveChangesAsync();

        var selectedModel = await service.GetSelectedModelAsync(user, "openai", "monaco", "article");

        Assert.IsNull(selectedModel);
    }

    private static ClaimsPrincipal CreateUser(string userId, string userName)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userName),
        ],
        authenticationType: "Test"));
    }
}