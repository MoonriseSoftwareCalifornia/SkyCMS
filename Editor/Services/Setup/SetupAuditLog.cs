// <copyright file="SetupAuditLog.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Setup audit log entry structure.
    /// Tracks configuration changes made during or after setup.
    /// Stored in Settings table as JSON (Group="SETUP", Name="SettingChange").
    /// TODO: Create audit log viewer UI that displays these records in an admin-accessible table.
    ///       Design to be extensible for other audit logs (article edits, user access, etc.).
    /// </summary>
    public class SetupAuditLog
    {
        /// <summary>
        /// Gets or sets the setup session ID.
        /// </summary>
        public Guid SessionId { get; set; }

        /// <summary>
        /// Gets or sets when the change occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets who initiated the change (user email or system).
        /// </summary>
        public string InitiatedBy { get; set; }

        /// <summary>
        /// Gets or sets a description of what changed.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the changes made (property name -> (old value, new value)).
        /// Sensitive fields are masked as "(masked)" in the audit log.
        /// </summary>
        public Dictionary<string, (string OldValue, string NewValue)> Changes { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether this was the initial setup or a post-setup change.
        /// </summary>
        public bool IsInitialSetup { get; set; }
    }
}
