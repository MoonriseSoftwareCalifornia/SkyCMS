// <copyright file="ISetupCheckService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using System.Threading.Tasks;

namespace Sky.Editor.Services.Setup
{
    /// <summary>
    ///  Setup check interface.
    /// </summary>
    public interface ISetupCheckService
    {
        /// <summary>
        /// Gets the message describing the setup status.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Determines if the application is set up.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating if the application is set up.</returns>
        Task<bool> IsSetup();

        /// <summary>
        /// Resets the object to its initial state.
        /// </summary>
        void Reset();
    }
}