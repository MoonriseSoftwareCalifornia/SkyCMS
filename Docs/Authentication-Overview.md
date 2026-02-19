---
audience: [developers, administrators]
title: Authentication & Authorization Overview
description: Flexible authentication system with ASP.NET Core Identity, Azure AD, and Azure B2C support
keywords: authentication, authorization, identity, Azure-AD, B2C, roles, permissions
audience: [developers, administrators]
version: 2.0
updated: 2025-12-20
canonical: /Authentication-Overview.html
aliases: []
scope:
  platforms: [azure, local, other-oidc]
  tenancy: [single, multi]
status: stable
chunk_hint: 360
---

# Authentication & Authorization in SkyCMS

SkyCMS uses a flexible authentication system built on ASP.NET Core Identity, supporting both traditional username/password authentication and cloud-based identity providers like Azure Active Directory (Azure AD) and Azure B2C.

---

## Key facts {#key-facts}

- Supports local auth, Azure AD, Azure B2C, and generic OpenID Connect/OAuth 2.0.
- Editor app requires authentication; Publisher can be public or gated per page.
- Claims-based RBAC with customizable roles; tokens for API, sessions for browser.
- Use managed identity or app registrations for Azure providers; never store secrets in repo.

## Table of Contents {#table-of-contents}

- [Overview](#overview)
- [Authentication Methods](#authentication-methods)
- [Key Concepts](#key-concepts)
- [Getting Started](#getting-started)
- [For Users](#for-users)
- [For Developers](#for-developers)
- [Configuration](#configuration)
- [Troubleshooting](#troubleshooting)

---

## Overview {#overview}

Authentication ensures users are who they claim to be. Authorization ensures authenticated users can only access resources they're permitted to use.

SkyCMS separates authentication into:

- **Editor Application** - Protects content management interface (requires authentication)
- **Publisher Application** - Public-facing website (configurable per page)
- **API Authentication** - Token-based access for programmatic use

---

## Authentication Methods {#authentication-methods}

### 1. Local Authentication (Built-in) {#local-authentication}

Traditional username and password authentication managed by SkyCMS.

**When to use:**
- Small teams with few editors
- No existing identity provider
- Simple deployment without cloud dependencies
- Testing and development

**Configuration:**
- Users created and managed through SkyCMS admin panel
- Passwords hashed and stored in database
- Password reset via email
- Basic role-based access control

**Strengths:**
- Simple setup and management
- No external dependencies
- Complete control over user data

**Limitations:**
- Users must remember credentials
- Password complexity policies must be manually enforced
- No single sign-on (SSO) across applications
- Limited to SkyCMS users only

---

### 2. Azure Active Directory (Azure AD) {#azure-ad}

Enterprise identity provider for organizations using Microsoft cloud services.

**When to use:**
- Organization already uses Microsoft 365, Azure, or other Microsoft services
- Need single sign-on (SSO) across multiple applications
- Organization has identity compliance requirements
- Enterprise security policies required

**Features:**
- Automatic user provisioning
- Multi-factor authentication (MFA)
- Conditional access policies
- Audit logging
- Works with existing organizational user directories

**Setup:**
1. Register SkyCMS application in Azure AD tenant
2. Configure client ID, secret, and redirect URIs
3. Link SkyCMS to Azure AD
4. Users sign in with organizational account

**Related:**
- Azure AD setup guide (coming soon)
- Azure AD documentation

---

### 3. Azure B2C {#azure-b2c}

Consumer identity provider for public-facing applications.

**When to use:**
- Public-facing platform with external users
- Need social login (Google, Facebook, etc.)
- Require custom user attributes
- Multi-tenant SaaS application

**Features:**
- Social and email sign-in
- Custom authentication flows
- User profile management
- Self-service password reset

**Strengths:**
- Supports external users
- Minimal development effort
- Flexible customization

**Limitations:**
- Additional Azure subscription cost
- More complex setup than local auth

**Related:**
- Azure B2C integration guide (coming soon)
- Azure B2C documentation

---

### 4. OpenID Connect / OAuth 2.0 {#openidconnect}

Generic protocol-based authentication for integrating with other identity providers.

**When to use:**
- Using a non-Microsoft identity provider (Auth0, Okta, etc.)
- Custom identity provider
- Specific organizational requirements

**Configuration:**
- Requires metadata endpoint from identity provider
- Configure client ID, secret, scopes
- Map claims to SkyCMS user properties

**Related:**
- Generic OAuth 2.0 setup (coming soon)

---

## Key Concepts {#key-concepts}

### Authentication vs. Authorization {#authentication-vs-authorization}

| Aspect | Authentication | Authorization |
|--------|-----------------|-----------------|
| **Definition** | Confirming user identity | Controlling access to resources |
| **Question** | "Are you who you claim to be?" | "Are you allowed to do this?" |
| **Examples** | Login credentials, MFA | Admin access, view specific pages |
| **In SkyCMS** | Logging in to editor | Role-based page/section access |

### Roles & Permissions {#roles-and-permissions}

SkyCMS uses role-based access control (RBAC):

- **Admin** - Full access to all content and settings
- **Editor** - Create, edit, and publish content
- **Contributor** - Create and edit content, cannot publish
- **Viewer** - Read-only access (future enhancement)

Custom roles can be created for specific needs.

### Claims & Tokens {#claims-and-tokens}

When a user authenticates, they receive claims (assertions about their identity):

```
Claims Example:
- sub (subject): Unique user ID
- email: user@example.com
- name: User Name
- roles: ["Editor", "Admin"]
```

These claims are packaged into a security token (usually a JWT) that proves authentication.

### Sessions vs. Tokens {#sessions-vs-tokens}

- **Sessions** (traditional) - Server stores user state; browser stores session ID
- **Tokens** (modern) - Token contains user state; client stores token
- **SkyCMS** - Uses session cookies for browser-based access; tokens for API

---

## Getting Started {#getting-started}

### For New Users {#for-new-users}

When signing into the SkyCMS editor for the first time:

1. **Go to Editor URL** - `https://yourdomain.com/editor`
2. **Choose Authentication Method**
   - Click login button
   - Select your organization's identity provider (Azure AD, etc.) or enter local credentials
3. **Grant Permissions** - Review and grant requested permissions
4. **You're Logged In** - Start creating and editing content

### For Site Administrators {#for-site-administrators}

To set up authentication:

1. **Choose Authentication Method** - Decide which method fits your organization
2. **Configure in SkyCMS**
   - Go to Settings → Authentication
   - Select method and enter configuration details
3. **Create Users** - Add editors and contributors
4. **Assign Roles** - Set permissions for each user
5. **Test** - Verify login works correctly

### For DevOps / Deployment {#for-devops}

See [Configuration](#configuration) section below for environment variables and connection string setup.

---

## For Users {#for-users}

### How to Log In {#how-to-log-in}

1. Navigate to your SkyCMS editor URL: `https://yourdomain.com/editor`
2. Click "Sign In"
3. Enter credentials or select organization sign-in method
4. If using Azure AD or B2C, follow your organization's MFA process if required
5. You're logged into the SkyCMS editor

### Resetting Your Password {#resetting-password}

**Local Authentication:**
1. Click "Forgot Password" on login screen
2. Enter your email address
3. Check email for reset link
4. Click link and set new password
5. Log in with new password

**Azure AD / B2C:**
- Use your organization's standard password reset process
- This is typically managed outside SkyCMS

### Session Timeout {#session-timeout}

- Editor sessions timeout after 30 days of inactivity (configurable)
- You'll be returned to login screen
- Just log back in to continue

---

## For Developers {#for-developers}

### Authentication Architecture {#authentication-architecture}

SkyCMS uses ASP.NET Core Identity with pluggable provider support:

```
User Request
    ↓
[Authorization Middleware]
    ↓
Check Claims/Roles
    ↓
Access Granted/Denied
```

### Available Libraries {#available-libraries}

- **[AspNetCore.Identity.FlexDb](./Components/AspNetCore.Identity.FlexDb.md)** - Flexible identity framework supporting multiple database providers
- **Microsoft.AspNetCore.Authentication** - ASP.NET Core authentication
- **Microsoft.AspNetCore.Authentication.AzureAD.UI** - Azure AD integration
- **IdentityModel** - OIDC/OAuth 2.0 helpers

### Customizing Claims & Roles {#customizing-claims-roles}

To add custom claims or roles:

```csharp
// In your claims transformation class
public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
{
    var identity = (ClaimsIdentity)principal.Identity;
    
    // Add custom claims
    identity.AddClaim(new Claim("department", "engineering"));
    identity.AddClaim(new Claim(ClaimTypes.Role, "CustomRole"));
    
    return principal;
}
```

### Custom Authorization Policies {#custom-authorization-policies}

Define policies for specific access patterns:

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("CanPublish", policy =>
        policy.Requirements.Add(new CanPublishRequirement()));
    
    options.AddPolicy("IsAdmin", policy =>
        policy.RequireRole("Admin"));
});
```

### Integrating Custom Identity Providers {#integrating-custom-providers}

1. Create authentication handler for your provider
2. Register in `Program.cs`
3. Map claims to SkyCMS user properties
4. Test authentication flow

See [Identity Framework Documentation](./Components/AspNetCore.Identity.FlexDb.md) for implementation details.

---

## Configuration {#configuration}

### Environment Variables {#environment-variables}

Configure authentication in `appsettings.json` or environment variables:

```json
{
  "Authentication": {
    "Method": "AzureAD",
    "AllowLocalAuth": false,
    "SessionTimeoutMinutes": 43200,
    "RequireMfa": false
  },
  "AzureAD": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  }
}
```

### Supported Configuration Values {#supported-config-values}

| Setting | Values | Default | Purpose |
|---------|--------|---------|---------|
| `Method` | `LocalAuth`, `AzureAD`, `AzureB2C`, `OpenIDConnect` | `LocalAuth` | Which authentication method to use |
| `AllowLocalAuth` | `true`, `false` | `true` | Allow local username/password |
| `RequireMfa` | `true`, `false` | `false` | Require multi-factor authentication |
| `SessionTimeoutMinutes` | 1-525600 | 43200 (30 days) | How long sessions last |

### Securing Secrets {#securing-secrets}

Never commit secrets to version control:

```powershell
# Use user secrets for development
dotnet user-secrets set "AzureAD:ClientSecret" "your-secret"

# Use Azure Key Vault for production
# See CONTRIBUTING.md for details
```

---

## Troubleshooting {#troubleshooting}

## FAQ {#faq}

- **When should I choose Azure AD vs Azure B2C?** Use Azure AD for workforce/enterprise SSO; use Azure B2C for public/external users and social logins.
- **Can I keep local auth enabled alongside Azure AD?** Yes; set `AllowLocalAuth=true` if you want a fallback local admin, otherwise set it to false.
- **How do I enforce MFA?** Enforce via your IdP (Azure AD Conditional Access/B2C policies); SkyCMS consumes the IdP’s MFA result.

<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "FAQPage",
  "mainEntity": [
    {"@type": "Question", "name": "When should I choose Azure AD vs Azure B2C?", "acceptedAnswer": {"@type": "Answer", "text": "Use Azure AD for workforce SSO and enterprise policies. Use Azure B2C for public or external users and social logins."}},
    {"@type": "Question", "name": "Can I keep local auth enabled alongside Azure AD?", "acceptedAnswer": {"@type": "Answer", "text": "Yes. Set AllowLocalAuth=true if you want a fallback local admin account; set it to false to require Azure AD only."}},
    {"@type": "Question", "name": "How do I enforce MFA?", "acceptedAnswer": {"@type": "Answer", "text": "Enforce MFA in your identity provider (Azure AD Conditional Access or B2C policies). SkyCMS honors the IdP's MFA result."}}
  ]
}
</script>

### "Invalid Credentials" Error

**Causes:**
- Wrong username/password
- User account disabled
- Account locked after too many attempts

**Solution:**
- Verify credentials are correct
- Check account status in Settings → Users
- Wait 15 minutes if account is locked, then try again

### "Access Denied" Error

**Causes:**
- User doesn't have required role
- User doesn't have permission for this resource
- Session expired

**Solution:**
- Verify user role in Settings → Users
- Check page/resource permissions
- Log out and log back in

### Azure AD Login Fails

**Causes:**
- Invalid client ID or secret
- Wrong Azure AD tenant
- Redirect URI not configured in Azure
- User not assigned to SkyCMS application

**Solution:**
- Verify Azure AD application configuration
- Check redirect URIs match SkyCMS URLs
- Assign users/groups to SkyCMS application in Azure portal
- Check application logs for detailed error messages

### Sessions Expire Too Quickly

**Solution:**
- Increase `SessionTimeoutMinutes` in configuration
- Check if reverse proxy has shorter timeout
- Verify browser allows session cookies

### Users Can't Log In After Provider Change

**Causes:**
- Old authentication method still configured
- User accounts not migrated to new provider
- Cached credentials or sessions

**Solution:**
- Clear browser cache and cookies
- Log out completely (close all browser windows)
- Verify new authentication method is configured
- Check that users exist in new identity provider

### MFA Not Working

**Causes:**
- MFA configuration incorrect
- User not enrolled in MFA
- Authenticator app out of sync

**Solution:**
- Verify MFA requirement is correctly configured
- User should enroll in MFA (check identity provider settings)
- Re-sync authenticator app time if using TOTP

---

## See Also

- **[Learning Paths: Developer](./LEARNING_PATHS.md#developer)** - Authentication setup for developers
- **[Learning Paths: Decision Maker](./LEARNING_PATHS.md#decision-maker--manager)** - Authentication strategy and planning
- **[Components: Identity Framework](./Components/AspNetCore.Identity.FlexDb.md)** - Detailed identity implementation
- **[CONTRIBUTING.md: Secrets Management](./CONTRIBUTING.md)** - Managing secrets safely
- **[Publishing Overview](./Publishing-Overview.md)** - Authorization and access control for publishing
- **[Troubleshooting Guide](./Troubleshooting.md)** - General troubleshooting
- **[Configuration: Database](./Configuration/Database-Overview.md)** - Where user data is stored
- **[Main Documentation Hub](./index.md)** - Browse all documentation

---

## Related Documentation (To Be Created)

- Authentication setup guides for Azure AD, B2C, OAuth 2.0
- API authentication and token management
- Custom claim mappings and role definitions
- Multi-tenant authentication patterns
- Single sign-on (SSO) configuration

---

**Last Updated:** December 17, 2025  
**Owner:** @toiyabe
