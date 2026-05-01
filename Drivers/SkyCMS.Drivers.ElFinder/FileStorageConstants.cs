// <copyright file="FileStorageConstants.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace SkyCMS.Drivers.ElFinder;

/// <summary>
/// Shared file-type extension lists used by both the legacy FileManager and the elFinder connector.
/// </summary>
public static class FileStorageConstants
{
    /// <summary>
    /// File extensions that are safe to open and edit as text in the code editor.
    /// </summary>
    public static readonly string[] ValidEditorExtensions =
        new[] { ".js", ".css", ".html", ".htm", ".json", ".xml", ".txt" };

    /// <summary>
    /// File extensions recognised as images (used for thumbnail generation and filtering).
    /// </summary>
    public static readonly string[] ValidImageExtensions =
        new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp", ".ico" };

    /// <summary>
    /// File extensions that are blocked from upload for security reasons.
    /// </summary>
    public static readonly string[] DangerousFileExtensions = new[]
    {
        ".exe", ".dll", ".bat", ".cmd", ".sh", ".ps1", ".psm1", ".psd1",
        ".vbs", ".vbe", ".jse", ".wsf", ".wsh", ".msi", ".msp",
        ".scr", ".hta", ".cpl", ".msc", ".jar", ".app", ".deb", ".rpm",
        ".dmg", ".pkg", ".run", ".bin", ".com", ".gadget", ".application",
        ".pif", ".lnk", ".inf", ".reg",
    };
}
