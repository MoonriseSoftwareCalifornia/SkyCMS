// <copyright file="IElFinderNameResolver.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace SkyCMS.Drivers.ElFinder.Adapters
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Resolves a display name for a file-system entry given its full path.
    /// The default implementation returns the raw name unchanged; host applications
    /// can register a richer implementation (e.g. article-title lookup) via DI.
    /// </summary>
    public interface IElFinderNameResolver
    {
        /// <summary>
        /// Returns the display name to use for an entry.
        /// </summary>
        /// <param name="fullPath">Full normalised path of the entry (e.g. <c>/pub/articles/42/image.png</c>).</param>
        /// <param name="rawName">The raw storage name (last path segment).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Display name — may equal <paramref name="rawName"/> if no override applies.</returns>
        Task<string> ResolveNameAsync(string fullPath, string rawName, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Default no-op implementation: returns the raw name unchanged.
    /// Registered by <see cref="ElFinderServiceCollectionExtensions"/> with TryAdd
    /// so host applications can substitute a richer resolver.
    /// </summary>
    internal sealed class PassThroughNameResolver : IElFinderNameResolver
    {
        public Task<string> ResolveNameAsync(string fullPath, string rawName, CancellationToken cancellationToken = default)
            => Task.FromResult(rawName ?? string.Empty);
    }
}
