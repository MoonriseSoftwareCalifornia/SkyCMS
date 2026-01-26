// <copyright file="ContactApiConfigTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sky.Cms.Api.Shared.Models;

/// <summary>
/// Unit tests for ContactApiConfig.
/// </summary>
[TestClass]
public class ContactApiConfigTests
{
    [TestMethod]
    public void Constructor_ShouldDisableCaptcha_WhenJsonIsNull()
    {
        // Arrange & Act
        var config = new ContactApiConfig(null);

        // Assert
        Assert.IsFalse(config.RequireCaptcha);
        Assert.IsNull(config.CaptchaProvider);
        Assert.IsNull(config.CaptchaSiteKey);
        Assert.IsNull(config.CaptchaSecretKey);
    }

    [TestMethod]
    public void Constructor_ShouldDisableCaptcha_WhenJsonIsEmpty()
    {
        // Arrange & Act
        var config = new ContactApiConfig(string.Empty);

        // Assert
        Assert.IsFalse(config.RequireCaptcha);
    }

    [TestMethod]
    public void Constructor_ShouldParseTurnstileConfig_WhenValidJson()
    {
        // Arrange
        var json = "{\"Provider\":\"turnstile\",\"SiteKey\":\"test-site-key\",\"SecretKey\":\"test-secret\",\"RequireCaptcha\":true}";

        // Act
        var config = new ContactApiConfig(json);

        // Assert
        Assert.IsTrue(config.RequireCaptcha);
        Assert.AreEqual("turnstile", config.CaptchaProvider);
        Assert.AreEqual("test-site-key", config.CaptchaSiteKey);
        Assert.AreEqual("test-secret", config.CaptchaSecretKey);
    }

    [TestMethod]
    public void Constructor_ShouldParseReCaptchaConfig_WhenValidJson()
    {
        // Arrange
        var json = "{\"Provider\":\"recaptcha\",\"SiteKey\":\"recaptcha-site-key\",\"SecretKey\":\"recaptcha-secret\",\"RequireCaptcha\":true}";

        // Act
        var config = new ContactApiConfig(json);

        // Assert
        Assert.IsTrue(config.RequireCaptcha);
        Assert.AreEqual("recaptcha", config.CaptchaProvider);
        Assert.AreEqual("recaptcha-site-key", config.CaptchaSiteKey);
        Assert.AreEqual("recaptcha-secret", config.CaptchaSecretKey);
    }

    [TestMethod]
    public void Constructor_ShouldDisableCaptcha_WhenProviderMissing()
    {
        // Arrange
        var json = "{\"SiteKey\":\"test-key\",\"SecretKey\":\"test-secret\",\"RequireCaptcha\":true}";

        // Act
        var config = new ContactApiConfig(json);

        // Assert
        Assert.IsFalse(config.RequireCaptcha); // Should be false because provider is missing
    }

    [TestMethod]
    public void Constructor_ShouldHandleInvalidJson_Gracefully()
    {
        // Arrange
        var invalidJson = "{invalid json}";

        // Act
        var config = new ContactApiConfig(invalidJson);

        // Assert
        Assert.IsFalse(config.RequireCaptcha);
        Assert.IsNull(config.CaptchaProvider);
    }

    [TestMethod]
    public void FromDatabaseSettings_ShouldCreateConfig_WithAllSettings()
    {
        // Arrange
        var captchaJson = "{\"Provider\":\"turnstile\",\"SiteKey\":\"key\",\"SecretKey\":\"secret\",\"RequireCaptcha\":true}";

        // Act
        var config = ContactApiConfig.FromDatabaseSettings(
            "admin@test.com",
            3000,
            captchaJson);

        // Assert
        Assert.AreEqual("admin@test.com", config.AdminEmail);
        Assert.AreEqual(3000, config.MaxMessageLength);
        Assert.IsTrue(config.RequireCaptcha);
        Assert.AreEqual("turnstile", config.CaptchaProvider);
    }

    [TestMethod]
    public void FromDatabaseSettings_ShouldUseDefaults_WhenCaptchaJsonNull()
    {
        // Act
        var config = ContactApiConfig.FromDatabaseSettings(
            "admin@test.com",
            5000,
            null);

        // Assert
        Assert.AreEqual("admin@test.com", config.AdminEmail);
        Assert.AreEqual(5000, config.MaxMessageLength);
        Assert.IsFalse(config.RequireCaptcha);
    }

    [TestMethod]
    public void DefaultConstructor_ShouldSetDefaults()
    {
        // Act
        var config = new ContactApiConfig();

        // Assert
        Assert.AreEqual(string.Empty, config.AdminEmail);
        Assert.AreEqual(5000, config.MaxMessageLength);
        Assert.IsFalse(config.RequireCaptcha);
    }
}