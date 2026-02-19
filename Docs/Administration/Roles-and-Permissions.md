---
title: Roles & Permissions (RBAC)
description: Role-Based Access Control and authorization system in SkyCMS
keywords: RBAC, roles, permissions, authorization, access-control, security
audience: [developers, administrators]
---

# Role-Based Access Control (RBAC) & Authorization

SkyCMS uses **ASP.NET Core Identity** with role-based authorization to control access to administrative and content management features. This guide explains the authorization model, available roles, permission matrix, and best practices for managing user access.

## Table of Contents
- [Authorization Model](#authorization-model)
- [Built-in Roles](#built-in-roles)
- [Permission Matrix](#permission-matrix)
- [Role Assignment](#role-assignment)
- [Creating Custom Roles](#creating-custom-roles)
- [Authorization at Different Levels](#authorization-at-different-levels)
- [Best Practices](#best-practices)
- [Troubleshooting Authorization Issues](#troubleshooting-authorization-issues)

## Authorization Model

SkyCMS implements **ASP.NET Core's declarative authorization** using role-based access control (RBAC). Authorization is enforced through:

1. **`[Authorize]` Attribute**: Applied at the controller or action method level
2. **Role-Based Checks**: Users must be in specific roles to access protected resources
3. **Automatic Role Initialization**: Required roles are created automatically on first launch
4. **Role Manager**: Administrators can create, manage, and delete custom roles via the admin interface

### How Authorization Works

When a user requests a protected action:
1. SkyCMS checks if the user is authenticated
2. SkyCMS verifies the user's assigned roles against the required roles for that action
3. Access is granted if the user is in at least one of the required roles
4. Access is denied with an HTTP 403 Forbidden response if roles don't match

## Built-in Roles

SkyCMS requires six predefined roles to function properly. These roles are automatically created when the application starts.

### Editor/CMS Roles

#### **Administrators**
- **Description**: Full system access and administrative control
- **Key Capabilities**:
  - Create, modify, and delete all roles
  - Manage user accounts and assign roles
  - Create and modify page templates and layouts
  - Configure system settings
  - Manage all content (pages, articles, etc.)
  - Access file manager with full permissions
  - Configure CDN and storage settings
  - View audit logs and diagnostics

#### **Editors**
- **Description**: Full content creation and management permissions
- **Key Capabilities**:
  - Create, edit, and publish pages and articles
  - Modify existing page layouts and templates (in some contexts)
  - Manage files and media through the file manager
  - View published and draft content
  - **Cannot**: Manage user accounts, system configuration, roles
  - **Cannot**: Create or modify templates/layouts (restricted context)

#### **Authors**
- **Description**: Content creation with limited publishing rights
- **Key Capabilities**:
  - Create and edit their own pages and articles
  - Submit content for review
  - Access file manager to upload media
  - View their own published content
  - **Cannot**: Publish content directly (must be reviewed by Editors or Administrators)
  - **Cannot**: Modify other users' content
  - **Cannot**: Access system settings or user management

#### **Reviewers**
- **Description**: Content review and approval capability
- **Key Capabilities**:
  - View all pages in the editor interface
  - Review content pending publication
  - Provide feedback on draft content
  - **Cannot**: Create or edit content
  - **Cannot**: Directly publish content
  - **Cannot**: Manage users or system settings

### Portal/Public Roles

#### **Authenticated**
- **Description**: For future use; represents any authenticated user
- **Current Usage**: Primarily for future extensibility; not actively used in core features

#### **Anonymous**
- **Description**: For future use; represents unauthenticated visitors
- **Current Usage**: Primarily for future extensibility; not actively used in core features

## Permission Matrix

This matrix shows which roles can access key SkyCMS features:

| Feature | Admin | Editor | Author | Reviewer |
|---------|-------|--------|--------|----------|
| **Editor Dashboard** | ✓ | ✓ | ✓ | ✓ |
| **Create Pages** | ✓ | ✓ | ✓ | ✗ |
| **Edit Pages** | ✓ | ✓ | Own | ✗ |
| **Publish Pages** | ✓ | ✓ | ✗ | ✗ |
| **Delete Pages** | ✓ | ✓ | ✗ | ✗ |
| **Manage Templates** | ✓ | ✓ | ✗ | ✗ |
| **Manage Layouts** | ✓ | ✓ | Limited | ✗ |
| **File Manager** | ✓ | ✓ | ✓ | ✗ |
| **Manage Users** | ✓ | ✗ | ✗ | ✗ |
| **Manage Roles** | ✓ | ✗ | ✗ | ✗ |
| **System Settings** | ✓ | ✗ | ✗ | ✗ |
| **View Content for Review** | ✓ | ✓ | ✓ | ✓ |

## Role Assignment

### Assigning Roles During Setup

The first user to register or log in is automatically assigned the **Administrators** role to bootstrap the system. Subsequent users require explicit role assignment.

### Assigning Roles in the Admin Interface

1. Navigate to **Administration → Users** in the SkyCMS editor
2. Select a user from the list
3. Under **Roles**, choose the appropriate role(s)
4. Save changes

Users can be assigned to **multiple roles** simultaneously. A user with both "Authors" and "Reviewers" roles can create content and review others' work.

### Assigning Roles via Code

When creating users programmatically:

```csharp
var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
var result = await userManager.AddToRoleAsync(user, "Editors");

if (result.Succeeded)
{
    // Role assignment successful
}
```

## Creating Custom Roles

In addition to the built-in roles, administrators can create custom roles for specialized workflows.

### Creating a Custom Role

1. Navigate to **Administration → Roles** in the SkyCMS editor
2. Click **Add New Role**
3. Enter a role name (e.g., "ContentReviewers", "SocialMediaManager")
4. Click **Create**

### Important Constraints

- **Protected Roles**: The following roles **cannot be deleted** and should not be modified:
  - `Administrators`
  - `Authors`
  - `Editors`
  - `Reviewers`
  - `Authenticated`
  - `Anonymous`

- **Custom Roles**: Custom roles you create are not protected and can be deleted if needed

### Using Custom Roles

Custom roles can be used in authorization code if developers extend the controllers. However, the standard SkyCMS features only recognize the built-in roles.

```csharp
[Authorize(Roles = "Administrators, CustomReviewers")]
public IActionResult SensitiveOperation()
{
    // Only Administrators and CustomReviewers can access this action
    return Ok();
}
```

## Authorization at Different Levels

### Controller-Level Authorization

Authorization applied to the entire controller affects all actions:

```csharp
[Authorize(Roles = "Administrators")]
public class RolesController : Controller
{
    // All actions in this controller require Administrators role
}
```

**Example Controllers**:
- `UsersController` - Restricted to `Administrators`
- `RolesController` - Restricted to `Administrators`

### Action-Level Authorization

Authorization can be more granular, applied to individual actions:

```csharp
[Authorize(Roles = "Administrators, Editors")]
public IActionResult ManageLayouts()
{
    // Only Administrators and Editors can access this action
}
```

**Example from EditorController**:
- `SavePageAsync()` - Requires `Administrators, Editors, Authors`
- `PublishPageAsync()` - Requires `Administrators, Editors`
- `DeletePageAsync()` - Requires `Administrators, Editors`
- `CreateTemplate()` - Requires `Administrators`

### Live Editor Hub

The Live Editor (real-time collaborative editing) requires specific roles:

```csharp
[Authorize(Roles = "Reviewers, Administrators, Editors, Authors")]
public class LiveEditorHub : Hub
{
    // Real-time editing only available to these roles
}
```

## Best Practices

### 1. Principle of Least Privilege

Assign users the **minimum role necessary** to perform their job:
- **Content creators** → `Authors` role
- **Content managers** → `Editors` role
- **Quality assurance** → `Reviewers` role
- **System administrators** → `Administrators` role only when necessary

### 2. Regular Role Audits

Periodically review user role assignments to:
- Identify users with excessive permissions
- Remove users who no longer need access
- Ensure inactive users are deactivated

### 3. Segregation of Duties

- **Don't combine conflicting roles** on a single user (e.g., developer + reviewer for critical systems)
- Use **multiple accounts** if a person needs different permission levels
- Document the business reason for multi-role assignments

### 4. Custom Role Naming

When creating custom roles, use **clear, descriptive names**:
- ✓ `VideoContentCreators`
- ✓ `LocalPublishers`
- ✗ `Group1`
- ✗ `temp_role`

### 5. Documenting Role Responsibilities

Maintain documentation of what each role can do:
- Update this documentation when adding custom roles
- Include specific responsibilities and use cases
- Share with team members to avoid confusion

### 6. Monitor Administrative Actions

- Log all role assignments and removals
- Audit who created which custom roles
- Review deletion of custom roles before they occur
- Enable application-level diagnostics (see [Troubleshooting](../Troubleshooting.md))

### 7. Protect Administrator Credentials

- Use **strong passwords** for Administrator accounts
- Enable **multi-factor authentication** (if configured)
- Limit the number of Administrators
- Remove Administrator access when no longer needed
- Monitor Administrator activity logs

## Troubleshooting Authorization Issues

### User Cannot Access Editor

**Symptoms**: User sees "Access Denied" or is redirected to login

**Resolution**:
1. Verify the user has at least one role assigned
2. Check that the required role matches the action requirements
3. Ensure the user's role hasn't been revoked
4. Try logging out and back in to refresh role claims

### "Access Denied" on Specific Action

**Symptoms**: User can access some features but not others

**Resolution**:
1. Check which roles are required for that action
   - Look for `[Authorize(Roles = "...")]` on the action
2. Verify the user is in the required role
3. Check if action-level authorization overrides controller-level
4. Confirm custom roles are properly spelled/matched

### Role Created But Not Working

**Symptoms**: Custom role created but authorization still fails

**Resolution**:
1. Verify the role name is spelled exactly (case-sensitive in some contexts)
2. Ensure the user is actually assigned to the custom role
3. Check that custom role is used in controller authorization attributes
4. Try a browser refresh or re-login to update role claims
5. Verify no caching layer is preventing role updates

### Missing Required Roles on Startup

**Symptoms**: Application errors about missing roles during startup

**Resolution**:
1. Check the `SetupNewAdministrator.cs` initialization
2. Verify database connectivity
3. Check logs for specific errors during role creation
4. Ensure all roles in `RequiredIdentityRoles` are properly defined
5. Review [Post-Installation](../Installation/Post-Installation.md) troubleshooting section

### User Has Role But Cannot Access

**Symptoms**: User is in the correct role but access is still denied

**Resolution**:
1. Clear browser cookies and cached authentication
2. Log out and log back in to refresh claims
3. Check for additional permission checks in the code beyond role-based authorization
4. Verify no custom authorization policies are more restrictive
5. Check application event logs for authorization failures

## Related Documentation

- [Authentication Overview](../Authentication-Overview.md) - User identity management
- [Post-Installation Configuration](../Installation/Post-Installation.md) - Security hardening after setup
- [Setup Wizard](../Installation/SetupWizard-Complete.md) - Initial administrator creation
- [Troubleshooting](../Troubleshooting.md) - Tracking authorization events

## Code References

> **Note**: The following source code files are located in the SkyCMS project repository, not in the published documentation.

- **Role Definition**: `Editor/Data/RequiredIdentityRoles.cs`
- **Role Initialization**: `Editor/Services/SetupNewAdministrator.cs`
- **Role Management UI**: `Editor/Controllers/RolesController.cs`
- **User Management**: `Editor/Controllers/UsersController.cs`
- **Content Editor**: `Editor/Controllers/EditorController.cs`
