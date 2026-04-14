// <copyright file="EmailAdminController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Controllers
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.EmailServices;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity.UI.Services;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Models;

    /// <summary>
    /// Email administrator controller.
    /// </summary>
    [Authorize(Roles = "Administrators, Editors")]
    public class EmailAdminController : Controller
    {
        private readonly ILogger<EmailAdminController> logger;
        private readonly ICosmosEmailSender emailSender;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailAdminController"/> class.
        /// </summary>
        /// <param name="logger">Log service.</param>
        /// <param name="emailSender">Email sending service.</param>
        public EmailAdminController(
            ILogger<EmailAdminController> logger,
            IEmailSender emailSender)
        {
            this.logger = logger;
            this.emailSender = (ICosmosEmailSender)emailSender;
        }

        /// <summary>
        /// Index action.
        /// </summary>
        /// <returns>IAction Result.</returns>
        public IActionResult Index()
        {
            return View(new TestEmailMessageViewModel());
        }

        /// <summary>
        /// Handles the postback from the email form.
        /// </summary>
        /// <param name="emailMessage">Test email message.</param>
        /// <returns>IActionResult.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(TestEmailMessageViewModel emailMessage)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await emailSender.SendEmailAsync(emailMessage.To, emailMessage.Subject, emailMessage.Body, emailMessage.Body);
                    emailMessage.Success = emailSender.SendResult.IsSuccessStatusCode;
                    emailMessage.ErrorMessage = emailSender.SendResult.Message;
                }
                catch (Exception e)
                {
                    emailMessage.Success = false;
                    emailMessage.ErrorMessage = e.Message;
                    logger.LogError(e, "Error sending test email.");
                }
            }

            return View(emailMessage);
        }
    }
}
