---
title: Setup Wizard - Step 2 Admin
description: Create administrator account in setup wizard
keywords: setup-wizard, admin, administrator, user, configuration
audience: [developers, administrators]
---

# Setup Wizard: Step 2 - Administrator Account

[← Storage](./SetupWizard-Step1-Storage.md) | **Step 2 of 6** | [Next: Publisher Settings →](./SetupWizard-Step3-Publisher.md)

---

## Create Administrator Account

Create the first administrator account that will have full access to manage SkyCMS.

![Admin account screen](../images/screenshots/setup-wizard-admin.webp)

---

## Fields

### Administrator Email (Required)

The email address for the administrator account. This will be used to log in.

**Field Name**: `AdminEmail`  
**Required**: ✅ Yes  
**Format**: Valid email address  
**Example**: `admin@mywebsite.com`

**Notes**:
- This email will be used for login
- Must be unique (cannot conflict with existing users)
- Used as default sender email if email provider is configured

### Password (Required)

The password for the administrator account.

**Field Name**: `AdminPassword`  
**Required**: ✅ Yes  
**Minimum Length**: 8 characters

**Password Requirements**:
- ✅ At least one **uppercase** letter (A-Z)
- ✅ At least one **lowercase** letter (a-z)
- ✅ At least one **digit** (0-9)
- ✅ At least one **special character** (!@#$%^&*)

**Example valid passwords**:
- `MyP@ssw0rd`
- `Admin123!`
- `Secure#Pass2024`

### Confirm Password (Required)

Re-enter the password to confirm it was typed correctly.

**Field Name**: `AdminPasswordConfirm`  
**Required**: ✅ Yes  
**Must Match**: AdminPassword field

---

## Actions

### "Next" Button

Proceeds to **Step 3: Publisher Settings** after validation.

**Validation**:
- Email is required and must be valid format
- Password is required and meets complexity requirements
- Password and confirmation must match
- Email doesn't already exist in database

---

## Password Strength Indicator

The wizard displays a real-time password strength indicator as you type:

- 🔴 **Weak** - Does not meet requirements
- 🟡 **Fair** - Meets minimum requirements
- 🟢 **Strong** - Exceeds minimum requirements

**Aim for "Strong"** by using:
- Mix of upper and lowercase
- Multiple numbers and special characters
- Length of 12+ characters

---

## Security Best Practices

### Choose a Strong Password

✅ **DO**:
- Use a password manager to generate and store the password
- Use 12+ characters
- Use a unique password (not reused from other sites)
- Include mix of character types

❌ **DON'T**:
- Use common passwords (`Password123!`, `Admin2024!`)
- Use personal information (birthdays, names)
- Share the password with anyone
- Write the password down

### Email Address Recommendations

- Use a dedicated admin email (e.g., `admin@mywebsite.com`)
- Ensure the email account is secure (2FA enabled)
- Use an email you have reliable access to
- Consider using a group email if managing with a team

---

## What Gets Created

When this step completes, the system:

1. **Creates Identity User**
   - Email stored in ASP.NET Core Identity tables
   - Password hashed using PBKDF2 (secure)
   - User ID generated automatically

2. **Assigns Administrator Role**
   - User added to "Administrators" role
   - Full permissions granted automatically

3. **Sets Admin Email Config**
   - Email saved as `AdminEmail` in system settings
   - Used as default sender for system emails

---

## Troubleshooting

### "Email address is required"

**Solution**: Enter a valid email address in the format `user@domain.com`

### "Password does not meet requirements"

**Causes**:
- Too short (less than 8 characters)
- Missing uppercase letter
- Missing lowercase letter
- Missing number
- Missing special character

**Solution**: Create a password that meets all requirements listed above.

### "Passwords do not match"

**Solution**: Ensure the password and confirmation fields are identical (check for typos).

### "Email already exists"

**Cause**: An administrator account with this email was already created.

**Solutions**:
1. Use a different email address
2. Or, reset the database and start setup over
3. Or, complete setup and use the existing account

### "Failed to create administrator account"

**Common Causes**:
- Database connection issue
- Database permissions insufficient
- Identity tables not created

**Solution**:
- Check application logs for detailed error
- Verify database connection string is correct
- Ensure database user has create/write permissions

---

## What Happens Next

After clicking **Next**, you'll proceed to:

**[Step 3: Publisher Settings →](./SetupWizard-Step3-Publisher.md)**

The administrator account is created immediately and saved to the database.

---

## See Also

- **[Setup Wizard Overview](./SetupWizard.md)** - Complete wizard guide
- **[Authentication Overview](../Authentication-Overview.md)** - User authentication details
- **[← Previous: Storage](./SetupWizard-Step1-Storage.md)**
- **[Next: Publisher Settings →](./SetupWizard-Step3-Publisher.md)**
