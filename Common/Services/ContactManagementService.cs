// <copyright file="ContactManagementService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Models;
    using MailChimp.Net;
    using MailChimp.Net.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity.UI.Services;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Service for managing contacts, including adding and updating contact information, and integrating with MailChimp for email marketing.
    /// </summary>
    public class ContactManagementService : IContactManagementService
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IEmailSender emailSender;
        private readonly ILogger<IContactManagementService> logger;
        private readonly IHttpContextAccessor httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="IContactManagementService"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="emailSender">Email sender.</param>
        /// <param name="logger">Log service.</param>
        /// <param name="httpContextAccessor">HttpContext accessor.</param>
        public ContactManagementService(
            ApplicationDbContext dbContext,
            IEmailSender emailSender,
            ILogger<IContactManagementService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            this.dbContext = dbContext;
            this.emailSender = emailSender;
            this.logger = logger;
            this.httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Adds a new contact or updates an existing contact in the database and MailChimp list.
        /// </summary>
        /// <param name="model">Post model.</param>
        /// <returns>ContactViewModel.</returns>
        public async Task<ContactViewModel> AddContactAsync(ContactViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(model.Email))
            {
                throw new ArgumentException("Invalid email address", nameof(model.Email));
            }

            var contact = await dbContext.Contacts.FirstOrDefaultAsync(f => f.Email.ToLower() == model.Email.ToLower());

            if (contact == null)
            {
                dbContext.Contacts.Add(new Data.Contact() { Email = model.Email.ToLower(), FirstName = model.FirstName, LastName = model.LastName, Phone = model.Phone });
            }
            else
            {
                contact.Updated = DateTimeOffset.UtcNow;
                contact.FirstName = model.FirstName;
                contact.LastName = model.LastName;
                contact.Phone = model.Phone;
            }

            await dbContext.SaveChangesAsync();

            // MailChimp? If so add contact to list.
            var settings = await dbContext.Settings.Where(w => w.Group == "MailChimp").ToListAsync();
            if (settings.Count > 0)
            {
                var key = settings.FirstOrDefault(f => f.Name == "ApiKey");
                var list = settings.FirstOrDefault(f => f.Name == "ContactListName");

                if (key == null || list == null || string.IsNullOrWhiteSpace(key.Value) || string.IsNullOrWhiteSpace(list.Value))
                {
                    logger.LogWarning("MailChimp settings incomplete. Skipping integration.");
                    return model;
                }

                var manager = new MailChimpManager(key.Value);

                var lists = await manager.Lists.GetAllAsync();
                var mclist = lists.FirstOrDefault(w => w.Name.Equals(list.Value, StringComparison.OrdinalIgnoreCase));

                var member = new Member { FullName = $"{model.FirstName} {model.LastName}", EmailAddress = contact.Email, StatusIfNew = MailChimp.Net.Models.Status.Subscribed };

                member.LastChanged = DateTimeOffset.UtcNow.ToString();

                member.MergeFields.Add("FNAME", model.FirstName);
                member.MergeFields.Add("LNAME", model.LastName);

                var updated = await manager.Members.AddOrUpdateAsync(mclist.Id, member);

                logger.LogInformation($"Add or updated {updated.FullName} {updated.EmailAddress} with MailChimp on {updated.LastChanged}.");
            }

            var httpContext = httpContextAccessor.HttpContext;
            var alertsSetting = await dbContext.Settings.FirstOrDefaultAsync(w => w.Group == "ContactsConfig" && w.Name == "EnableAlerts");

            if (alertsSetting != null && bool.Parse(alertsSetting.Value) == true)
            {
                // Not using UserManager because we don't want to require injection for that in this base class.
                var adminUserGroup = await dbContext.Roles.FirstOrDefaultAsync(w => w.NormalizedName == "ADMINISTRATORS");
                if (adminUserGroup == null)
                {
                    logger.LogWarning("Administrators role not found. Cannot send contact alerts.");
                    return model;
                }

                var roleUsers = await dbContext.UserRoles.Where(w => w.RoleId == adminUserGroup.Id).Select(s => s.UserId).ToArrayAsync();
                var admins = await dbContext.Users.Where(w => roleUsers.Contains(w.Id)).Select(s => s.Email).ToArrayAsync();

                var subject = "New Contact Information";
                var body = $"<p>New contact information received from {model.FirstName} {model.LastName} at {model.Email} on website '{httpContext.Request.Host}.'</p>";
                body += $"<p>Open your editor to view and download the contact list.</p>";

                foreach (var e in admins)
                {
                    await emailSender.SendEmailAsync(e, subject, body);
                }
            }

            return model;
        }
    }
}
