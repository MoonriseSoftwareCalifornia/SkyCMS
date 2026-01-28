using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cosmos.Common.Data;
using Cosmos.Common.Models;
using Cosmos.DynamicConfig;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Sky.Cms.Controllers;

namespace Sky.Tests.Controllers
{
    [TestClass]
    public class BaseControllerTests : SkyCmsTestBase
    {
        private TestableBaseController _controller;

        [TestInitialize]
        public void TestInitialize()
        {
            InitializeTestContext();
            _controller = new TestableBaseController(Db, UserManager, Cache, DynamicConfigurationProvider);
        }

        [TestMethod]
        public void BaseValidateHtml_ReturnsParsedText_WhenHtmlIsValid()
        {
            var html = "<div>Hello <b>World</b></div>";
            var result = _controller.CallBaseValidateHtml("test", html);
            Assert.IsTrue(result.Contains("Hello World"));
        }

        [TestMethod]
        public void BaseValidateHtml_ReturnsEmpty_WhenInputIsNullOrEmpty()
        {
            var result = _controller.CallBaseValidateHtml("test", null);
            Assert.AreEqual(string.Empty, result);

            result = _controller.CallBaseValidateHtml("test", "");
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public async Task BaseGetLayoutListItems_ReturnsLayouts()
        {
            // Arrange: Ensure at least one layout exists
            if (!Db.Layouts.Any())
            {
                Db.Layouts.Add(new Layout { Id = Guid.NewGuid(), LayoutName = "TestLayout", IsDefault = true, Published = DateTimeOffset.UtcNow });
                Db.SaveChanges();
            }

            // Act
            var items = await _controller.CallBaseGetLayoutListItems();

            // Assert
            Assert.IsTrue(items.Count > 0);
            Assert.IsTrue(items.Any(i => i.Text == "TestLayout" || i.Text == "Default"));
        }

        [TestMethod]
        public async Task GetUserId_ReturnsCurrentUserId()
        {
            var userId = await _controller.CallGetUserId();
            Assert.AreEqual(TestUserId.ToString(), userId);
        }

        [TestMethod]
        public async Task GetCurrentLayoutAsync_ReturnsDefaultLayout()
        {
            var layout = await _controller.CallGetCurrentLayoutAsync();
            Assert.IsNotNull(layout);
            Assert.IsTrue(layout.IsDefault);
        }

        [TestMethod]
        public async Task GetTemplatesForCurrentLayoutAsync_ReturnsTemplatesForLayout()
        {
            // Arrange: Add a template for the default layout
            var layout = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            var template = new Template
            {
                Id = Guid.NewGuid(),
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber,
                PageType = "test-page",
                Content = "<div>Test</div>"
            };
            Db.Templates.Add(template);
            Db.SaveChanges();

            // Act
            var templates = await _controller.CallGetTemplatesForCurrentLayoutAsync();

            // Assert
            Assert.IsTrue(templates.Any(t => t.Id == template.Id));
        }

        // Helper: Expose internal/protected methods for testing
        private class TestableBaseController : BaseController
        {
            public TestableBaseController(
                ApplicationDbContext dbContext,
                UserManager<IdentityUser> userManager,
                IMemoryCache memoryCache = null,
                IDynamicConfigurationProvider configProvider = null)
                : base(dbContext, userManager, memoryCache, configProvider)
            {
            }

            public string CallBaseValidateHtml(string fieldName, string inputHtml) =>
                base.BaseValidateHtml(fieldName, inputHtml);

            public Task<List<SelectListItem>> CallBaseGetLayoutListItems() =>
                base.BaseGetLayoutListItems();

            public Task<string> CallGetUserId() =>
                base.GetUserId();

            public Task<Layout> CallGetCurrentLayoutAsync() =>
                base.GetCurrentLayoutAsync();

            public Task<IQueryable<Template>> CallGetTemplatesForCurrentLayoutAsync() =>
                base.GetTemplatesForCurrentLayoutAsync();
        }
    }
}