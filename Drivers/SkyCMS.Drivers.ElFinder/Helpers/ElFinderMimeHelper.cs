// <copyright file="ElFinderMimeHelper.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace SkyCMS.Drivers.ElFinder.Helpers
{
    using System.IO;
    using MimeTypes;

    /// <summary>
    /// Single shared MIME-type resolver for all elFinder command handlers.
    /// Delegates to the MimeTypes NuGet package so all handlers are consistent.
    /// </summary>
    internal static class ElFinderMimeHelper
    {
        /// <summary>
        /// Returns the MIME type for the given file name based on its extension.
        /// Falls back to <c>application/octet-stream</c> for unknown extensions.
        /// </summary>
        /// <param name="fileName">File name (with or without path components).</param>
        /// <returns>MIME type string.</returns>
        public static string GetMimeType(string fileName)
        {
            try
            {
                var ext = Path.GetExtension(fileName ?? string.Empty);
                return MimeTypeMap.GetMimeType(ext);
            }
            catch
            {
                return "application/octet-stream";
            }
        }
    }
}
