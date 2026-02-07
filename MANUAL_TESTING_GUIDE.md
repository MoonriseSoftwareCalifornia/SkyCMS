# Manual Integration Testing Guide - Setup Wizard

## ?? Objective
Verify the complete setup wizard flow from initial page through completion, ensuring draft state management, navigation, authorization, and database persistence all work correctly.

---

## ?? Pre-Test Checklist

- [ ] Close all instances of the solution in Visual Studio
- [ ] Run `dotnet build SkyCMS.sln` ? verify **Build successful**
- [ ] Open solution fresh in Visual Studio
- [ ] **IMPORTANT**: Delete any existing draft state:
  ```sql
  DELETE FROM Settings WHERE [Group]='SETUP' AND [Name]='DRAFT_STATE';
  ```

---

## ?? Test 1: Complete Basic Wizard Flow (15 minutes)

### Breakpoints to Set
1. **Editor\Areas\Setup\Pages\Index.cshtml.cs** ? `OnGetAsync()` line 1
2. **Editor\Areas\Setup\Pages\Step1_Storage.cshtml.cs** ? `OnPostNextAsync()` line 1  
3. **Editor\Areas\Setup\Pages\Summary.cshtml.cs** ? `OnGetAsync()` line 1
4. **Editor\Services\Setup\SetupService.cs** ? `SaveDraftStateAsync()` line 1

### Steps

#### **A. Start at Welcome Page (2 min)**
1. Press **F5** to start debugging
2. Navigate to `https://localhost:5001/Setup/Index` (or `http://...` if not HTTPS)
3. **Breakpoint hit**: Verify `IsSetupCompleteAsync()` called
4. Expected: **Welcome to SkyCMS Setup Wizard** page loads
   - Shows pre-configured items (if any)
   - Has "Get Started" or "Next" button

#### **B. Navigate to Storage Step (3 min)**
1. Click "Next" button ? navigate to `Step1_Storage`
2. Page loads with form fields:
   - ? `StorageType` (radio buttons or dropdown)
   - ? `StorageConnectionString` (text input)
   - ? `BlobPublicUrl` (text input, default "/")
   - ? Navigation buttons (Back/Next)

3. **Fill the form** with test values:
   ```
   StorageType: AzureBlob (or any option)
   StorageConnectionString: DefaultEndpointsProtocol=https;AccountName=test;
   BlobPublicUrl: https://test.blob.core.windows.net/
   ```

#### **C. Submit Storage Form (4 min)**
1. Click **Next** button
2. **Breakpoint hit**: `OnPostNextAsync()` executes
3. Watch **Debug Output** for:
   ```
   [Information] Sky.Editor.Services.Setup.SetupService: Updated storage configuration for setup {SetupId}
   [Information] Sky.Editor.Services.Setup.SetupService: Updated current step to 2 for setup {SetupId}
   ```
4. **Verify Database** (open SQL Server Management Studio or Azure Data Studio):
   ```sql
   SELECT * FROM Settings 
   WHERE [Group]='SETUP' AND [Name]='DRAFT_STATE'
   LIMIT 1
   ```
   - ? Should have one row with Value = JSON containing:
     - `"Id": "{SetupId}"`
     - `"StorageConnectionString": "DefaultEndpoint..."`
     - `"CurrentStep": 2`
     - `"BlobPublicUrl": "https://test.blob..."`

#### **D. Redirect to Summary (3 min)**
1. **Expected Redirect**: Browser navigates to `/Setup/Summary`
2. **Breakpoint hit**: `Summary.cshtml.cs` `OnGetAsync()` executes
3. Page displays:
   - ?? **Critical Configuration** warning banner
   - ?? Table with 5 rows:
     - Database Connection | ?????... | ?? Not Configured
     - Storage Connection | ?????... | ?? Configured
     - Blob Public URL | https://test.blob.core.windows.net | ?? Configured
     - Email Provider | (not configured) | ?? Optional
     - Publisher URL | (not configured) | ?? Not Configured
   - Buttons: **Go Back & Edit** | **Complete Setup**

