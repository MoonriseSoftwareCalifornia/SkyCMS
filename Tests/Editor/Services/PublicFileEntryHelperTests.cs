// <copyright file="PublicFileEntryHelperTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Editor.Services
{
    using System;
    using System.Collections.Generic;
    using Cosmos.BlobService;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Services;

    /// <summary>
    /// Tests for <see cref="PublicFileEntryHelper"/> shared file-entry utility methods.
    /// </summary>
    [TestClass]
    public class PublicFileEntryHelperTests
    {
        // ===== NormalizePath Tests =====

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void NormalizePath_EmptyString_ReturnsRoot()
        {
            var result = PublicFileEntryHelper.NormalizePath(string.Empty);
            Assert.AreEqual("/", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void NormalizePath_WhitespaceOnly_ReturnsRoot()
        {
            var result = PublicFileEntryHelper.NormalizePath("   ");
            Assert.AreEqual("/", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void NormalizePath_NullString_ReturnsRoot()
        {
            var result = PublicFileEntryHelper.NormalizePath(null!);
            Assert.AreEqual("/", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void NormalizePath_RootOnly_ReturnsRoot()
        {
            var result = PublicFileEntryHelper.NormalizePath("/");
            Assert.AreEqual("/", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void NormalizePath_BackslashesToForwardSlashes()
        {
            var result = PublicFileEntryHelper.NormalizePath("\\pub\\articles\\123");
            Assert.AreEqual("/pub/articles/123", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void NormalizePath_AddsLeadingSlash()
        {
            var result = PublicFileEntryHelper.NormalizePath("pub/articles/123");
            Assert.AreEqual("/pub/articles/123", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void NormalizePath_RemovesTrailingSlash()
        {
            var result = PublicFileEntryHelper.NormalizePath("/pub/articles/123/");
            Assert.AreEqual("/pub/articles/123", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void NormalizePath_RemovesDoubleSlashes()
        {
            var result = PublicFileEntryHelper.NormalizePath("/pub//articles///123");
            Assert.AreEqual("/pub/articles/123", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void NormalizePath_RemovesTrailingSlashPreservesRoot()
        {
            var result = PublicFileEntryHelper.NormalizePath("///");
            Assert.AreEqual("/", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void NormalizePath_ComplexPath()
        {
            var result = PublicFileEntryHelper.NormalizePath("\\\\pub\\\\articles\\/123\\/");
            Assert.AreEqual("/pub/articles/123", result);
        }

        // ===== IsPathWithinRoot Tests =====

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void IsPathWithinRoot_ExactRootMatch()
        {
            var result = PublicFileEntryHelper.IsPathWithinRoot("/pub", "/pub");
            Assert.IsTrue(result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void IsPathWithinRoot_ChildPath()
        {
            var result = PublicFileEntryHelper.IsPathWithinRoot("/pub/articles/123", "/pub");
            Assert.IsTrue(result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void IsPathWithinRoot_NotWithinRoot()
        {
            var result = PublicFileEntryHelper.IsPathWithinRoot("/private/articles/123", "/pub");
            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void IsPathWithinRoot_ParentTraversalAttempt()
        {
            var result = PublicFileEntryHelper.IsPathWithinRoot("/pub/../private", "/pub");
            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void IsPathWithinRoot_DoubleDotInMiddle()
        {
            var result = PublicFileEntryHelper.IsPathWithinRoot("/pub/articles/../templates", "/pub");
            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void IsPathWithinRoot_EmptyPath()
        {
            var result = PublicFileEntryHelper.IsPathWithinRoot(string.Empty, "/pub");
            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void IsPathWithinRoot_EmptyRoot()
        {
            var result = PublicFileEntryHelper.IsPathWithinRoot("/pub/articles/123", string.Empty);
            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void IsPathWithinRoot_CaseInsensitive()
        {
            var result = PublicFileEntryHelper.IsPathWithinRoot("/PUB/Articles/123", "/pub");
            Assert.IsTrue(result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void IsPathWithinRoot_WithTrailingSlash()
        {
            var result = PublicFileEntryHelper.IsPathWithinRoot("/pub/articles/123/", "/pub");
            Assert.IsTrue(result);
        }

        // ===== TryGetArticleNumber Tests =====

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void TryGetArticleNumber_FromEntry_ValidNumber()
        {
            var entry = new FileManagerEntry { Name = "123", IsDirectory = true };
            var result = PublicFileEntryHelper.TryGetArticleNumber(entry, out var articleNumber);
            Assert.IsTrue(result);
            Assert.AreEqual(123, articleNumber);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void TryGetArticleNumber_FromEntry_NotADirectory()
        {
            var entry = new FileManagerEntry { Name = "123", IsDirectory = false };
            var result = PublicFileEntryHelper.TryGetArticleNumber(entry, out var articleNumber);
            Assert.IsFalse(result);
            Assert.AreEqual(0, articleNumber);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void TryGetArticleNumber_FromEntry_NonNumeric()
        {
            var entry = new FileManagerEntry { Name = "articles", IsDirectory = true };
            var result = PublicFileEntryHelper.TryGetArticleNumber(entry, out var articleNumber);
            Assert.IsFalse(result);
            Assert.AreEqual(0, articleNumber);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void TryGetArticleNumber_FromPath_ValidNumber()
        {
            var result = PublicFileEntryHelper.TryGetArticleNumber("/pub/articles/456", out var articleNumber);
            Assert.IsTrue(result);
            Assert.AreEqual(456, articleNumber);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void TryGetArticleNumber_FromPath_NonNumeric()
        {
            var result = PublicFileEntryHelper.TryGetArticleNumber("/pub/articles/notanumber", out var articleNumber);
            Assert.IsFalse(result);
            Assert.AreEqual(0, articleNumber);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void TryGetArticleNumber_FromPath_Root()
        {
            var result = PublicFileEntryHelper.TryGetArticleNumber("/", out var articleNumber);
            Assert.IsFalse(result);
            Assert.AreEqual(0, articleNumber);
        }

        // ===== TryGetTemplateId Tests =====

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void TryGetTemplateId_FromEntry_ValidGuid()
        {
            var guidValue = Guid.NewGuid();
            var entry = new FileManagerEntry { Name = guidValue.ToString(), IsDirectory = true };
            var result = PublicFileEntryHelper.TryGetTemplateId(entry, out var templateId);
            Assert.IsTrue(result);
            Assert.AreEqual(guidValue, templateId);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void TryGetTemplateId_FromEntry_NotADirectory()
        {
            var guidValue = Guid.NewGuid().ToString();
            var entry = new FileManagerEntry { Name = guidValue, IsDirectory = false };
            var result = PublicFileEntryHelper.TryGetTemplateId(entry, out var templateId);
            Assert.IsFalse(result);
            Assert.AreEqual(Guid.Empty, templateId);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void TryGetTemplateId_FromEntry_InvalidGuid()
        {
            var entry = new FileManagerEntry { Name = "not-a-guid", IsDirectory = true };
            var result = PublicFileEntryHelper.TryGetTemplateId(entry, out var templateId);
            Assert.IsFalse(result);
            Assert.AreEqual(Guid.Empty, templateId);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void TryGetTemplateId_FromPath_ValidGuid()
        {
            var guidValue = Guid.NewGuid();
            var result = PublicFileEntryHelper.TryGetTemplateId($"/pub/templates/{guidValue}", out var templateId);
            Assert.IsTrue(result);
            Assert.AreEqual(guidValue, templateId);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void TryGetTemplateId_FromPath_InvalidGuid()
        {
            var result = PublicFileEntryHelper.TryGetTemplateId("/pub/templates/not-a-guid", out var templateId);
            Assert.IsFalse(result);
            Assert.AreEqual(Guid.Empty, templateId);
        }

        // ===== GetDisplayName Tests =====

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void GetDisplayName_Directory()
        {
            var entry = new FileManagerEntry { Name = "articles", IsDirectory = true };
            var result = PublicFileEntryHelper.GetDisplayName(entry);
            Assert.AreEqual("articles", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void GetDisplayName_FileWithExtension()
        {
            var entry = new FileManagerEntry { Name = "document", Extension = "docx", IsDirectory = false };
            var result = PublicFileEntryHelper.GetDisplayName(entry);
            Assert.AreEqual("document.docx", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void GetDisplayName_FileWithExtensionWithDot()
        {
            var entry = new FileManagerEntry { Name = "document", Extension = ".pdf", IsDirectory = false };
            var result = PublicFileEntryHelper.GetDisplayName(entry);
            Assert.AreEqual("document.pdf", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void GetDisplayName_FileAlreadyHasExtension()
        {
            var entry = new FileManagerEntry { Name = "document.txt", Extension = "txt", IsDirectory = false };
            var result = PublicFileEntryHelper.GetDisplayName(entry);
            Assert.AreEqual("document.txt", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void GetDisplayName_FileNoExtension()
        {
            var entry = new FileManagerEntry { Name = "README", Extension = null, IsDirectory = false };
            var result = PublicFileEntryHelper.GetDisplayName(entry);
            Assert.AreEqual("README", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void GetDisplayName_EmptyName()
        {
            var entry = new FileManagerEntry { Name = null, IsDirectory = false };
            var result = PublicFileEntryHelper.GetDisplayName(entry);
            Assert.AreEqual(string.Empty, result);
        }

        // ===== GetEntryMimeType Tests =====

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void GetEntryMimeType_Directory()
        {
            var entry = new FileManagerEntry { Name = "articles", IsDirectory = true };
            var result = PublicFileEntryHelper.GetEntryMimeType(entry);
            Assert.AreEqual("directory", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void GetEntryMimeType_TextFile()
        {
            var entry = new FileManagerEntry { Name = "document", Extension = "txt", IsDirectory = false };
            var result = PublicFileEntryHelper.GetEntryMimeType(entry);
            Assert.AreEqual("text/plain", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void GetEntryMimeType_PdfFile()
        {
            var entry = new FileManagerEntry { Name = "document", Extension = "pdf", IsDirectory = false };
            var result = PublicFileEntryHelper.GetEntryMimeType(entry);
            Assert.AreEqual("application/pdf", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void GetEntryMimeType_JsonFile()
        {
            var entry = new FileManagerEntry { Name = "data", Extension = "json", IsDirectory = false };
            var result = PublicFileEntryHelper.GetEntryMimeType(entry);
            Assert.AreEqual("application/json", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void GetEntryMimeType_UnknownExtension()
        {
            var entry = new FileManagerEntry { Name = "file", Extension = "xyz", IsDirectory = false };
            var result = PublicFileEntryHelper.GetEntryMimeType(entry);
            Assert.IsNotNull(result);
            Assert.IsFalse(string.IsNullOrEmpty(result));
        }

        // ===== ResolveEntryPath Tests =====

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ResolveEntryPath_WithEntryPath()
        {
            var entry = new FileManagerEntry { Path = "/pub/articles/123/document.txt" };
            var result = PublicFileEntryHelper.ResolveEntryPath("/pub/articles/123", entry);
            Assert.AreEqual("/pub/articles/123/document.txt", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ResolveEntryPath_WithoutEntryPath()
        {
            var entry = new FileManagerEntry { Name = "document.txt", Path = null };
            var result = PublicFileEntryHelper.ResolveEntryPath("/pub/articles/123", entry);
            Assert.AreEqual("/pub/articles/123/document.txt", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ResolveEntryPath_WithoutEntryPathNormalizesParent()
        {
            var entry = new FileManagerEntry { Name = "document.txt", Path = null };
            var result = PublicFileEntryHelper.ResolveEntryPath("/pub/articles/123/", entry);
            Assert.AreEqual("/pub/articles/123/document.txt", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ResolveEntryPath_WithoutName()
        {
            var entry = new FileManagerEntry { Name = null, Path = null };
            var result = PublicFileEntryHelper.ResolveEntryPath("/pub/articles/123", entry);
            Assert.AreEqual("/pub/articles/123/untitled", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ResolveEntryPath_NestedPath()
        {
            var entry = new FileManagerEntry { Path = "/pub/templates/abc123/layout.html" };
            var result = PublicFileEntryHelper.ResolveEntryPath("/pub/templates/abc123", entry);
            Assert.AreEqual("/pub/templates/abc123/layout.html", result);
        }

        // ===== ResolveFriendlyDisplayName Tests =====

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ResolveFriendlyDisplayName_ArticleFolder_WithTitle()
        {
            var entry = new FileManagerEntry { Name = "123", IsDirectory = true };
            var articleTitles = new Dictionary<int, string> { { 123, "My Article" } };
            var templateTitles = new Dictionary<Guid, string>();

            var result = PublicFileEntryHelper.ResolveFriendlyDisplayName(
                "/pub/articles", entry, articleTitles, templateTitles);

            Assert.AreEqual("My Article", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ResolveFriendlyDisplayName_ArticleFolder_NoTitle()
        {
            var entry = new FileManagerEntry { Name = "456", IsDirectory = true };
            var articleTitles = new Dictionary<int, string>();
            var templateTitles = new Dictionary<Guid, string>();

            var result = PublicFileEntryHelper.ResolveFriendlyDisplayName(
                "/pub/articles", entry, articleTitles, templateTitles);

            Assert.AreEqual("456", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ResolveFriendlyDisplayName_TemplateFolder_WithTitle()
        {
            var templateId = Guid.NewGuid();
            var entry = new FileManagerEntry { Name = templateId.ToString(), IsDirectory = true };
            var articleTitles = new Dictionary<int, string>();
            var templateTitles = new Dictionary<Guid, string> { { templateId, "Default Layout" } };

            var result = PublicFileEntryHelper.ResolveFriendlyDisplayName(
                "/pub/templates", entry, articleTitles, templateTitles);

            Assert.AreEqual("Default Layout", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ResolveFriendlyDisplayName_TemplateFolder_NoTitle()
        {
            var templateId = Guid.NewGuid();
            var entry = new FileManagerEntry { Name = templateId.ToString(), IsDirectory = true };
            var articleTitles = new Dictionary<int, string>();
            var templateTitles = new Dictionary<Guid, string>();

            var result = PublicFileEntryHelper.ResolveFriendlyDisplayName(
                "/pub/templates", entry, articleTitles, templateTitles);

            Assert.AreEqual(templateId.ToString(), result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ResolveFriendlyDisplayName_NotArticleOrTemplateFolder()
        {
            var entry = new FileManagerEntry { Name = "document.txt", IsDirectory = false };
            var articleTitles = new Dictionary<int, string>();
            var templateTitles = new Dictionary<Guid, string>();

            var result = PublicFileEntryHelper.ResolveFriendlyDisplayName(
                "/pub/articles", entry, articleTitles, templateTitles);

            Assert.AreEqual("document.txt", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ResolveFriendlyDisplayName_OtherParentPath()
        {
            var entry = new FileManagerEntry { Name = "123", IsDirectory = true };
            var articleTitles = new Dictionary<int, string> { { 123, "My Article" } };
            var templateTitles = new Dictionary<Guid, string>();

            var result = PublicFileEntryHelper.ResolveFriendlyDisplayName(
                "/pub/other", entry, articleTitles, templateTitles);

            // Should not mask because parent path is not /pub/articles
            Assert.AreEqual("123", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ResolveFriendlyDisplayName_CaseInsensitiveParentPath()
        {
            var entry = new FileManagerEntry { Name = "123", IsDirectory = true };
            var articleTitles = new Dictionary<int, string> { { 123, "My Article" } };
            var templateTitles = new Dictionary<Guid, string>();

            var result = PublicFileEntryHelper.ResolveFriendlyDisplayName(
                "/PUB/ARTICLES", entry, articleTitles, templateTitles);

            Assert.AreEqual("My Article", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ResolveFriendlyDisplayName_EmptyTitleInDictionary()
        {
            var entry = new FileManagerEntry { Name = "123", IsDirectory = true };
            var articleTitles = new Dictionary<int, string> { { 123, string.Empty } };
            var templateTitles = new Dictionary<Guid, string>();

            var result = PublicFileEntryHelper.ResolveFriendlyDisplayName(
                "/pub/articles", entry, articleTitles, templateTitles);

            // Should fall back to name because title is empty
            Assert.AreEqual("123", result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ResolveFriendlyDisplayName_NullTitleInDictionary()
        {
            var entry = new FileManagerEntry { Name = "123", IsDirectory = true };
            var articleTitles = new Dictionary<int, string> { { 123, null! } };
            var templateTitles = new Dictionary<Guid, string>();

            var result = PublicFileEntryHelper.ResolveFriendlyDisplayName(
                "/pub/articles", entry, articleTitles, templateTitles);

                    // Should fall back to name because title is null
                        Assert.AreEqual("123", result);
                    }

                    // ===== IsUploadPathSafe Tests =====

                    [TestMethod]
                    [TestCategory("PublicFileEntryHelper")]
                    public void IsUploadPathSafe_PubRoot_ReturnsTrue()
                    {
                        Assert.IsTrue(PublicFileEntryHelper.IsUploadPathSafe("/pub"));
                    }

                    [TestMethod]
                    [TestCategory("PublicFileEntryHelper")]
                    public void IsUploadPathSafe_PubSubfolder_ReturnsTrue()
                    {
                        Assert.IsTrue(PublicFileEntryHelper.IsUploadPathSafe("/pub/articles/123"));
                    }

                    [TestMethod]
                    [TestCategory("PublicFileEntryHelper")]
                    public void IsUploadPathSafe_EmptyPath_ReturnsFalse()
                    {
                        Assert.IsFalse(PublicFileEntryHelper.IsUploadPathSafe(string.Empty));
                    }

                    [TestMethod]
                    [TestCategory("PublicFileEntryHelper")]
                    public void IsUploadPathSafe_NullPath_ReturnsFalse()
                    {
                        Assert.IsFalse(PublicFileEntryHelper.IsUploadPathSafe(null));
                    }

                    [TestMethod]
                    [TestCategory("PublicFileEntryHelper")]
                    public void IsUploadPathSafe_TraversalSequence_ReturnsFalse()
                    {
                        Assert.IsFalse(PublicFileEntryHelper.IsUploadPathSafe("/pub/../private"));
                    }

                    [TestMethod]
                    [TestCategory("PublicFileEntryHelper")]
                    public void IsUploadPathSafe_OutsidePub_ReturnsFalse()
                    {
                        Assert.IsFalse(PublicFileEntryHelper.IsUploadPathSafe("/private/files"));
                    }

                    [TestMethod]
                    [TestCategory("PublicFileEntryHelper")]
                    public void IsUploadPathSafe_SlashesOnly_ReturnsFalse()
                    {
                        Assert.IsFalse(PublicFileEntryHelper.IsUploadPathSafe("///"));
                    }

                    // ===== IsDangerousExtension Tests =====

                    [TestMethod]
                    [TestCategory("PublicFileEntryHelper")]
                    public void IsDangerousExtension_ExeFile_ReturnsTrue()
                    {
                        Assert.IsTrue(PublicFileEntryHelper.IsDangerousExtension("malware.exe"));
                    }

                    [TestMethod]
                    [TestCategory("PublicFileEntryHelper")]
                    public void IsDangerousExtension_Ps1File_ReturnsTrue()
                    {
                        Assert.IsTrue(PublicFileEntryHelper.IsDangerousExtension("script.ps1"));
                    }

                    [TestMethod]
                    [TestCategory("PublicFileEntryHelper")]
                    public void IsDangerousExtension_JpegFile_ReturnsFalse()
                    {
                        Assert.IsFalse(PublicFileEntryHelper.IsDangerousExtension("photo.jpeg"));
                    }

                    [TestMethod]
                    [TestCategory("PublicFileEntryHelper")]
                    public void IsDangerousExtension_HtmlFile_ReturnsFalse()
                    {
                        Assert.IsFalse(PublicFileEntryHelper.IsDangerousExtension("page.html"));
                    }

                    [TestMethod]
                    [TestCategory("PublicFileEntryHelper")]
                    public void IsDangerousExtension_NullFileName_ReturnsFalse()
                    {
                        Assert.IsFalse(PublicFileEntryHelper.IsDangerousExtension(null));
                    }

                    [TestMethod]
                    [TestCategory("PublicFileEntryHelper")]
                    public void IsDangerousExtension_UpperCaseExtension_ReturnsTrue()
                    {
                        Assert.IsTrue(PublicFileEntryHelper.IsDangerousExtension("VIRUS.EXE"));
                    }
                }
            }
