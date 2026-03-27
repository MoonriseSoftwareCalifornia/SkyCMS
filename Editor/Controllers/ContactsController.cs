// <copyright file="ContactsController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Services.Configurations;
    using CsvHelper;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Models;

    /// <summary>
    /// Contact management controller.
    /// </summary>
    [Authorize(Roles = "Administrators,Editors")]
    public class ContactsController : Controller
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<ContactsController> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContactsController"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="logger">Logger.</param>
        public ContactsController(ApplicationDbContext dbContext, ILogger<ContactsController> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the contact list page.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Index()
        {
            try
            {
                var alertsEnabled = await GetAlertsEnabledAsync();
                var hasMailChimp = await HasMailChimpIntegrationAsync();

                ViewData["EnableAlerts"] = alertsEnabled;
                ViewData["MailChimpIntegrated"] = hasMailChimp;

                return View();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading contacts index");
                return StatusCode(500, "An error occurred loading the contacts page");
            }
        }

        /// <summary>
        /// Enables or disables contact alerts.
        /// </summary>
        /// <param name="enable">Indicates if alerts should be enabled.</param>
        /// <returns>Success or not.</returns>
        [HttpPost]
        public async Task<IActionResult> EnableAlerts(bool enable)
        {
            try
            {
                var setting = await dbContext.Settings
                    .FirstOrDefaultAsync(w => w.Group == ConfigGroups.ContactsConfig && w.Name == ConfigKeys.EnableAlerts);

                if (setting == null)
                {
                    setting = new Setting
                    {
                        Group = ConfigGroups.ContactsConfig,
                        Name = ConfigKeys.EnableAlerts,
                        Value = enable.ToString(),
                        Description = "Send an email alert when a new contact is added or updated."
                    };
                    dbContext.Settings.Add(setting);
                }
                else
                {
                    setting.Value = enable.ToString();
                }

                await dbContext.SaveChangesAsync();
                logger.LogInformation("Contact alerts {Status}", enable ? "enabled" : "disabled");

                return Ok(new { enabled = enable });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating alert setting");
                return StatusCode(500, "Failed to update alert setting");
            }
        }

        /// <summary>
        /// Gets the contact list.
        /// </summary>
        /// <returns>Returns a list of contacts.</returns>
        [HttpGet]
        public async Task<IActionResult> GetContacts()
        {
            try
            {
                var contacts = await dbContext.Contacts
                    .OrderBy(o => o.Email)
                    .ToListAsync();

                return Json(new ContactsListResponse { data = contacts });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving contacts");
                return StatusCode(500, "Failed to retrieve contacts");
            }
        }

        /// <summary>
        /// Exports contacts as CSV.
        /// </summary>
        /// <returns>Returns a CSV file.</returns>
        public async Task<IActionResult> ExportContacts()
        {
            try
            {
                var data = await dbContext.Contacts
                    .OrderBy(o => o.Created)
                    .ToListAsync();

                if (!data.Any())
                {
                    return NotFound("No contacts found to export");
                }

                var memoryStream = new MemoryStream();
                try
                {
                    await using (var writer = new StreamWriter(memoryStream, leaveOpen: true))
                    await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture, leaveOpen: true))
                    {
                        var export = data.Select((contact, index) => new ContactsExportViewModel
                        {
                            Id = index + 1,
                            Created = contact.Created,
                            Email = contact.Email ?? string.Empty,
                            FirstName = contact.FirstName ?? string.Empty,
                            LastName = contact.LastName ?? string.Empty,
                            Phone = contact.Phone ?? string.Empty,
                            Updated = contact.Updated
                        });

                        await csv.WriteRecordsAsync(export);
                        await csv.FlushAsync();
                    }

                    logger.LogInformation("Exported {Count} contacts", data.Count);
                    return File(memoryStream.ToArray(), "text/csv", "contact-list.csv");
                }
                finally
                {
                    await memoryStream.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error exporting contacts");
                return StatusCode(500, "Failed to export contacts");
            }
        }

        /// <summary>
        /// Opens the MailChimp configuration page.
        /// </summary>
        /// <returns>Returns the view.</returns>
        public async Task<IActionResult> MailChimp()
        {
            try
            {
                var settings = await dbContext.Settings
                    .Where(w => w.Group == ConfigGroups.MailChimp)
                    .ToListAsync();

                var model = new MailChimpConfig
                {
                    ContactListName = settings.FirstOrDefault(f => f.Name == ConfigKeys.ContactListName)?.Value,
                    ApiKey = settings.FirstOrDefault(f => f.Name == ConfigKeys.ApiKey)?.Value
                };

                return View(model);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading MailChimp configuration");
                return StatusCode(500, "Failed to load MailChimp configuration");
            }
        }

        /// <summary>
        /// Removes MailChimp settings.
        /// </summary>
        /// <returns>Redirects to Index when done.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMailChimp()
        {
            try
            {
                var settings = await dbContext.Settings
                    .Where(w => w.Group == ConfigGroups.MailChimp)
                    .ToListAsync();

                if (settings.Any())
                {
                    dbContext.Settings.RemoveRange(settings);
                    await dbContext.SaveChangesAsync();
                    logger.LogInformation("MailChimp configuration removed");
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error removing MailChimp configuration");
                TempData["Error"] = "Failed to remove MailChimp configuration";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Saves MailChimp settings.
        /// </summary>
        /// <param name="model">MailChimp configuration model.</param>
        /// <returns>Redirects to index if successful.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MailChimp(MailChimpConfig model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.ApiKey) || string.IsNullOrWhiteSpace(model.ContactListName))
            {
                ModelState.AddModelError(string.Empty, "API Key and Contact List Name are required");
                return View(model);
            }

            try
            {
                var settings = await dbContext.Settings
                    .Where(w => w.Group == ConfigGroups.MailChimp)
                    .ToListAsync();

                await UpsertSettingAsync(settings, ConfigKeys.ApiKey, model.ApiKey, "MailChimp API Key");
                await UpsertSettingAsync(settings, ConfigKeys.ContactListName, model.ContactListName.Trim(),
                    "List name that contacts are added to");

                await dbContext.SaveChangesAsync();
                logger.LogInformation("MailChimp configuration saved");

                TempData["Success"] = "MailChimp configuration saved successfully";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving MailChimp configuration");
                ModelState.AddModelError(string.Empty, "Failed to save MailChimp configuration");
                return View(model);
            }
        }

        private async Task<bool> GetAlertsEnabledAsync()
        {
            var alertSetting = await dbContext.Settings
                .FirstOrDefaultAsync(w => w.Group == ConfigGroups.ContactsConfig && w.Name == ConfigKeys.EnableAlerts);

            return alertSetting != null && bool.TryParse(alertSetting.Value, out var enabled) && enabled;
        }

        private async Task<bool> HasMailChimpIntegrationAsync()
        {
            return await dbContext.Settings
                .AnyAsync(w => w.Group == ConfigGroups.MailChimp);
        }

        private async Task UpsertSettingAsync(List<Setting> settings, string name, string value, string description)
        {
            var setting = settings.FirstOrDefault(f => f.Name == name);
            if (setting == null)
            {
                setting = new Setting
                {
                    Group = ConfigGroups.MailChimp,
                    Name = name,
                    Value = value,
                    Description = description
                };
                dbContext.Settings.Add(setting);
            }
            else
            {
                setting.Value = value;
            }
        }

        private static class ConfigGroups
        {
            public const string ContactsConfig = "ContactsConfig";
            public const string MailChimp = "MailChimp";
        }

        private static class ConfigKeys
        {
            public const string EnableAlerts = "EnableAlerts";
            public const string ContactListName = "ContactListName";
            public const string ApiKey = "ApiKey";
        }

        /// <summary>
        /// Response model for DataTables jQuery plugin.
        /// </summary>
        private class ContactsListResponse
        {
            public List<Contact> data { get; set; } = new();
        }
    }
}
