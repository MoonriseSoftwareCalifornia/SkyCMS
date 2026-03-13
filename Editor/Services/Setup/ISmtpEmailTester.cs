// <copyright file="ISmtpEmailTester.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using System.Threading.Tasks;

    /// <summary>
    /// Tests SMTP email configuration.
    /// </summary>
    public interface ISmtpEmailTester
    {
        /// <summary>
        /// Executes an SMTP test email operation.
        /// </summary>
        /// <param name="host">SMTP host.</param>
        /// <param name="port">SMTP port.</param>
        /// <param name="username">SMTP user name.</param>
        /// <param name="password">SMTP password.</param>
        /// <param name="senderEmail">Sender email address.</param>
        /// <param name="recipient">Recipient email address.</param>
        /// <returns>Email configuration test result.</returns>
        Task<TestResult> TestAsync(string host, string port, string username, string password, string senderEmail, string recipient);
    }
}