#### **E. Complete Setup (3 min)**
1. Click **Complete Setup** button
2. Watch Debug Output for:
   ```
   [Information] Starting setup completion for {SetupId}
   [Information] Retrieving setup configuration...
   [Information] ? Setup configuration retrieved
   [Information] Creating administrator account...
   [Information] ? Administrator account created
   [Information] Saving settings to database...
   [Information] ? Settings saved
   [Information] ? Committed state saved
   [Information] ? Draft state cleaned up
   [Information] Setup completed successfully for {SetupId}
   ```

3. **Verify Database Changes** (critical):
   ```sql
   -- Should be DELETED (cleaned up)
   SELECT COUNT(*) FROM Settings 
   WHERE [Group]='SETUP' AND [Name]='DRAFT_STATE'
   -- Expected: 0

   -- Should exist (final committed state)
   SELECT * FROM Settings 
   WHERE [Group]='SYSTEM' AND [Name]='SETUP_WIZARD_STATE'
   -- Expected: 1 row with Value = JSON

   -- Should be set to false
   SELECT * FROM Settings 
   WHERE [Group]='SYSTEM' AND [Name]='AllowSetup'
   -- Expected: Value = 'false'
   ```

4. **Expected Redirect**: Browser goes to `/` (home page)
5. **Success**: Page loads (not setup wizard)

---

## ?? Test 2: Back Button Navigation (10 minutes)

### Prerequisites
- Completed Test 1 successfully
- Delete draft state again: `DELETE FROM Settings WHERE [Group]='SETUP' AND [Name]='DRAFT_STATE'`

### Steps

1. Navigate back to `/Setup/Index`
   - **Should see**: Welcome page (setup already complete, so this may redirect)
   
2. **If redirected to home**: Try accessing as admin:
   - Login with admin account (created in Test 1)
   - Navigate to `/Setup/Index` again
   - **Expected**: `[RequireSetupOrAdmin]` allows access (admin post-setup access)

3. Click "Next" ? Enter `/Setup/Step1_Storage`
4. Fill form with **different values**:
   ```
   StorageConnectionString: DefaultEndpointsProtocol=https;AccountName=test2;
   BlobPublicUrl: https://test2.blob.core.windows.net/
   ```

5. **Click "Back" button**
   - Browser should navigate back to `/Setup/Index`
   - Verify URL changed to `/Setup`

6. **Click "Next" again** ? `/Setup/Step1_Storage`
   - **CRITICAL**: Form fields should still have **original values** (not your step 5 values)
   - This proves draft state was persisted when you went back
   - Verify in database: draft still exists with original values

---

## ?? Test 3: Authorization & Admin Access (10 minutes)

### Setup Complete Already (from Test 1)

1. **Logout** current user (if logged in)

2. Try to access `/Setup/Index`
   - **Expected**: Redirected to login or error page (setup complete, not admin)

3. **Login as admin** (email from Test 1 step E)
   - Username: `admin@example.local` (or email you set)
   - Password: Password you set in admin step

4. Navigate to `/Setup/Index`
   - **Expected**: Page loads ? (`[RequireSetupOrAdmin]` allows admin)
   - **Debug**: Breakpoint in Index.cshtml.cs OnGetAsync

5. Start modifying settings (fill next step partially)

6. Go back and forth:
   - Click Back ? click Next
   - **Verify**: Values are preserved (draft state working)

7. **Make a different change** in Step1_Storage:
   - Change BlobPublicUrl to different value
   - Click Next ? Summary
   - **Verify**: New value shows in Summary table

---

## ?? Test 4: Sensitive Field Masking (5 minutes)

### Prerequisites
- Currently on Step1_Storage page (from any test)
- **Have a connection string visible** in StorageConnectionString field

### Steps

1. **Look at the StorageConnectionString field**
   - If visible: `DefaultEndpointsProtocol=https;AccountName=...`
   - Should see **Reveal button** [???] next to field

2. Click **Reveal button** [???]
   - Connection string should change to:
   - `?????...?????` (bullet points, fully masked)
   - Button should change to **Hide** [???????]
   - Open browser DevTools (F12) ? Network tab
   - **Should NOT see** full value in responses

