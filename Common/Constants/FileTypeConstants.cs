// <copyright file="FileTypeConstants.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Common.Constants;

/// <summary>
/// Constants for file types and extensions used throughout Sky CMS.
/// </summary>
public static class FileTypeConstants
{
    /// <summary>
    /// Default comma-separated list of allowed file type extensions for upload.
    /// </summary>
    /// <remarks>
    /// This list includes:
    /// - Scripts: .js
    /// - Styles: .css
    /// - Markup: .htm, .html
    /// - Video: .mov, .webm, .avi, .mp4, .mpeg, .ts
    /// - Vector Graphics: .svg
    /// - Data: .json.
    /// </remarks>
    public const string DefaultAllowedFileTypes = ".js,.css,.htm,.html,.mov,.webm,.avi,.mp4,.mpeg,.ts,.svg,.json";

    /// <summary>
    /// JavaScript file extension.
    /// </summary>
    public const string JavaScript = ".js";

    /// <summary>
    /// CSS file extension.
    /// </summary>
    public const string Css = ".css";

    /// <summary>
    /// HTML file extension.
    /// </summary>
    public const string Html = ".html";

    /// <summary>
    /// HTM file extension.
    /// </summary>
    public const string Htm = ".htm";

    /// <summary>
    /// SVG file extension.
    /// </summary>
    public const string Svg = ".svg";

    /// <summary>
    /// JSON file extension.
    /// </summary>
    public const string Json = ".json";

    /// <summary>
    /// MP4 video file extension.
    /// </summary>
    public const string Mp4 = ".mp4";

    /// <summary>
    /// WebM video file extension.
    /// </summary>
    public const string WebM = ".webm";

    /// <summary>
    /// MOV video file extension.
    /// </summary>
    public const string Mov = ".mov";

    /// <summary>
    /// AVI video file extension.
    /// </summary>
    public const string Avi = ".avi";

    /// <summary>
    /// MPEG video file extension.
    /// </summary>
    public const string Mpeg = ".mpeg";

    /// <summary>
    /// TS video file extension.
    /// </summary>
    public const string Ts = ".ts";
}
