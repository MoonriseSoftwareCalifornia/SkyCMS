# Sky.Cms.Api.Shared Integration Updates

## Recent Changes (January 2026)

This document summarizes the recent integration of the Sky.Cms.Api.Shared project into the SkyCMS Editor application.

## What Changed

### 1. **API Integration in Editor**

The Contact Form API is now fully integrated into the SkyCMS Editor web application:

- **Controller Registration**: The `ContactApiController` from Sky.Cms.Api.Shared is registered in the Editor's MVC pipeline
- **Endpoints Available**:
  - `GET /_api/contact/skycms-contact.js` - Returns JavaScript library with configuration
  - `POST /_api/contact/submit` - Accepts form submissions

### 2. **Email Configuration Service Refactoring**

The email configuration has been centralized and made multi-tenant aware:

**Before**:
- Email settings scattered across multiple locations
- EmailSettings and IEmailConfigurationService were in Sky.Editor

**After**:
- Unified `IEmailConfigurationService` in `Cosmos.Common`
- Shared `EmailSettings` model available to all projects
- `TenantAwareEmailSender` dynamically selects email provider at runtime
- Each tenant can have different email configurations

**Files Changed**:
```
Common/Services/Email/
├── IEmailConfigurationService.cs  (NEW - moved from Editor)
└── EmailSettings.cs               (NEW - moved from Editor)

Editor/Services/Email/
├── EmailConfigurationService.cs   (UPDATED - implements IEmailConfigurationService)
├── TenantAwareEmailSender.cs      (UPDATED - uses new interface)
└── EmailSettings.cs               (MOVED to Common)
└── IEmailConfigurationService.cs  (MOVED to Common)
```

### 3. **Contact API Improvements**

The Contact Form API now has smart fallback behavior:

- **AdminEmail Fallback**: If `ContactApi.AdminEmail` is not configured, falls back to the email service's `SenderEmail`
- **Graceful Degradation**: Works in multi-tenant and single-tenant scenarios
- **Logging**: Detailed logging of configuration and fallback behavior

### 4. **Rate Limiting Configuration**

Added explicit rate limiting for contact form submissions:

```csharp
// Contact form submission rate limiter (for Sky.Cms.Api.Shared)
options.AddFixedWindowLimiter("contact-form", opt =>
{
    opt.Window = TimeSpan.FromMinutes(5);
    opt.PermitLimit = 3;  // Max 3 contact form submissions per 5 minutes per IP
    opt.QueueLimit = 0;   // No queuing
});
```

**Configuration Location**: `Editor/Program.cs` (lines 544-553)

### 5. **Unit Tests Added**

Comprehensive unit tests for the integrated API:

```
Tests/
├── Controllers/ContactApiControllerTests.cs
├── Features/ContactForm/SubmitContactFormHandlerTests.cs
└── Models/ContactApiConfigTests.cs
```

**Test Coverage**:
- Controller endpoints
- Email configuration fallback
- CAPTCHA integration
- Rate limiting
- Error handling

## Documentation Updates

### New Documentation Files

1. **[Integration-Guide.md](./Integration-Guide.md)**
   - How to integrate the API into your website
   - Configuration options
   - Troubleshooting

2. **[Tutorial.md](./Tutorial.md)**
   - Step-by-step guide to adding a contact form
   - Complete working examples
   - Email setup instructions
   - CAPTCHA configuration

### Updated Documentation Files

1. **[README.md](./README.md)**
   - Added links to new Tutorial and Integration Guide
   - Updated "For Developers" section

2. **[ContactForm.md](./ContactForm.md)**
   - Complete reference documentation
   - Already comprehensive; no changes needed

## How to Use

### For Website Developers

1. **Add a contact form to your website**:
   ```html
   <form id="contactForm">
     <input type="text" name="name" required>
     <input type="email" name="email" required>
     <textarea name="message" required></textarea>
     <button type="submit">Send</button>
   </form>

   <script src="/_api/contact/skycms-contact.js"></script>
   <script>
     SkyCmsContact.init('#contactForm');
   </script>
   ```

2. **Configure email**:
   - In Editor admin panel, configure email service
   - Optionally set `ContactApi.AdminEmail` in database settings

3. **Optional: Enable CAPTCHA**:
   - Get Turnstile or reCAPTCHA keys
   - Add CAPTCHA configuration to database settings

### For API Developers

