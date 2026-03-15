using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Sky.Editor.Services.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Sky.Editor.Services.Layouts
{
    /// <summary>
    /// Provides functionality for retrieving and managing layout page templates used in the application.
    /// </summary>
    /// <remarks>This service supplies layout templates that can be used for rendering or previewing various
    /// page designs. It is typically used to obtain available templates for display or selection in user interfaces.
    /// Thread safety depends on the implementation of the underlying dependencies.</remarks>
    public class LayoutTemplateService : ILayoutTemplateService
    {
        private IWebHostEnvironment environment;
        private ILogger<LayoutTemplateService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LayoutTemplateService"/> class.
        /// </summary>
        /// <param name="environment">Hosting environment.</param>
        /// <param name="logger">Log service.</param>
        public LayoutTemplateService(IWebHostEnvironment environment, ILogger<LayoutTemplateService> logger)
        {
            this.environment = environment;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<PageTemplate>> GetAllTemplatesAsync()
        {
            var model = new List<PageTemplate>
            {
                new PageTemplate
                {
                    Key = "html-components",
                    Name = "HTML Components Preview",
                    Description = "This HTML file serves as a comprehensive specimen page to demonstrate the styling of various HTML elements.",
                    Category = "Preview",
                    FilePath = "PreviewTemplates/HtmlComponents.html",
                    ThumbnailPath = "/images/templates/blog-stream-thumb.png",
                    Tags = new List<string> { "blog", "article", "post", "content" },
                    Content = await LoadTemplateContentAsync("PreviewTemplates/HtmlComponents.html"),
                }
            };

            return model;
        }

        private async Task<string> LoadTemplateContentAsync(string filePath)
        {
            try
            {
                // Try embedded resource first
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"{assembly.GetName().Name}.Templates.{filePath.Replace('/', '.').Replace('\\', '.')}";

                await using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    return await reader.ReadToEndAsync();
                }

                // Fallback to file system
                var physicalPath = Path.Combine(environment.ContentRootPath, "Templates", filePath);
                if (File.Exists(physicalPath))
                {
                    return await File.ReadAllTextAsync(physicalPath);
                }

                logger.LogWarning("Template file not found: {FilePath}", filePath);
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading template: {FilePath}", filePath);
                return null;
            }
        }
    }
}
