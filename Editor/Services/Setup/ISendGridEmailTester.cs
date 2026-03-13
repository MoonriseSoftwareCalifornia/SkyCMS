// <copyright file="ISendGridEmailTester.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using System.Threading.Tasks;

    /// <summary>
    /// Tests SendGrid email configuration.
    /// </summary>
    public interface ISendGridEmailTester
    {
        /// <summary>
        /// Executes a SendGrid test email operation.
        /// </summary>
        /// <param name="apiKey">SendGrid API key.</param>
        /// <param name="senderEmail">Sender email address.</param>
        /// <param name="recipient">Recipient email address.</param>
        /// <returns>Email configuration test result.</returns>
        Task<TestResult> TestAsync(string apiKey, string senderEmail, string recipient);
    }
}