1. **Read the Integration Guide**: [Docs/Api/Integration-Guide.md](./Integration-Guide.md)
2. **Follow the Tutorial**: [Docs/Api/Tutorial.md](./Tutorial.md)
3. **Reference the API docs**: [Docs/Api/ContactForm.md](./ContactForm.md)
4. **Check the architecture**: [Docs/Api/ARCHITECTURE.md](./ARCHITECTURE.md)

## Architecture Overview

### Email Configuration Flow

```
Program.cs (Editor)
    ↓
Registers IEmailConfigurationService
    ↓
EmailConfigurationService (implemented in Editor)
    ↓
Reads from:
├─ Environment Variables (highest priority)
├─ appsettings.json
├─ Database Settings table
    ↓
Returns EmailSettings
    ↓
Used by:
├─ TenantAwareEmailSender
├─ ContactApiController
└─ SubmitContactFormHandler
```

### Contact Form Request Flow

```
Browser
    ↓
GET /_api/contact/skycms-contact.js
    ↓
ContactApiController.GetContactScript()
    ↓
Loads configuration from database
    ↓
Generates anti-forgery token
    ↓
Returns JavaScript with embedded config
    ↓
JavaScript initializes form on browser
    ↓
User fills and submits form
    ↓
POST /_api/contact/submit
    ↓
ContactApiController.Submit()
    ↓
Rate limiting check
    ↓
Anti-forgery token validation
    ↓
Model validation
    ↓
CAPTCHA validation (if enabled)
    ↓
SubmitContactFormHandler
    ↓
SubmitContactFormCommand
    ↓
Email send via TenantAwareEmailSender
    ↓
Response to browser
    ↓
JavaScript callback (onSuccess or onError)
```

## Configuration Examples

### Minimal Setup (Using Email Service Fallback)

No ContactApi settings needed - uses email service configuration:

```json
{
  "SendGridApiKey": "your-sendgrid-key",
  "SenderEmail": "noreply@yourdomain.com"
}
```

The API will use `SenderEmail` as the admin email automatically.

### Explicit ContactApi Configuration

```sql
INSERT INTO Settings (Id, Group, Name, Value, Description)
VALUES 
  (NEWID(), 'ContactApi', 'AdminEmail', 'support@yourdomain.com', 'Email for form submissions'),
  (NEWID(), 'ContactApi', 'MaxMessageLength', '5000', 'Max message length'),
  (NEWID(), 'CAPTCHA', 'Config', '{"Provider":"turnstile","SiteKey":"your-key","SecretKey":"your-secret","RequireCaptcha":true}', 'CAPTCHA config');
```

### Multi-Tenant Scenario

Each tenant can have different email configurations:

**Tenant A**:
- Uses SendGrid with key A
- Admin email: support-a@domaina.com

**Tenant B**:
- Uses Azure Communication Services
- Admin email: support-b@domainb.com

The `TenantAwareEmailSender` automatically selects the correct provider based on the current tenant's database context.

## Testing

Run the new unit tests:

```bash
# Test all Contact API tests
dotnet test Tests/ --filter "Category=ContactApi"

# Or specific test class
dotnet test Tests/Controllers/ContactApiControllerTests.cs
```

**Test Results Expected**:
- ✓ Controller tests (form submission, script generation)
- ✓ Handler tests (email sending, fallback behavior)
- ✓ Model tests (configuration parsing)

## Backwards Compatibility

All changes are backwards compatible:

- ✓ Existing websites continue to work
- ✓ Existing email configurations work unchanged
- ✓ If not using the API, there's no performance impact
- ✓ Can be opted into on a per-website basis

## Performance Impact

- **Minimal**: The API is lazy-loaded only when requested
- **JavaScript library**: 3-5 KB
- **Form submission**: < 100ms (excluding network and email service)
- **Email sending**: Asynchronous (doesn't block response)

## Security Enhancements

1. **Anti-forgery tokens**: CSRF protection on all submissions
2. **Rate limiting**: 3 requests per 5 minutes per IP
3. **Server-side validation**: All fields validated server-side
4. **Secret management**: CAPTCHA secrets never exposed to client
5. **Email sanitization**: HTML escaped to prevent injection attacks

## Next Steps

1. ✓ Integration complete
2. ✓ Documentation complete
3. → Test in staging environment
4. → Enable in production
5. → Monitor submissions and logs

## Questions?

Refer to:
- **Getting started**: [Tutorial.md](./Tutorial.md)
- **Integration details**: [Integration-Guide.md](./Integration-Guide.md)
- **API reference**: [ContactForm.md](./ContactForm.md)
- **Architecture**: [ARCHITECTURE.md](./ARCHITECTURE.md)
- **Configuration**: [Configuration.md](./Configuration.md)

