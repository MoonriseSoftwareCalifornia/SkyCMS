---
title: Page Scheduling Guide
description: Schedule page publishing and unpublishing with date/time controls
keywords: scheduling, publishing, unpublishing, date-time, automation
audience: [developers, content-creators, administrators]
---

# Page Scheduling

## Overview

SkyCMS includes a powerful page scheduling system that allows content creators to schedule web pages for automatic publication at future dates and times. This feature is perfect for planning content releases, coordinating marketing campaigns, or managing time-sensitive information.

When a scheduled article is published, the system automatically sends an email notification to the author, keeping them informed about their content going live.

## For Content Creators

### How to Schedule a Page

1. **Edit Your Content**
   - Use any of the SkyCMS editors (CKEditor, GrapesJS, or Code Editor) to create or modify your web page content
   - Make all necessary changes to text, images, layout, and other content

2. **Review Your Changes**
   - Click the **"Review/Publish"** button in the toolbar
   - A preview of your page will appear, showing exactly how it will look when published

   ![Review/Publish dialog with publish options](../images/screenshots/page-scheduling-review-dialog.webp)

3. **Choose Publishing Option**
   
   At the top of the preview, you'll see two buttons:
   
   - **"Publish Now"** - Makes your changes live immediately
   - **"Publish Later"** - Opens a calendar widget for scheduling

4. **Schedule for Future Publication**
   - Click **"Publish Later"**
   - Select your desired publication date and time from the calendar widget
   - Times are displayed in your local timezone and automatically converted to UTC for storage
   - Click confirm to schedule the publication

   ![Publish later calendar and time picker](../images/screenshots/page-scheduling-calendar.webp)

### Email Notifications

When your scheduled article is published, you'll receive an email notification containing:

- **Article Details**: Title, article number, version number, and publication timestamp
- **Direct Link**: A clickable link to view your published article
- **Publication Status**: Confirmation that the article is now live on the website

**Important Notes:**
- Email notifications are sent only to the article author (the user who created or last edited the article)
- The email is sent after successful publication
- If you don't receive an email, check your spam/junk folder or contact your administrator
- Only authors with a valid email address on their account will receive notifications

### Important Notes

- **Future Dates Only**: When scheduling, you can only select dates and times in the future
- **Permissions**: Only Administrators and Editors can publish or schedule pages
- **Timezone Handling**: The system uses your browser's timezone when displaying times, but stores everything in UTC to ensure consistency across time zones
- **Version Management**: Each scheduled publication creates a new version of the page. When the scheduled time arrives, that version automatically becomes live

### Monitoring Scheduled Publications

Administrators and Editors can view scheduled publications through the Hangfire Dashboard:

- Access the dashboard by choosing 'Page Scheduler' from the main drop down menu
- View all scheduled jobs, their status, and execution history
- Monitor for any failed publications

![Hangfire page scheduler dashboard](../images/screenshots/page-scheduler-dashboard.webp)

### What Happens at Publication Time

When your scheduled time arrives:

1. The scheduler runs automatically every 10 minutes
2. It identifies all pages scheduled for publication
3. The scheduled version is automatically published
4. Any previously published versions are unpublished
5. The new content becomes live on your website
6. An email notification is sent to the article author

### Best Practices

- **Schedule in Advance**: Schedule important content well in advance to allow time for review
- **Verify Timezones**: Double-check that your scheduled time matches your intended timezone
- **Use Staging**: Test your content thoroughly before scheduling
- **Monitor Publications**: Check the dashboard after your scheduled time to confirm successful publication
- **Check Your Email**: Look for the publication confirmation email to verify your article went live
- **Avoid Conflicts**: Don't schedule multiple versions of the same page at similar times

### Troubleshooting

**My page didn't publish at the scheduled time**
- Check the Hangfire dashboard for any errors
- Verify that the scheduled time was in the future when you set it
- Contact your administrator if the issue persists

**I didn't receive a notification email**
- Verify your user account has a valid email address
- Check your spam/junk email folder
- Contact your administrator to verify the email service is configured correctly

**I need to cancel a scheduled publication**
- Currently, you'll need to manually unpublish the scheduled version or set the publish date to null
- Contact your administrator for assistance

**I scheduled the wrong time**
- Edit the page version and update the scheduled publication time
- Or unpublish the version and create a new scheduled publication

---

## For Developers and Administrators

### Technical Overview

The SkyCMS page scheduling system uses **Hangfire** as the background job processing framework to automatically publish web pages based on their scheduled publication dates. The system also includes an integrated email notification feature that alerts authors when their scheduled content goes live.

### Architecture

#### Components

