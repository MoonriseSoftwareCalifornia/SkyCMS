# WebsiteCopyOrchestrator Entity Type Extension Guide

## Overview

The `WebsiteCopyOrchestrator` class manages website copy operations across different database providers (Cosmos DB, SQL Server, MySQL, SQLite). When new entity types are added to `ApplicationDbContext`, they must be registered with the orchestrator to support database copying and validation.

## Architecture

The orchestrator uses type-safe dispatch patterns (C# switch expressions) to handle entity operations at runtime without reflection-based method invocation. This ensures:

- ✅ Cosmos DB compatibility (no reflection issues with query translation)
- ✅ Type safety with compiler verification
- ✅ Clear error messages for unsupported types
- ✅ Easy to understand and maintain

### Key Components

1. **SupportedEntityTypeNames** - A static `HashSet<string>` containing all supported entity type names
2. **IsSupportedEntityType()** - Helper method to check if a type is registered
3. **CountEntitiesAsync()** - Type-safe method to count entities by type
4. **ReadEntitiesAsync()** - Type-safe method to read entities by type without change tracking

## Adding a New Entity Type

Follow these steps to add support for a new entity type in the website copy process:

### Step 1: Add the Entity to the Database Context

First, ensure your entity is properly defined in `Cosmos.Common.Data.ApplicationDbContext`:

```csharp
public class MyNewEntity
{
	[Key]
	public Guid Id { get; set; }

	[StringLength(256)]
	public string Name { get; set; } = string.Empty;

	// ... other properties
}

// In ApplicationDbContext.cs
public DbSet<MyNewEntity> MyNewEntities { get; set; }
```

### Step 2: Register the Entity Type Name

Add the entity type name to the `SupportedEntityTypeNames` constant in `WebsiteCopyOrchestrator.cs`:

```csharp
private static readonly HashSet<string> SupportedEntityTypeNames = new(StringComparer.Ordinal)
{
	// ... existing entries ...
	nameof(MyNewEntity),  // Add this line
};
```

**Important Notes:**
- Use `nameof(MyNewEntity)` for unambiguous type names
- If the name is ambiguous (exists in multiple namespaces), use the fully qualified name as a string literal (e.g., `"Cosmos.Common.Data.MyNewEntity"`)
- The name must exactly match the `Type.Name` property of your entity

### Step 3: Add Type Dispatch to CountEntitiesAsync

In the `CountEntitiesAsync` method switch expression, add a new case:

```csharp
private static async Task<int> CountEntitiesAsync(DbContext dbContext, Type clrType)
{
	var typeName = clrType.Name;

	return typeName switch
	{
		// ... existing cases ...
		nameof(MyNewEntity) => await dbContext.Set<MyNewEntity>().CountAsync(),
		// ... rest of cases ...
	};
}
```

**Pattern:**
```csharp
nameof(EntityType) => await dbContext.Set<EntityType>().CountAsync(),
```

### Step 4: Add Type Dispatch to ReadEntitiesAsync

In the `ReadEntitiesAsync` method switch expression, add the corresponding case:

```csharp
private static async Task<List<object>> ReadEntitiesAsync(DbContext dbContext, Type clrType)
{
	var typeName = clrType.Name;

	var results = typeName switch
	{
		// ... existing cases ...
		nameof(MyNewEntity) => (await dbContext.Set<MyNewEntity>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
		// ... rest of cases ...
	};

	return results;
}
```

**Pattern:**
```csharp
nameof(EntityType) => (await dbContext.Set<EntityType>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
```

### Step 5: Update the Unit Tests

Add test cases to verify your entity type works correctly. Update `Tests/Services/WebsiteCopyOrchestratorTests.cs`:

```csharp
[TestMethod]
[Description("Verifies MyNewEntity type is discoverable and can be counted")]
public void MyNewEntity_IsInSupportedEntityTypes()
{
	// Arrange
	var appDb = new ApplicationDbContext(_dbOptions);

	// Act
	var entityTypes = appDb.Model.GetEntityTypes()
		.Select(t => t.ClrType.Name)
		.ToList();

	// Assert
	Assert.IsTrue(entityTypes.Contains(nameof(MyNewEntity)), 
		"MyNewEntity should be discoverable in ApplicationDbContext");
}
```

## Special Cases

### Generic Entity Types (e.g., IdentityUserPasskey<T>)

For generic entity types, use a pattern-matching guard in the switch:

```csharp
// In SupportedEntityTypeNames
"IdentityUserPasskey<string>", // Generic type with specific type parameter

// In CountEntitiesAsync
_ when typeName.StartsWith("IdentityUserPasskey", StringComparison.Ordinal)
	=> await dbContext.Set<IdentityUserPasskey<string>>().CountAsync(),

// In ReadEntitiesAsync
_ when typeName.StartsWith("IdentityUserPasskey", StringComparison.Ordinal)
	=> (await dbContext.Set<IdentityUserPasskey<string>>().AsNoTracking().ToListAsync()).Cast<object>().ToList(),
```

### Ambiguous Type Names

If your entity type name conflicts with another namespace (e.g., `Metric` exists in both `Cosmos.Common.Data` and `Cosmos.DynamicConfig`):

1. Use the fully qualified name in `SupportedEntityTypeNames`:
```csharp
"Cosmos.Common.Data.Metric", // or just use the string literal "Metric" if it uniquely identifies the type in ApplicationDbContext
```

2. Use the full namespace qualification in the switch case:
```csharp
"Metric" => await dbContext.Set<Cosmos.Common.Data.Metric>().CountAsync(),
```

## Verification Checklist

After adding a new entity type, verify:

- ✅ The type is added to `ApplicationDbContext`
- ✅ The type name is added to `SupportedEntityTypeNames`
- ✅ A case is added to `CountEntitiesAsync` switch
- ✅ A case is added to `ReadEntitiesAsync` switch
- ✅ Unit tests are added or updated
- ✅ The project builds successfully
- ✅ Unit tests pass
- ✅ The type follows EF Core conventions (has `[Key]` attribute, etc.)

## Error Handling

The orchestrator gracefully handles unsupported entity types:

1. **During Copy:** Unsupported types are logged to Debug output and skipped
2. **During Validation:** Unsupported types are logged to Debug output and validation continues

This allows forward compatibility. If an entity type is discovered at runtime but not yet registered, the copy/validation operations will skip it rather than fail completely.

Example debug output:
```
Skipping copy for unsupported entity type: MyFutureEntity
Skipping validation for unsupported entity type: MyFutureEntity
```

## Performance Considerations

- **CountAsync()** is optimized for Cosmos DB and SQL-based providers (returns count without materializing data)
- **ReadEntitiesAsync()** uses `.AsNoTracking()` to avoid EF Core change tracking overhead
- Type dispatch via switch expressions is compiled to efficient IL code (no reflection overhead)

## Testing Your Changes

### Unit Test Example

```csharp
[TestMethod]
public async Task CopyDatabase_HandlesMyNewEntity()
{
	// Arrange
	var sourceDb = new ApplicationDbContext(_dbOptions);
	var destDb = new ApplicationDbContext(_dbOptions);
	await sourceDb.Database.EnsureCreatedAsync();
	await destDb.Database.EnsureCreatedAsync();

	var entity = new MyNewEntity { Id = Guid.NewGuid(), Name = "Test" };
	sourceDb.MyNewEntities.Add(entity);
	await sourceDb.SaveChangesAsync();

	// Act - verify counting works
	var sourceCount = await sourceDb.MyNewEntities.CountAsync();
	var destCount = await destDb.MyNewEntities.CountAsync();

	// Assert
	Assert.AreEqual(1, sourceCount);
	Assert.AreEqual(0, destCount);
}
```

## Troubleshooting

### "Unknown entity type for counting" Exception

**Cause:** The entity type is discovered by EF Core's entity discovery but not registered in `SupportedEntityTypeNames` or the switch statements.

**Solution:** Follow the "Adding a New Entity Type" steps above.

### Type Name Mismatch

**Problem:** `Entity.GetType().Name` doesn't match what's in the switch case.

**Solution:** Use `nameof(YourType)` instead of string literals. The `nameof` operator ensures compile-time verification.

### NullReferenceException in ReadEntitiesAsync

**Cause:** DbSet for the entity type doesn't exist on the context.

**Solution:** Verify the entity is properly decorated with `[Owned]` check and has a primary key.

## Related Files

- `Sky.MultiTenant-Adminstrator/Services/WebsiteCopyOrchestrator.cs` - Main orchestrator
- `Common/Data/ApplicationDbContext.cs` - Entity definitions
- `Tests/Services/WebsiteCopyOrchestratorTests.cs` - Unit tests

## Best Practices

1. **Always use `nameof()`** for type names to catch errors at compile time
2. **Add unit tests** before considering the feature complete
3. **Keep switch cases in alphabetical order** for easier maintenance
4. **Document any special handling** (e.g., "Metric" namespace qualification) as comments
5. **Test with multiple database providers** (at minimum Cosmos DB and SQL Server via in-memory)

---

For questions or issues, refer to the unit tests and existing entity type implementations as examples.
