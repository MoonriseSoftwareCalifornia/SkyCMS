// <copyright file="RoleConstants.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Common.Constants;

/// <summary>
/// Constants for user roles used throughout Sky CMS.
/// </summary>
public static class RoleConstants
{
    /// <summary>
    /// Administrator role - full system access.
    /// </summary>
    public const string Administrator = "Administrators";

    /// <summary>
    /// Author role - can create and edit content.
    /// </summary>
    public const string Author = "Authors";

    /// <summary>
    /// Editor role - can edit content created by others.
    /// </summary>
    public const string Editor = "Editors";

    /// <summary>
    /// Reviewer role - can review and approve content.
    /// </summary>
    public const string Reviewer = "Reviewers";

    /// <summary>
    /// Team Member role - basic team access.
    /// </summary>
    public const string TeamMember = "Team Members";
}
