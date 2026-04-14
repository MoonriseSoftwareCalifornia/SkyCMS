// <copyright file="IArticleScheduler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>
namespace Sky.Editor.Services.Scheduling
{
    using System.Threading.Tasks;

    /// <summary>
    /// Scheduled service that activates article versions with multiple published dates,
    /// ensuring only the most recent non-future version is actively published.
    /// </summary>
    public interface IArticleScheduler
    {
        /// <summary>
        /// Executes the scheduled job to process article versions with multiple published dates.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ExecuteAsync();
    }
}