1. **ArticleScheduler Service**
   - Location: `Editor/Services/Scheduling/ArticleScheduler.cs`
   - Interface: `IArticleScheduler`
   - Implements the core scheduling logic
   - Handles email notifications to authors upon successful publication

2. **Hangfire Integration**
   - Location: `Editor/Services/Scheduling/HangFireExtensions.cs`
   - Configures Hangfire with appropriate database storage
   - Supports: Cosmos DB, SQL Server, MySQL, SQLite

3. **Authorization Filter**
   - Location: `Editor/Services/Scheduling/HangfireAuthorizationFilter.cs`
   - Restricts dashboard access to authenticated Administrators and Editors

4. **Email Service Integration**
   - Uses `ICosmosEmailSender` for sending notifications
   - Gracefully handles email service failures without blocking publication
   - Sends HTML-formatted emails with article details and direct links

5. **Configuration**
   - Location: `Editor/Program.cs`
   - Sets up recurring job execution

### How It Works

#### Execution Flow

1. **Recurring Job Setup** (Program.cs, lines 348-352)
   ```csharp
   recurring.AddOrUpdate<ArticleScheduler>(
       "article-version-publisher",
       x => x.ExecuteAsync(),
       Cron.MinuteInterval(10)); // Runs every 10 minutes
   ```

2. **Scheduler Execution** (ArticleScheduler.ExecuteAsync)
   - Runs every 10 minutes via Hangfire
   - Supports both single-tenant and multi-tenant modes
   - In multi-tenant mode, processes all configured tenants

3. **Article Processing**
   - Queries for articles with `Published` dates ≤ current UTC time
   - Groups by `ArticleNumber` to find articles with multiple versions
   - For each article with 2+ published versions:
     - Selects the most recent non-future version
     - Unpublishes older versions (sets `Published = null`)
     - Publishes the active version via `ArticleEditLogic.PublishArticle()`
     - Sends an email notification to the author using `ICosmosEmailSender`

4. **Multi-Tenant Support**
   - When `IsMultiTenantEditor` is true, the scheduler:
   - Retrieves all domain names from the configuration provider
   - Creates a separate database context for each tenant
   - Processes articles independently per tenant

### Database Storage

The scheduler query looks for articles meeting these criteria:
```csharp
.Where(a => a.Published != null
            && a.Published <= now
            && a.StatusCode != (int)StatusCodeEnum.Deleted)
```

Articles with `Published = null` are considered:
- Drafts (if never published)
- Previously published but superseded (if unpublished by scheduler)

### Hangfire Configuration

#### Supported Databases

- **Cosmos DB**: Uses `Hangfire.AzureCosmosDB`
- **SQL Server**: Uses `Hangfire.SqlServer`
- **MySQL**: Uses `Hangfire.MySqlStorage`
- **SQLite**: Uses `Hangfire.Storage.SQLite` (typically for testing)

#### Server Options

```csharp
options.Queues = new[] { "critical", "default" };
options.WorkerCount = Math.Max(Environment.ProcessorCount, 1);
options.SchedulePollingInterval = TimeSpan.FromMinutes(10);
options.ShutdownTimeout = TimeSpan.FromMinutes(2);
options.HeartbeatInterval = TimeSpan.FromMinutes(5);
```

### Dashboard Access

- **URL**: `/Editor/CCMS___PageScheduler`
- **Authorization**: Administrators and Editors only
- **Features**:
  - View recurring jobs and their schedules
  - Monitor job execution history
  - View failed jobs and retry them
  - Real-time job processing statistics

### Timezone Handling

- **Frontend**: JavaScript Moment.js library handles timezone conversion in the browser
- **Backend**: All dates stored as `DateTimeOffset` in UTC
- **Scheduler**: Uses `IClock.UtcNow` for consistent time comparisons across tenants and servers

### Error Handling and Logging

The scheduler logs:
- Execution start and completion times
- Each article version activation
- Any errors during processing

Log levels:
- `Information`: Normal execution flow
- `Debug`: Article version details
- `Error`: Processing failures

### Known Limitations and TODOs

1. **Future-Only Validation**
   - TODO: Enforce UI validation to only allow future dates when scheduling
   - Prevents conflicts with currently published versions

2. **Race Conditions**
   - TODO: Investigate potential race conditions during manual publish/unpublish operations
   - Consider implementing optimistic concurrency control or row-level locking

3. **Failed Publication Handling**
   - TODO: Implement retry mechanism for failed publications
   - TODO: Add notification system to alert administrators of failures

4. **User Notifications**
   - TODO: Implement notification system to alert content creators when scheduled publications go live
   - Consider email, in-app notifications, or webhook integrations

