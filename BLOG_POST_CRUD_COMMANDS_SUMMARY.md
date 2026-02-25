# Blog Post CRUD Commands - Complete Implementation

## ?? Overview

Successfully created **3 dedicated blog post CRUD commands** to replace generic article handling and achieve separation of concerns:

1. **CreateBlogPostCommand** & **CreateBlogPostCommandHandler**
2. **UpdateBlogPostCommand** & **UpdateBlogPostCommandHandler**
3. **DeleteBlogPostCommand** & **DeleteBlogPostCommandHandler**

## ?? File Structure

```
Editor\Features\Blogs\
??? CreatePost\
?   ??? CreateBlogPostCommand.cs
?   ??? CreateBlogPostCommandHandler.cs
??? UpdatePost\
?   ??? UpdateBlogPostCommand.cs
?   ??? UpdateBlogPostCommandHandler.cs
??? DeletePost\
    ??? DeleteBlogPostCommand.cs
    ??? DeleteBlogPostCommandHandler.cs
```

---

## ?? Command Details

### **1. CreateBlogPostCommand**

**Purpose**: Create a new blog post within an existing blog stream.

**Input Properties**:
```csharp
public class CreateBlogPostCommand : ICommand<CommandResult<CreateBlogPostCommandResult>>
{
    public string BlogKey { get; set; }              // "cat-wash" - Must reference existing stream
    public string Title { get; set; }                // "New Post Title" - Required
    public string Content { get; set; }              // "<p>HTML content</p>" - Required
    public string? Introduction { get; set; }        // Optional excerpt
    public string? BannerImage { get; set; }         // Optional image URL
    public Guid TemplateId { get; set; }             // Blog post template ID
    public Guid UserId { get; set; }                 // Author/creator
    public DateTimeOffset? Published { get; set; }   // null = draft, set = published
}
```

**Output**:
```csharp
public class CreateBlogPostCommandResult
{
    public Guid Id { get; set; }                    // Article GUID
    public int ArticleNumber { get; set; }          // Logical number (shared across versions)
    public string UrlPath { get; set; }             // "cat-wash/new-post-title"
    public string BlogKey { get; set; }             // Parent stream key
}
```

**Handler Logic**:
1. ? Validates all required fields (UserId, BlogKey, Title, Content, TemplateId)
2. ? Verifies parent blog stream exists and is not deleted
3. ? Creates URL path: `{blogKey}/{slugified-title}`
4. ? Gets next available ArticleNumber
5. ? Creates new Article entity with version = 1
6. ? Returns created post details or error

---

### **2. UpdateBlogPostCommand**

**Purpose**: Update an existing blog post, creating a new version.

**Input Properties**:
```csharp
public class UpdateBlogPostCommand : ICommand<CommandResult<UpdateBlogPostCommandResult>>
{
    public int ArticleNumber { get; set; }           // Which post to update
    public string Title { get; set; }                // Updated title - Required
    public string Content { get; set; }              // Updated content - Required
    public string? Introduction { get; set; }        // Updated excerpt
    public string? BannerImage { get; set; }         // Updated image
    public DateTimeOffset? Published { get; set; }   // Publishing state
    public Guid UserId { get; set; }                 // Editor/updater
}
```

**Output**:
```csharp
public class UpdateBlogPostCommandResult
{
    public Guid Id { get; set; }                    // New version's GUID
    public int ArticleNumber { get; set; }          // Same article number
    public int VersionNumber { get; set; }          // Incremented version
    public string UrlPath { get; set; }             // Unchanged UrlPath
}
```

**Handler Logic**:
1. ? Validates ArticleNumber, UserId, Title, Content
2. ? Finds latest version of blog post
3. ? Creates new version with incremented VersionNumber
4. ? **Preserves** UrlPath and BlogKey (no URL changes)
5. ? Updates Title, Content, Introduction, BannerImage, Published status
6. ? Returns new version details or error

**Key Insight**: URL paths are immutable - this prevents broken links when editing!

---

### **3. DeleteBlogPostCommand**

**Purpose**: Soft delete a blog post by marking all versions as deleted.

**Input Properties**:
```csharp
public class DeleteBlogPostCommand : ICommand<CommandResult<DeleteBlogPostCommandResult>>
{
    public int ArticleNumber { get; set; }           // Which post to delete
    public string BlogKey { get; set; }              // Verify ownership
    public Guid UserId { get; set; }                 // Auditing
}
```

**Output**:
```csharp
public class DeleteBlogPostCommandResult
{
    public int ArticleNumber { get; set; }          // Deleted article number
    public string Message { get; set; }             // "Blog post and all N version(s) deleted successfully."
}
```

**Handler Logic**:
1. ? Validates ArticleNumber, UserId, BlogKey
2. ? Finds ALL versions of blog post by ArticleNumber
3. ? Verifies BlogKey matches (safety check - prevents cross-stream deletion)
4. ? Marks all versions with `StatusCode = Deleted`
5. ? Updates timestamp
6. ? Returns count of deleted versions

**Soft Delete Pattern**: Data is never actually removed, just marked as deleted. Allows recovery and audit trails.

---

## ?? Data Flow Example

### **Create Blog Post**

