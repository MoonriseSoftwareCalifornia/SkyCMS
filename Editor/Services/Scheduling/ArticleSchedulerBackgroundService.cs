// <copyright file="ArticleSchedulerBackgroundService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Scheduling
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Background service that executes the ArticleScheduler at regular intervals.
    /// Runs every 10 minutes to check for scheduled article publications.
    /// </summary>
    public class ArticleSchedulerBackgroundService : BackgroundService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<ArticleSchedulerBackgroundService> logger;
        private readonly TimeSpan interval = TimeSpan.FromMinutes(10);
        private readonly TimeSpan startupDelay = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Initializes a new instance of the <see cref="ArticleSchedulerBackgroundService"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider for creating scoped dependencies.</param>
        /// <param name="logger">Logger instance.</param>
        public ArticleSchedulerBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ArticleSchedulerBackgroundService> logger)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("✅ Article Scheduler Background Service starting");

            try
            {
                // Wait before first execution to ensure app is fully initialized
                await Task.Delay(startupDelay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                logger.LogInformation("Article Scheduler Background Service cancelled during startup delay");
                return;
            }

            logger.LogInformation("Article Scheduler Background Service ready - executing every {Interval}", interval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExecuteSchedulerAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error executing ArticleScheduler");
                }

                // Wait for the next interval
                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected when stopping
                    break;
                }
            }

            logger.LogInformation("Article Scheduler Background Service stopped");
        }

        /// <summary>
        /// Executes the article scheduler in a scoped context.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ExecuteSchedulerAsync(CancellationToken stoppingToken)
        {
            using var scope = serviceProvider.CreateScope();
            var scheduler = scope.ServiceProvider.GetRequiredService<IArticleScheduler>();

            await scheduler.ExecuteAsync();
        }
    }
}