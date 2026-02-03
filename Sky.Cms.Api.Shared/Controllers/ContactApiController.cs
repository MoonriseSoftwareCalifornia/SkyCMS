// <copyright file="ContactApiController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Controllers;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Services.Email;
using Sky.Cms.Api.Shared.Features.ContactForm.Submit;
using Sky.Cms.Api.Shared.Features.ContactForm.ValidateCaptcha;
using Sky.Cms.Api.Shared.Models;
using Cosmos.Common.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// API controller for handling contact form submissions.
/// </summary>
[ApiController]
[Route("_api/contact")]
public class ContactApiController : ControllerBase
{
    private readonly IMediator mediator;
    private readonly IAntiforgery antiforgery;
    private readonly ILogger<ContactApiController> logger;
    private readonly ApplicationDbContext dbContext;
    private readonly IEmailConfigurationService emailConfigService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactApiController"/> class.
    /// </summary>
    /// <param name="mediator">Mediator for CQRS commands and queries.</param>
    /// <param name="antiforgery">Antiforgery service.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="dbContext">Database context.</param>
    /// <param name="emailConfigService">Email configuration service for fallback admin email.</param>
    public ContactApiController(
        IMediator mediator,
        IAntiforgery antiforgery,
        ILogger<ContactApiController> logger,
        ApplicationDbContext dbContext,
        IEmailConfigurationService emailConfigService)
    {
        this.mediator = mediator;
        this.antiforgery = antiforgery;
        this.logger = logger;
        this.dbContext = dbContext;
        this.emailConfigService = emailConfigService;
    }