3. Click **Hide** again
   - String should show masked form: `Default...?????...ets`
   - (First 10 + last 10 characters visible, middle masked)

4. Click **Copy button** [??]
   - Value should be copied to clipboard (actual connection string)
   - Button should change to **Copied** temporarily
   - Paste in Notepad: `Ctrl+V`
   - **Should see**: Full connection string (actual value, not masked)

---

## ?? Test 5: Database State Validation (5 minutes)

### After Test 1 Completes

Run these queries to verify all state transitions:

```sql
-- ? Verify Draft State was DELETED after completion
SELECT COUNT(*) AS DraftCount FROM Settings 
WHERE [Group]='SETUP' AND [Name]='DRAFT_STATE';
-- Expected: 0

-- ? Verify Committed State CREATED
SELECT * FROM Settings 
WHERE [Group]='SYSTEM' AND [Name]='SETUP_WIZARD_STATE';
-- Expected: 1 row, Value contains { "Id": "...", "IsComplete": true, ... }

-- ? Verify AllowSetup set to false
SELECT * FROM Settings 
WHERE [Group]='SYSTEM' AND [Name]='AllowSetup';
-- Expected: Value = 'false'

-- ? Verify Critical Settings Saved
SELECT * FROM Settings WHERE [Group] IN ('STORAGE', 'PUBLISHER', 'EMAIL') AND [Name] IN ('StorageConnectionString', 'BlobPublicUrl', 'PublisherUrl', 'AdminEmail');
-- Expected: Multiple rows with configured values

-- ? Verify Admin Account Created
SELECT COUNT(*) AS AdminCount FROM AspNetUsers 
WHERE UserName = 'admin@example.local';
-- Expected: 1

-- ? Verify Admin Role Assigned
SELECT u.UserName, r.Name AS RoleName
FROM AspNetUsers u
JOIN AspNetUserRoles ur ON u.Id = ur.UserId
JOIN AspNetRoles r ON ur.RoleId = r.Id
WHERE r.Name = 'Administrators';
-- Expected: Admin user listed

-- ? Verify Default Layout Created
SELECT COUNT(*) AS LayoutCount FROM Layout;
-- Expected: >= 1

-- ? Verify Home Page Created
SELECT COUNT(*) AS HomePageCount FROM Article WHERE UrlPath = 'root';
-- Expected: 1
```

---

## ?? Expected Outcomes Summary

| Test | Expected Result | Pass? |
|------|-----------------|-------|
| Welcome page loads | ? Setup wizard displays | [ ] |
| Navigation to Step1 | ? Form loads with fields | [ ] |
| Form submission | ? Draft state created in DB | [ ] |
| Redirect to Summary | ? Critical settings visible | [ ] |
| Setup completion | ? Draft deleted, committed created | [ ] |
| Back button works | ? Draft preserved, redirects | [ ] |
| Admin access | ? [RequireSetupOrAdmin] enforces | [ ] |
| Sensitive masking | ? Values masked, reveal works | [ ] |
| Database validation | ? All state transitions correct | [ ] |

---

## ?? Troubleshooting

| Error | Solution |
|-------|----------|
| "Setup is already complete" after first completion | ? Expected - delete draft + try login as admin |
| Form fields empty on back | ? Draft state not loaded - check DB queries |
| Breakpoint never hits | Check URL is correct, not routing elsewhere |
| Sensitive field showing plaintext | Check setup-sensitive-fields.js is loaded (DevTools > Sources) |
| Database changes not saved | Check AllowSetting is not forcing read-only |

---

## ? Test Complete!

When all 5 tests pass:

```
? Draft/Committed State Management: WORKING
? Navigation (Forward/Back): WORKING
? Authorization: WORKING
? Sensitive Field Masking: WORKING
? Database Persistence: WORKING
? Setup Wizard: READY FOR PRODUCTION
```

---

## ?? Next Steps After Manual Testing

1. ? Verify all 5 tests pass (this guide)
2. Replicate Step1_Storage template to Step2-Step6 pages
3. Run full wizard end-to-end (6 steps + summary)
4. Test post-setup admin modifications
5. Deploy to staging environment

