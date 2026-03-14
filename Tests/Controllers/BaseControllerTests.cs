using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Cosmos.DynamicConfig;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sky.Cms.Controllers;
using System.Security.Claims;

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
            // Add a user to the in-memory database so UserManager can find it
            var testUser = new IdentityUser { Id = TestUserId.ToString(), UserName = "testuser" };
            Db.Users.Add(testUser);
            Db.SaveChanges();
            _controller = new TestableBaseController(Db, UserManager, Mediator, Cache, DynamicConfigurationProvider);
            // Set up a valid ClaimsPrincipal for the controller
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()) };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext();
            _controller.ControllerContext.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            _controller.ControllerContext.HttpContext.User = principal;
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
            var layoutViewModel = await Mediator.QueryAsync(new Cosmos.Common.Features.Layouts.Queries.GetDefaultLayoutQuery());
            var layout = await Db.Layouts.FirstOrDefaultAsync(l => l.Id == layoutViewModel.Id);
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
                IMediator mediator,
                IMemoryCache memoryCache = null,
                IDynamicConfigurationProvider configProvider = null)
                : base(dbContext, userManager, mediator, memoryCache, configProvider)
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