    /// <summary>
    /// Gets the JavaScript library with embedded antiforgery token and configuration.
    /// </summary>
    /// <returns>JavaScript file content.</returns>
    [HttpGet("skycms-contact.js")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContactScript(CancellationToken cancellationToken = default)
    {
        try
        {
            // Load tenant-specific configuration from database
            var config = await LoadContactApiConfigAsync(cancellationToken);

            // Generate antiforgery token
            var tokens = antiforgery.GetAndStoreTokens(HttpContext);
            var token = tokens.RequestToken ?? string.Empty; // Fix nullable warning

            // Build JavaScript with embedded configuration
            var script = GenerateJavaScriptLibrary(token, config);

            return Content(script, "application/javascript");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating contact form JavaScript");
            return StatusCode(500, new { error = "Error generating script" });
        }
    }

    /// <summary>
    /// Submits a contact form.
    /// </summary>
    /// <param name="request">Contact form request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Contact form response.</returns>
    [HttpPost("submit")]
    [EnableRateLimiting("contact-form")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(ContactFormResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ContactFormResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ContactFormResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Submit([FromBody] ContactFormRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate model state
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>());

                return BadRequest(new ContactFormResponse
                {
                    Success = false,
                    Message = "Validation failed. Please check your input.",
                    Error = string.Join(", ", errors.SelectMany(e => e.Value))
                });
            }

            // Load tenant-specific configuration from database
            var config = await LoadContactApiConfigAsync(cancellationToken);

            // Get remote IP address
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Validate CAPTCHA if required
            if (config.RequireCaptcha)
            {
                if (string.IsNullOrEmpty(request.CaptchaToken))
                {
                    logger.LogWarning("CAPTCHA token missing for IP: {RemoteIp}", remoteIp);
                    return BadRequest(new ContactFormResponse
                    {
                        Success = false,
                        Message = "CAPTCHA validation is required.",
                        Error = "Missing CAPTCHA token"
                    });
                }

                var captchaQuery = new ValidateCaptchaQuery
                {
                    Token = request.CaptchaToken,
                    RemoteIpAddress = remoteIp,
                    CaptchaProvider = config.CaptchaProvider,
                    SecretKey = config.CaptchaSecretKey
                };

                var captchaValid = await mediator.QueryAsync(captchaQuery, cancellationToken);
                
                if (!captchaValid)
                {
                    logger.LogWarning(
                        "CAPTCHA validation failed for IP: {RemoteIp} using provider: {Provider}",
                        remoteIp,
                        config.CaptchaProvider);
                    
                    return BadRequest(new ContactFormResponse
                    {
                        Success = false,
                        Message = "CAPTCHA validation failed. Please try again.",
                        Error = "Invalid CAPTCHA"
                    });
                }
            }

            // Submit contact form via mediator
            var command = new SubmitContactFormCommand
            {
                Request = request,
                RemoteIpAddress = remoteIp
            };

            var result = await mediator.SendAsync(command, cancellationToken);

            if (result.IsSuccess && result.Data != null)
            {
                logger.LogInformation("Contact form submitted successfully from IP: {RemoteIp}", remoteIp);
                return Ok(result.Data);
            }
            else
            {
                return BadRequest(new ContactFormResponse
                {
                    Success = false,
                    Message = result.ErrorMessage ?? "Failed to submit contact form.",
                    Error = result.ErrorMessage
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing contact form submission");
            return StatusCode(500, new ContactFormResponse
            {
                Success = false,
                Message = "An unexpected error occurred. Please try again later.",
                Error = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Loads Contact API configuration from the Settings table in the database.
    /// Reads CAPTCHA settings as a JSON string from the CAPTCHA group.
    /// Falls back to email provider's AdminEmail if Contact API AdminEmail is not configured.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ContactApiConfig populated from database settings.</returns>
    private async Task<ContactApiConfig> LoadContactApiConfigAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Load admin email and max message length from ContactApi group
            var contactApiSettings = await dbContext.Settings
                .Where(s => s.Group == "ContactApi")
                .ToListAsync(cancellationToken);

            var adminEmail = contactApiSettings.FirstOrDefault(s => s.Name == "AdminEmail")?.Value;
            
            // If ContactApi AdminEmail is not configured, fall back to email provider's SenderEmail
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                logger.LogInformation("Contact API AdminEmail not configured, falling back to email provider settings");
                var emailSettings = await emailConfigService.GetEmailSettingsAsync();
                adminEmail = emailSettings.SenderEmail;
                
                if (string.IsNullOrWhiteSpace(adminEmail))
                {
                    logger.LogWarning("No AdminEmail found in Contact API or Email settings, using default");
                    adminEmail = "admin@example.com";
                }
                else
                {
                    logger.LogInformation("Using email provider's SenderEmail as AdminEmail: {AdminEmail}", adminEmail);
                }
            }
            
            var maxMessageLength = int.Parse(
                contactApiSettings.FirstOrDefault(s => s.Name == "MaxMessageLength")?.Value ?? "5000");

            // Load CAPTCHA settings as JSON from CAPTCHA group
            var captchaSetting = await dbContext.Settings
                .FirstOrDefaultAsync(s => s.Group == "CAPTCHA" && s.Name == "Config", cancellationToken);

            // Use the JSON constructor or factory method from ContactApiConfig
            var config = ContactApiConfig.FromDatabaseSettings(
                adminEmail,
                maxMessageLength,
                captchaSetting?.Value);

            logger.LogInformation(
                "Loaded Contact API config - AdminEmail: {AdminEmail}, CAPTCHA: {Enabled}, Provider: {Provider}",
                config.AdminEmail,
                config.RequireCaptcha,
                config.CaptchaProvider ?? "none");

            return config;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load Contact API configuration from database");
            
            // Try to get email settings as final fallback
            try
            {
                var emailSettings = await emailConfigService.GetEmailSettingsAsync();
                var fallbackEmail = !string.IsNullOrWhiteSpace(emailSettings.SenderEmail) 
                    ? emailSettings.SenderEmail 
                    : "admin@example.com";
                
                logger.LogInformation("Using fallback email configuration: {AdminEmail}", fallbackEmail);
                
                return new ContactApiConfig
                {
                    AdminEmail = fallbackEmail,
                    MaxMessageLength = 5000,
                    RequireCaptcha = false
                };
            }
            catch
            {
                // Return absolute safe defaults on complete failure
                return new ContactApiConfig
                {
                    AdminEmail = "admin@example.com",
                    MaxMessageLength = 5000,
                    RequireCaptcha = false
                };
            }
        }
    }

    private string GenerateJavaScriptLibrary(string antiforgeryToken, ContactApiConfig config)
    {
        var captchaConfig = config.RequireCaptcha
            ? $@"
        requireCaptcha: true,
        captchaProvider: '{config.CaptchaProvider}',
        captchaSiteKey: '{config.CaptchaSiteKey}'"
            : @"
        requireCaptcha: false";

        // Generate provider-specific CAPTCHA implementation
        var captchaImplementation = GenerateCaptchaImplementation(config);

        return $@"
/**
 * SkyCMS Contact Form API Client
 * Auto-generated with embedded configuration and antiforgery token
 * Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
 * CAPTCHA Provider: {config.CaptchaProvider ?? "none"}
 */
(function(window) {{
    'use strict';

    const SkyCmsContact = {{
        config: {{{captchaConfig},
            antiforgeryToken: '{antiforgeryToken}',
            submitEndpoint: '/_api/contact/submit',
            maxMessageLength: {config.MaxMessageLength},
            fieldNames: {{
                name: 'name',
                email: 'email',
                message: 'message'
            }}
        }},

        /**
         * Initialize contact form
         * @param {{string|HTMLFormElement}} formSelector - Form selector or element
         * @param {{Object}} options - Configuration options
         * @param {{Object}} options.fieldNames - Custom field names mapping (optional)
         * @param {{string}} options.fieldNames.name - Name field name (default: 'name')
         * @param {{string}} options.fieldNames.email - Email field name (default: 'email')
         * @param {{string}} options.fieldNames.message - Message field name (default: 'message')
         * @param {{Function}} options.onSuccess - Success callback (optional)
         * @param {{Function}} options.onError - Error callback (optional)
         * @param {{string}} options.errorElementId - ID of element to display errors (optional)
         * @param {{string}} options.successElementId - ID of element to display success messages (optional)
         */
        init: function(formSelector, options) {{
            const form = typeof formSelector === 'string' 
                ? document.querySelector(formSelector) 
                : formSelector;

            if (!form) {{
                console.error('SkyCmsContact: Form not found');
                return;
            }}

            const config = {{ 
                ...this.config, 
                ...options,
                fieldNames: {{ ...this.config.fieldNames, ...(options?.fieldNames || {{}}) }}
            }};

            form.addEventListener('submit', async (e) => {{
                e.preventDefault();
                await this.handleSubmit(form, config);
            }});

            // Load CAPTCHA if required
            if (this.config.requireCaptcha) {{
                this.loadCaptcha(config);
            }}
        }},

        /**
         * Display error message using configured method
         * @param {{Object}} config - Configuration
         * @param {{string}} message - Error message
         */
        displayError: function(config, message) {{
            if (config.onError) {{
                try {{
                    config.onError({{ success: false, message: message }});
                }} catch (err) {{
                    console.error('SkyCmsContact: Error in onError callback:', err);
                    // Fallback to alert if custom handler fails
                    alert(message);
                }}
            }} else if (config.errorElementId) {{
                const errorEl = document.getElementById(config.errorElementId);
                if (errorEl) {{
                    errorEl.textContent = message;
                    errorEl.style.display = 'block';
                }} else {{
                    console.warn('SkyCmsContact: Error element not found: ' + config.errorElementId);
                    alert(message);
                }}
            }} else {{
                alert(message);
            }}
        }},

        /**
         * Display success message using configured method
         * @param {{Object}} config - Configuration
         * @param {{Object}} result - Success result
         */
        displaySuccess: function(config, result) {{
            if (config.onSuccess) {{
                try {{
                    config.onSuccess(result);
                }} catch (err) {{
                    console.error('SkyCmsContact: Error in onSuccess callback:', err);
                    // Fallback to alert if custom handler fails
                    alert(result.message);
                }}
            }} else if (config.successElementId) {{
                const successEl = document.getElementById(config.successElementId);
                if (successEl) {{
                    successEl.textContent = result.message;
                    successEl.style.display = 'block';
                }} else {{
                    console.warn('SkyCmsContact: Success element not found: ' + config.successElementId);
                    alert(result.message);
                }}
            }} else {{
                alert(result.message);
            }}
        }},

        /**
         * Handle form submission
         * @param {{HTMLFormElement}} form - Form element
         * @param {{Object}} config - Configuration
         */
        handleSubmit: async function(form, config) {{
            const formData = new FormData(form);
            const fieldNames = config.fieldNames || this.config.fieldNames;
            
            const data = {{
                name: formData.get(fieldNames.name),
                email: formData.get(fieldNames.email),
                message: formData.get(fieldNames.message)
            }};

            // Add CAPTCHA token if required
            if (config.requireCaptcha) {{
                try {{
                    data.captchaToken = await this.getCaptchaToken(config);
                }} catch (error) {{
                    console.error('SkyCmsContact: CAPTCHA error:', error);
                    this.displayError(config, 'CAPTCHA validation failed. Please try again.');
                    return;
                }}
            }}

            try {{
                const response = await fetch(config.submitEndpoint, {{
                    method: 'POST',
                    headers: {{
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': config.antiforgeryToken
                    }},
                    body: JSON.stringify(data)
                }});

                const result = await response.json();

                if (result.success) {{
                    this.displaySuccess(config, result);
                    form.reset();
                }} else {{
                    this.displayError(config, result.message || 'Submission failed. Please try again.');
                }}
            }} catch (error) {{
                console.error('SkyCmsContact submission error:', error);
                this.displayError(config, 'Network error. Please try again.');
            }}
        }},{captchaImplementation}
    }};

    window.SkyCmsContact = SkyCmsContact;

}})(window);
";
    }

    private string GenerateCaptchaImplementation(ContactApiConfig config)
    {
        if (!config.RequireCaptcha || string.IsNullOrEmpty(config.CaptchaProvider))
        {
            return @"

        /**
         * Load CAPTCHA script (No CAPTCHA configured)
         */
        loadCaptcha: function(config) {
            // No CAPTCHA provider configured
        },

        /**
         * Get CAPTCHA token (No CAPTCHA configured)
         */
        getCaptchaToken: async function(config) {
            return '';
        }";
        }

        return config.CaptchaProvider.ToLower() switch
        {
            "turnstile" => GenerateTurnstileImplementation(),
            "recaptcha" => GenerateReCaptchaImplementation(),
            _ => @"
        // Unknown provider implementation
        "
        };
    }

    private string GenerateTurnstileImplementation()
    {
        return @"

        /**
         * Load Cloudflare Turnstile script
         */
        loadCaptcha: function(config) {
            if (document.getElementById('turnstile-script')) {
                return; // Already loaded
            }

            const script = document.createElement('script');
            script.id = 'turnstile-script';
            script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js';
            script.async = true;
            script.defer = true;
            document.head.appendChild(script);

            console.log('SkyCmsContact: Cloudflare Turnstile loaded');
        },

        /**
         * Get Cloudflare Turnstile token
         * @param {Object} config - Configuration
         * @returns {Promise<string>} Turnstile token
         */
        getCaptchaToken: async function(config) {
            return new Promise((resolve, reject) => {
                // Wait for Turnstile to load
                const checkTurnstile = setInterval(() => {
                    if (typeof window.turnstile !== 'undefined') {
                        clearInterval(checkTurnstile);

                        try {
                            // Check if already rendered
                            const existingWidget = document.querySelector('.cf-turnstile');
                            if (existingWidget && existingWidget.querySelector('input[name=""cf-turnstile-response""]')) {
                                const token = existingWidget.querySelector('input[name=""cf-turnstile-response""]').value;
                                if (token) {
                                    resolve(token);
                                    return;
                                }
                            }

                            // Create container if it doesn't exist
                            let container = document.querySelector('.cf-turnstile');
                            if (!container) {
                                container = document.createElement('div');
                                container.className = 'cf-turnstile';
                                document.body.appendChild(container);
                            }

                            // Render Turnstile widget
                            window.turnstile.render(container, {
                                sitekey: config.captchaSiteKey,
                                callback: function(token) {
                                    resolve(token);
                                },
                                'error-callback': function() {
                                    reject(new Error('Turnstile validation failed'));
                                },
                                'expired-callback': function() {
                                    reject(new Error('Turnstile token expired'));
                                },
                                'timeout-callback': function() {
                                    reject(new Error('Turnstile validation timeout'));
                                }
                            });
                        } catch (error) {
                            reject(error);
                        }
                    }
                }, 100);

                // Timeout after 10 seconds
                setTimeout(() => {
                    clearInterval(checkTurnstile);
                    reject(new Error('Turnstile failed to load'));
                }, 10000);
            });
        }";
    }

    private string GenerateReCaptchaImplementation()
    {
        return @"

        /**
         * Load Google reCAPTCHA script
         */
        loadCaptcha: function(config) {
            if (document.getElementById('recaptcha-script')) {
                return; // Already loaded
            }

            const script = document.createElement('script');
            script.id = 'recaptcha-script';
            script.src = 'https://www.google.com/recaptcha/api.js?render=' + config.captchaSiteKey;
            script.async = true;
            script.defer = true;
            document.head.appendChild(script);

            console.log('SkyCmsContact: Google reCAPTCHA loaded');
        },

        /**
         * Get Google reCAPTCHA token
         * @param {Object} config - Configuration
         * @returns {Promise<string>} reCAPTCHA token
         */
        getCaptchaToken: async function(config) {
            return new Promise((resolve, reject) => {
                // Wait for reCAPTCHA to load
                const checkRecaptcha = setInterval(() => {
                    if (typeof window.grecaptcha !== 'undefined' && window.grecaptcha.ready) {
                        clearInterval(checkRecaptcha);

                        window.grecaptcha.ready(async () => {
                            try {
                                const token = await window.grecaptcha.execute(config.captchaSiteKey, { action: 'submit' });
                                resolve(token);
                            } catch (error) {
                                reject(error);
                            }
                        });
                    }
                }, 100);

                // Timeout after 10 seconds
                setTimeout(() => {
                    clearInterval(checkRecaptcha);
                    reject(new Error('reCAPTCHA failed to load'));
                }, 10000);
            });
        }";
    }
}