```
BlogController.CreateEntry()
  ?? User submits: title, blog key
  ?? Controller creates CreateBlogPostCommand
  ?? mediator.SendAsync(command)
      ?? CreateBlogPostCommandHandler.HandleAsync()
          ?? Validate all required fields
          ?? Verify parent blog stream exists
          ?? Create URL: "cat-wash/new-post-title"
          ?? Get next ArticleNumber (e.g., 42)
          ?? Create Article entity (VersionNumber = 1)
          ?? Save to DB
          ?? Return: { Id, ArticleNumber: 42, UrlPath, BlogKey }

Then: EditorController receives ArticleNumber 42 for live editing
```

### **Update Blog Post**

```
BlogController.EditEntry()
  ?? User saves changes to post #42
  ?? Creates UpdateBlogPostCommand { ArticleNumber: 42, Title, Content, ... }
  ?? mediator.SendAsync(command)
      ?? UpdateBlogPostCommandHandler.HandleAsync()
          ?? Find latest version of article #42 (e.g., version 3)
          ?? Create new Article entity:
          ?  - Same ArticleNumber (42)
          ?  - New VersionNumber (4)
          ?  - New Id (GUID)
          ?  - Updated Title, Content, etc.
          ?  - Keep same UrlPath and BlogKey
          ?? Save to DB
          ?? Return: { Id, ArticleNumber: 42, VersionNumber: 4, UrlPath }
```

### **Delete Blog Post**

```
BlogController.ConfirmDeleteEntry()
  ?? User deletes post from stream
  ?? Creates DeleteBlogPostCommand { ArticleNumber: 42, BlogKey: "cat-wash", UserId }
  ?? mediator.SendAsync(command)
      ?? DeleteBlogPostCommandHandler.HandleAsync()
          ?? Find all versions of article #42
          ?? Verify they belong to BlogKey "cat-wash"
          ?? Mark ALL versions: StatusCode = Deleted
          ?? Update timestamps
          ?? Save to DB
          ?? Return: { ArticleNumber: 42, Message: "...and all 4 version(s) deleted" }
```

---

## ? Key Features

### **Separation of Concerns**
? Dedicated handlers replace generic ArticleEditLogic  
? Clear, focused responsibility per handler  
? Blog-post-specific validation and logic  

### **Immutable URLs**
? UrlPath created at post creation and never changes  
? Prevents broken links when editing  
? BlogKey also immutable (tied to parent stream)  

### **Version Tracking**
? Every edit creates a new version  
? Original creation preserved in Article history  
? VersionNumber incremented automatically  

### **Safe Deletion**
? Soft delete - nothing is destroyed  
? All versions marked as deleted together  
? Blogging verification (BlogKey check)  
? Audit trail preserved  

### **Comprehensive Validation**
? UserId required (author tracking)  
? Parent stream must exist  
? Title and Content mandatory  
? BlogKey ownership verified on delete  

### **Proper Error Handling**
? Specific error messages  
? Logging at each step  
? DB exception handling  
? Clear failure returns  

---

## ?? Integration with BlogController

The new commands are designed to integrate seamlessly:

```csharp
// OLD: Uses generic CreateArticleCommand
var command = new CreateArticleCommand
{
    Title = title,
    ArticleType = ArticleType.BlogPost,
    BlogKey = blogKey,
    // ... generic fields
};

// NEW: Uses dedicated CreateBlogPostCommand
var command = new CreateBlogPostCommand
{
    Title = title,
    BlogKey = blogKey,
    Content = blogEntryTemplate.Content,
    TemplateId = blogEntryTemplate.Id,
    UserId = userId,
    Published = null
};
```

### **Commands are BlogController-driven**:
- ? CreateEntry() ? CreateBlogPostCommand
- ? EditEntry() ? UpdateBlogPostCommand
- ? ConfirmDeleteEntry() ? DeleteBlogPostCommand

---

## ?? Benefits Summary

| Aspect | Generic Articles | Blog Post CRUD Commands |
|--------|------------------|------------------------|
| **Clarity** | Generic logic mixed | Blog-specific handlers |
| **Validation** | Generic rules | Blog-specific rules |
| **URL Handling** | May change | Immutable (safe) |
| **BlogKey Enforcement** | Not enforced | Mandatory & verified |
| **Version Tracking** | Generic | Explicit tracking |
| **Error Messages** | Generic | Blog-specific context |
| **Testing** | Difficult | Easy & focused |
| **Maintenance** | Harder | Clearer intent |

---

## ?? Testing Considerations

Once you add unit tests, focus on:

1. **CreateBlogPostCommand**
   - ? Parent stream exists validation
   - ? URL path generation (slugified)
   - ? ArticleNumber assignment
   - ? Draft vs published creation
   - ? Missing field validation

2. **UpdateBlogPostCommand**
   - ? Version increment
   - ? URL path immutability
   - ? BlogKey preservation
   - ? Post not found handling
   - ? Content updates

3. **DeleteBlogPostCommand**
   - ? All versions marked deleted
   - ? BlogKey verification
   - ? Post not found handling
   - ? Version count in response
   - ? Audit fields updated

---

## ? Build Status

- **Compilation**: ? Successful
- **Files Created**: 6 handlers + commands
- **Patterns**: Consistent with existing codebase
- **Ready for**: BlogController integration

---

## ?? Next Steps

1. **Update BlogController** to use these new commands
2. **Add Unit Tests** for each handler
3. **Integrate with UI** (Razor Pages for blog post management)
4. **Remove dependency** on ArticleEditLogic for blog posts
5. **Consider**: Blog-post-specific publishing logic

This implementation achieves **true separation of concerns** - each blog operation has its own focused, testable handler! ??

