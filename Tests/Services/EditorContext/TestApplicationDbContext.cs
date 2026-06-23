using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Cosmos.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Sky.Tests.Services.EditorContext;

/// <summary>
/// In-memory test context with a tiny Setup/Returns shim so existing tests can stay concise.
/// </summary>
public class TestApplicationDbContext : ApplicationDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestApplicationDbContext"/> class.
    /// </summary>
    public TestApplicationDbContext()
        : base(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"EditorContextTests_{Guid.NewGuid()}")
            .Options)
    {
    }

    /// <summary>
    /// Mimics Moq's Setup(...).Returns(...) style for DbSet properties used by these tests.
    /// </summary>
    public SetupBuilder<T> Setup<T>(Expression<Func<TestApplicationDbContext, DbSet<T>>> selector)
        where T : class
    {
        if (selector.Body is not MemberExpression memberExpression)
        {
            throw new ArgumentException("Selector must target a DbSet property.", nameof(selector));
        }

        return new SetupBuilder<T>(this, memberExpression.Member.Name);
    }

    /// <summary>
    /// Builder used by Setup(...).Returns(...).
    /// </summary>
    public sealed class SetupBuilder<T>
        where T : class
    {
        private readonly TestApplicationDbContext _context;
        private readonly string _propertyName;

        public SetupBuilder(TestApplicationDbContext context, string propertyName)
        {
            _context = context;
            _propertyName = propertyName;
        }

        public void Returns(DbSet<T> dbSet)
        {
            if (_propertyName == nameof(TestApplicationDbContext.Articles) && dbSet is DbSet<Article> articles)
            {
                _context.Articles = articles;
                return;
            }

            if (_propertyName == nameof(TestApplicationDbContext.Layouts) && dbSet is DbSet<Layout> layouts)
            {
                _context.Layouts = layouts;
                return;
            }

            if (_propertyName == nameof(TestApplicationDbContext.Templates) && dbSet is DbSet<Template> templates)
            {
                _context.Templates = templates;
                return;
            }

            if (_propertyName == nameof(TestApplicationDbContext.PageDesignVersions) && dbSet is DbSet<PageDesignVersion> versions)
            {
                _context.PageDesignVersions = versions;
                return;
            }

            throw new NotSupportedException($"Unsupported DbSet setup for property '{_propertyName}'.");
        }
    }
}