5. **Conflict Resolution**
   - No automatic conflict resolution when multiple versions are scheduled for the same time
   - System processes based on query result ordering

6. **Polling Interval**
   - Currently hardcoded to 10 minutes
   - May be made configurable in future versions

### Customization

#### Changing the Polling Interval

Modify the cron expression in `Program.cs`:

```csharp
// Current: Every 10 minutes
Cron.MinuteInterval(10)

// Alternatives:
Cron.MinuteInterval(5)  // Every 5 minutes
Cron.Minutely()         // Every minute
Cron.Hourly()          // Every hour
```

**Note**: More frequent polling increases database load but provides faster publication times.

#### Custom Authorization Rules

Modify `HangfireAuthorizationFilter.cs` to implement custom authorization logic:

```csharp
public bool Authorize(DashboardContext context)
{
    var httpContext = context.GetHttpContext();
    
    // Add custom logic here
    return httpContext.User.Identity.IsAuthenticated && 
           httpContext.User.IsInRole("Administrators");
}
```

### Monitoring and Troubleshooting

#### Common Issues

**Scheduled pages not publishing**
1. Check Hangfire dashboard for failed jobs
2. Verify database connectivity
3. Check application logs for errors
4. Ensure Hangfire server is running (check `AddHangfireServer` in Program.cs)

**Performance issues**
1. Monitor database query performance
2. Consider optimizing the article query with appropriate indexes
3. Adjust `WorkerCount` in Hangfire server options
4. Review `SchedulePollingInterval` setting

**Multi-tenant issues**
1. Verify `IDynamicConfigurationProvider` is properly configured
2. Check that all tenant connection strings are valid
3. Review logs for tenant-specific errors

#### Useful Database Queries

**Find all scheduled articles**:
```sql
SELECT ArticleNumber, Title, VersionNumber, Published, StatusCode
FROM Articles
WHERE Published IS NOT NULL 
  AND Published > GETUTCDATE()
  AND StatusCode != [DeletedStatusCode]
ORDER BY Published
```

**Find articles with multiple published versions**:
```sql
SELECT ArticleNumber, COUNT(*) as VersionCount
FROM Articles
WHERE Published IS NOT NULL
GROUP BY ArticleNumber
HAVING COUNT(*) >= 2
```

### Testing

The scheduler includes comprehensive unit tests:
- Location: `Tests/ArticleSchedulerTests.cs`
- Uses `IClock` abstraction for testable time
- Covers single-tenant and multi-tenant scenarios
- Tests version activation and conflict resolution

### Security Considerations

1. **Dashboard Access**: Always restrict to authenticated administrators and editors
2. **Job Data**: Avoid storing sensitive information in job parameters
3. **Database Security**: Ensure Hangfire database connection uses least-privilege access
4. **Audit Logging**: All publication actions are logged for audit trails

### Performance Optimization

1. **Indexing**: Ensure indexes on:
   - `Articles.Published`
   - `Articles.ArticleNumber`
   - `Articles.StatusCode`

2. **Query Optimization**: The current implementation loads article numbers into memory for grouping (EF Core Cosmos DB limitation)

3. **Scaling**: Hangfire supports multiple server instances for high-availability deployments

### Integration Points

The scheduler integrates with:
- `ArticleEditLogic`: For publishing articles
- `IPublishingService`: For generating static files
- `ICatalogService`: For catalog updates
- `IRedirectService`: For managing redirects
- `ITitleChangeService`: For handling title changes

### API Reference

#### IArticleScheduler Interface

```csharp
public interface IArticleScheduler
{
    /// <summary>
    /// Executes the scheduled job to process article versions with multiple published dates.
    /// </summary>
    Task ExecuteAsync();
}
```

#### Key Methods

**ExecuteAsync()**
- Entry point for Hangfire recurring job
- Handles single-tenant and multi-tenant execution
- Logs start and completion

**Run(ApplicationDbContext, string)**
- Processes articles for a single tenant/database
- Queries for articles with multiple published versions
- Delegates to ProcessArticleVersions for each article

**ProcessArticleVersions(DateTimeOffset, ApplicationDbContext, int)**
- Handles version activation logic for a single article
- Unpublishes old versions
- Publishes the most recent non-future version
- Includes error handling and logging

### Further Reading

- [Hangfire Documentation](https://docs.hangfire.io/)
- ArticleEditLogic source code: `Editor/Services` (see source repository)

> **Note**: For architecture and multi-tenant configuration details, see the source code repository at [GitHub](https://github.com/CWALabs/SkyCMS).

---

## Related Documentation

- [Content Editing](./LiveEditor/README.md)
- Version Control (coming soon)
- [Publishing Workflows](../Publishing-Overview.md)
