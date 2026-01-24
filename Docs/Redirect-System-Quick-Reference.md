# Quick Reference Guide - Redirect Creation System

## For Developers

### How Redirects Are Created

When an article title changes, the system:

1. **Checks published status** of the article being changed
2. **Cascades to related articles** (children or blog posts)
3. **Checks each related article's** individual published status
4. **Creates redirects only for published** articles
5. **Resolves redirect chains** to point to final destinations
6. **Commits everything atomically** in a transaction

### Key Classes

```csharp
// Tracks a single URL change with metadata
UrlChange
  - OldUrl: string
  - NewUrl: string
  - IsPublished: bool
  - ArticleNumber: int

// Tracks results of redirect creation operation
RedirectCreationResult
  - SuccessCount: int
  - FailedRedirects: List<(int, string, string, string)>
  - SkippedCount: int
  - TotalAttempted: int (calculated)
  - AllSucceeded: bool (calculated)
```

### Example Usage

```csharp
// The system automatically handles this when you save an article with a new title
var command = new SaveArticleCommand
{
    ArticleNumber = 123,
    Title = "New Title", // Changed from "Old Title"
    // ... other properties
};

var result = await saveArticleHandler.HandleAsync(command);

// Redirects are created automatically:
// - "old-title" ? "new-title" (if article is published)
// - Child URLs updated and redirected (if children are published)
// - All in one transaction
```

## For Troubleshooting

### Common Scenarios

#### Scenario 1: Redirect Not Created
**Symptoms:** Old URL returns 404 instead of redirecting

**Possible Causes:**
1. Article was not published when title changed
2. Redirect creation failed (check logs)
3. Slug didn't actually change (case-only change)

**Check Logs For:**
```
"Redirect creation completed for article {ArticleNumber}: X created, Y failed, Z skipped"
```

#### Scenario 2: Child Article Not Redirected
**Symptoms:** Parent redirects but child doesn't

**Possible Causes:**
1. Child article was unpublished
2. Child's URL didn't change (same relative path)

**Expected Behavior:**
- Published children ? Redirects created ?
- Unpublished children ? No redirects ? (by design)

#### Scenario 3: Transaction Rolled Back
**Symptoms:** Title change fails, nothing updated

**Possible Causes:**
1. Slug conflict with existing article
2. Database constraint violation
3. Unexpected error during update

**Check Logs For:**
```
ERROR: "Title change transaction rolled back for article {ArticleNumber}"
```

### Log Levels

**Information:**
- Transaction committed successfully
- Redirect creation summary
- Chain resolution details

**Warning:**
- Some redirects failed (operation still succeeds)
- Duplicate old URLs detected
- Slug conflicts

**Error:**
- Transaction rolled back
- Failed to create specific redirect
- Redirect chain exceeds max depth

## For Code Review

### What to Look For

? **Good Patterns:**
- Using `AsNoTracking()` when querying articles for comparison
- Capturing `wasPublished` flag before any updates
- Checking individual article published status
- Using transactions for related updates

? **Anti-Patterns:**
- Assuming child has same published status as parent
- Not using transactions for multi-step updates
- Not checking redirect creation results
- Ignoring published status when creating redirects

### Test Coverage Checklist

When adding new title change features, ensure tests cover:
- [ ] Published article scenarios
- [ ] Unpublished article scenarios
- [ ] Mixed published/unpublished children
- [ ] Blog streams with mixed post statuses
- [ ] Transaction rollback on failures
- [ ] Redirect chain prevention

## For Operations/DevOps

### Monitoring Checklist

Monitor these metrics in production:

1. **Redirect Creation Success Rate**
   - Log level: Information
   - Pattern: `"Redirect creation completed"`
   - Target: >99% success rate

2. **Transaction Rollback Rate**
   - Log level: Error
   - Pattern: `"transaction rolled back"`
   - Target: <0.1% of title changes

3. **Redirect Chain Detection**
   - Log level: Information
   - Pattern: `"Detected redirect chain"`
   - Target: Should be rare after deployment

### Database Queries

```sql
-- Check recent redirects
SELECT * FROM Articles 
WHERE StatusCode = /* Redirect status code */
  AND Updated >= DATEADD(day, -1, GETDATE())
ORDER BY Updated DESC;

-- Count redirects per article
SELECT ArticleNumber, COUNT(*) as RedirectCount
FROM Articles
WHERE StatusCode = /* Redirect status code */
GROUP BY ArticleNumber
HAVING COUNT(*) > 1
ORDER BY RedirectCount DESC;

-- Find potential redirect chains (existing data)
SELECT a1.UrlPath as Source, 
       a1.RedirectTarget as Target1,
       a2.RedirectTarget as Target2
FROM Articles a1
LEFT JOIN Articles a2 ON a1.RedirectTarget = a2.UrlPath 
  AND a2.StatusCode = /* Redirect status code */
WHERE a1.StatusCode = /* Redirect status code */
  AND a2.Id IS NOT NULL;
```

### Performance Benchmarks

Expected performance (these are good targets):

- Simple title change: <100ms
- Title change with 10 children: <500ms
- Blog stream with 50 posts: <2s
- Transaction overhead: <10ms

If seeing slower performance:
1. Check for long-running queries in transaction
2. Verify database indexes on UrlPath, ArticleNumber
3. Check for lock contention on Articles table

## Quick Decision Tree

```
Title Changed?
  ??> Is article published?
  ?     ??> Yes ? Create redirect for article ?
  ?     ??> No ? Skip redirect ?
  ?
  ??> Has children?
  ?     ??> For each child:
  ?           ??> Is child published?
  ?           ?     ??> Yes ? Create redirect ?
  ?           ?     ??> No ? Skip ?
  ?           ??> Update child URL path ?
  ?
  ??> Is blog stream?
  ?     ??> For each blog post:
  ?           ??> Is post published?
  ?           ?     ??> Yes ? Create redirect ?
  ?           ?     ??> No ? Skip ?
  ?           ??> Update post URL path ?
  ?
  ??> Commit all changes in transaction ?
```

## Contact

For questions about the redirect system:
- See full documentation in `Docs/` folder
- Review tests in `Tests/Features/Articles/Save/`
- Check implementation in `Editor/Services/Titles/TitleChangeService.cs`
