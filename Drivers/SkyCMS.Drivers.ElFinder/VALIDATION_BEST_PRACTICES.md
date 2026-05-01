# Path Validation & Normalization Best Practices Guide

## Overview

This guide documents best practices for working with the path normalization and validation system implemented in Phases 3 and 4.

## Quick Reference

### For Storage Consumers

When calling `IStorageContext` methods:

```csharp
// ✅ GOOD: Pass paths as-is; normalization + validation automatic
var entry = await storageContext.GetFileAsync("/pub/articles/file.txt");
var stream = await storageContext.GetStreamAsync("pub\\images\\logo.png");

// ❌ BAD: Don't assume invalid paths will silently fail
try {
    var entry = await storageContext.GetFileAsync("../../../sensitive.txt");
    // This will throw StorageException - expect and handle it
}
catch (StorageException ex) {
    logger.LogWarning("Invalid path provided: {Message}", ex.Message);
}
```

### For Storage Implementers

If extending storage functionality:

```csharp
// In new StorageContext methods, always validate before storage ops
public async Task<MyType> MyNewMethodAsync(string path)
{
    path = PathUtilities.NormalizePath(path);
    ValidatePathOrThrow(path);  // ← Critical security step

    // Now safe to use path with driver
    var driver = await GetPrimaryDriverAsync();
    return await driver.MyOperationAsync(path);
}
```

## Validation Behavior by Exception Type

### StorageException (Invalid Path)

**When thrown**: Input path fails security validation  
**Catch to**: Handle caller errors, log security events  
**Example**: `"Path contains traversal attempt (..)"`

```csharp
try 
{
    await storageContext.DeleteFileAsync(userProvidedPath);
}
catch (StorageException ex) when (ex.Message.Contains("traversal"))
{
    // Security: Log potential attack attempt
    securityLogger.LogWarning("Path traversal blocked: {UserId}", userId);
    return BadRequest("Invalid path");
}
```

## Common Validation Failures & Solutions

### Traversal Attempts

```
Input: "../../admin.aspx"
Error: "Path contains traversal attempt (..)"
Solution: Validate user input before passing to storage layer
```

### Reserved Names

```
Input: "CON/files/data.txt"
Error: "Path segment 'CON' is a reserved name"
Solution: Sanitize filenames; use UUIDs or slugs for programmatic names
```

### Excessive Depth

```
Input: "a/b/c/d/.../z" (100 segments)
Error: "Path depth exceeds limit (64 segments max, got 100)"
Solution: Implement pagination/hierarchy limits in UI
```

## Security Audit Checklist

When deploying this system:

- [ ] All user-provided paths passed through `IStorageContext`
- [ ] No bypassing of validation via direct driver calls
- [ ] Logging configured for `StorageException` events
- [ ] Error messages don't expose internal paths to users
- [ ] Rate limiting on failing path validations
- [ ] Regular audit of path creation workflows

## Performance Considerations

- **Normalization**: O(n) string operations, minimal overhead
- **Validation**: Regex compiled once (singleton), millisecond per check
- **Caching**: Consider caching normalized paths if same path used repeatedly

```csharp
// Cache normalized paths if performance becomes an issue
private static readonly ConcurrentDictionary<string, string> PathCache = new();

public static string GetNormalizedPath(string path)
{
    return PathCache.GetOrAdd(path, p => PathUtilities.NormalizePath(p));
}
```

## Testing Your Code

### Unit Tests

Test against the `IPathValidator` directly:

```csharp
[TestMethod]
public void MyMethod_WithTraversalPath_Rejects()
{
    var validator = new PathValidator();
    var result = validator.ValidatePath("../../etc/passwd");
    Assert.IsFalse(result.IsValid);
}
```

### Integration Tests

Test against actual `StorageContext`:

```csharp
[TestMethod]
public async Task GetFileAsync_WithInvalidPath_ThrowsStorageException()
{
    var context = new StorageContext(config, cache);

    await Assert.ThrowsExceptionAsync<StorageException>(
        () => context.GetFileAsync("../sensitive")
    );
}
```

## Debugging Validation Failures

When validation unexpectedly fails:

1. **Check normalization first**:
   ```csharp
   var normalized = PathUtilities.NormalizePath(userPath);
   Debug.WriteLine($"Normalized: {normalized}");
   ```

2. **Validate in isolation**:
   ```csharp
   var validator = new PathValidator();
   var result = validator.ValidatePath(normalized);
   Debug.WriteLine($"Valid: {result.IsValid}, Error: {result.ErrorMessage}");
   ```

3. **Review error message**: Often indicates the exact issue

## Migration Guide for Existing Code

### Before (Phase 2)

```csharp
// Relied on driver-level validation only
await storageContext.DeleteFileAsync(path);
```

### After (Phase 3+4)

```csharp
// Now validated at entry point
// Invalid paths throw StorageException immediately
try 
{
    await storageContext.DeleteFileAsync(path);
}
catch (StorageException ex)
{
    // Handle invalid path
}
```

## FAQ

**Q: Can I disable validation?**  
A: No. Validation is mandatory for security. If you need to bypass it, reconsider your design.

**Q: What about network paths or UNC paths?**  
A: Only local normalized paths (with forward slashes) are supported. Convert network paths to local first.

**Q: Will my existing code break?**  
A: Only if you were passing invalid paths. Valid paths continue to work unchanged.

**Q: How do I report validation bugs?**  
A: File an issue with the exact path, expected behavior, and actual error message.

## References

- **Phase 3 Docs**: `PHASE_3_COMPLETION.md` - Normalization specification
- **Phase 4 Docs**: `PHASE_4_COMPLETION.md` - Validation specification
- **API Spec**: `API_SPEC.md` - elFinder protocol reference
- **Design**: `DESIGN.md` - Architecture and design decisions

---

**Last Updated**: 2024  
**Version**: 1.0  
**Status**: Production Ready
