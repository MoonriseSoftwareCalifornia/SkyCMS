# Cosmos.EmailServices - Multi-Service Email Provider

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![NuGet](https://img.shields.io/nuget/v/Cosmos.EmailServices.svg)](https://www.nuget.org/packages/Cosmos.EmailServices/)

This is an IEmailSender implementation for **ASP.NET Core Identity** supporting multiple email service providers. Part of the [SkyCMS](https://github.com/CWALabs/SkyCMS) project.

In one package it provides the following services:

* Azure Communication Services - Email Services
* SendGrid (Twilio)
* SMTP service that supports TLS, user name and password
* NoOp Email, an email service for dev/test that does nothing

## Installation

To install the package using CLI, run the following command:
```bash
dotnet add package Cosmos.EmailServices
```

To install the package using NuGet Package Manager, run the following command:
```bash
Install-Package Cosmos.EmailServices
```

## Usage

In the user secrets of an ASP.NET Core Identity web app, add one of
the following configuration depending on the service you want to use.

## Azure Communication Services - Email Services

To configure for [Azure Communication services](https://www.youtube.com/watch?v=uofVnRgm92o), add the following configuration
to the user secrets:

```json
{
	"AdminEmail": "your@emailaddress.com", // This is the default 'from to address',
	"ConnectionStrings": {
		{
			"AzureCommunicationConnection" : "[Your connection string here]"
		}
}
```

## SendGrid (Twilio)

To configure for [SendGrid](https://www.twilio.com/sendgrid), add the following
configuration:

```json
{
	"AdminEmail": "your@emailaddress.com", // This is the default 'from to address',
	"CosmosSendGridApiKey": "[Your SendGrid key here]",
}
```

## SMTP service that supports TLS, user name and password

To configure for an SMTP service, add the following configuration:
```json
{
	"AdminEmail": "", // This is the default 'from to address',
	"SmtpEmailProviderOptions" : {
		"Host": "smtp.yourhost.com",
		"Port": 587,
		"UsesSsl": true, // False if uses TLS
		"UserName": "yourusername",
		"Password": "yourpassword"
	}
}
```

## NoOp Email

If none of the three settings above are given, this will install a NoOp email service.

## Program.cs or `ConfigureServices` method of Startup.cs

Add the following using:

```csharp

using Cosmos.EmailServices;

```

Then add the following line:

```csharp

builder.Services.AddCosmosEmailServices(builder.Configuration);

```

## Example

For a full working example of this package, see the [SkyCMS](https://github.com/CWALabs/SkyCMS) source code.

In the mean time, here is an example of how to use the email service in a Razor Page:

```csharp
// <copyright file="ResetPassword.cshtml.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/MoonriseSoftwareCalifornia/CosmosCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Areas.Identity.Pages.Account
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common.Services.Configurations;
    using Cosmos.Common.Data;
    using Cosmos.EmailServices;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Identity.UI.Services;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.AspNetCore.RateLimiting;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Reset password page model.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("fixed")]
    public class ResetPasswordModel : PageModel
    {
        private readonly IOptions<SiteSettings> options;
        private readonly ICosmosEmailSender emailSender;
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<ForgotPasswordModel> logger;
        private readonly UserManager<IdentityUser> userManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResetPasswordModel"/> class.
        /// </summary>
        /// <param name="userManager">User manager.</param>
        /// <param name="options">Site settings.</param>
        /// <param name="emailSender">Email sender service.</param>
        /// <param name="dbContext">Database context.</param>
        /// <param name="logger">Log service.</param>
        public ResetPasswordModel(
            UserManager<IdentityUser> userManager,
            IOptions<SiteSettings> options,
            IEmailSender emailSender,
            ApplicationDbContext dbContext,
            ILogger<ForgotPasswordModel> logger)
        {
            this.userManager = userManager;
            this.options = options;
            this.emailSender = (ICosmosEmailSender)emailSender;
            this.dbContext = dbContext;
            this.logger = logger;
        }

        /// <summary>
        /// Gets or sets input model.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        /// Get handler.
        /// </summary>
        /// <param name="code">Reset password verification code.</param>
        /// <returns>Returns an <see cref="IActionResult"/>.</returns>
        public IActionResult OnGet(string code = null)
        {
            if (code == null)
            {
                return BadRequest("A code must be supplied for password reset.");
            }

            Input = new InputModel
            {
                Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code))
            };
            return Page();
        }

        /// <summary>
        /// Post handler.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var homePage = await dbContext.Pages.Select(s => new { s.Title, s.UrlPath }).FirstOrDefaultAsync(f => f.UrlPath == "root");
            var websiteName = homePage.Title ?? Request.Host.Host;

            var admins = await userManager.GetUsersInRoleAsync("Administrators");
            var emailHandler = new EmailHandler(emailSender, logger);

            var user = await userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                await emailHandler.SendGeneralInfoTemplateEmail(
                        "User without an account tried to change a password",
                        "System Notification",
                        websiteName,
                        Request.Host.Host,
                        $"<p>This is a notification that '{Input.Email},' who does not have an account on this website, tried to change a password for website '{Request.Host.Host}' on {DateTime.UtcNow.ToString()} (UTC). No password reset email was sent.</p>",
                        admins.Select(s => s.Email).ToList());

                // Don't reveal that the user does not exist
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            var result = await userManager.ResetPasswordAsync(user, Input.Code, Input.Password);
            if (result.Succeeded)
            {
                // Notify the administrators of a password change.
                await emailHandler.SendGeneralInfoTemplateEmail(
                    "Password was changed.",
                    "System Notification",
                    websiteName,
                    Request.Host.Host,
                    $"<p>This is a notification that '{Input.Email}' changed their password for website '{Request.Host.Host}' on {DateTime.UtcNow.ToString()} (UTC).</p>",
                    admins.Select(s => s.Email).ToList());

                // Notify the user of a password change.
                await emailHandler.SendGeneralInfoTemplateEmail(
                    "Password was changed.",
                    "System Notification",
                    websiteName,
                    Request.Host.Host,
                    $"<p>This is a confirmation that your password was changed for website '{Request.Host.Host}' on {DateTime.UtcNow.ToString()} (UTC).</p>",
                    Input.Email);

                return RedirectToPage("./ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        /// <summary>
        /// Form input model.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            /// Gets or sets user email address.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            /// <summary>
            /// Gets or sets error message (if any).
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            /// Gets or sets confirm password field.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            /// <summary>
            /// Gets or sets code field.
            /// </summary>
            public string Code { get; set; }
        }
    }
}
```

## EMail Templates

This package comes with a few Email templates that can be used to format
emails.

Each template consists of two files. One for HTML formatted email
and another for text.

Here are examples of two files found in the 'Templates' folder:

GeneralInfo.html
GeneralInfoTXT.txt

Here is an example of the text file:

```text
{{Subject}}
{{Subtitle}}
From: {{WebsiteName}}

{{Body}}


```

Note the double brackes.  This are spots in the email where content is inserted.

If you add templates to this project, please add them to the `EmailTemplates.resx`
file.

Here is an example of how to use the general email templates in the code:

```csharp

await emailHandler.SendGeneralInfoTemplateEmail(
    "Password was changed.",
    "System Notification",
    websiteName,
    Request.Host.Host,
    $"<p>This is a confirmation that your password was changed for website '{Request.Host.Host}' on {DateTime.UtcNow.ToString()} (UTC).</p>",
    Input.Email);
```

For more options, see class `EmailHandler.cs`.