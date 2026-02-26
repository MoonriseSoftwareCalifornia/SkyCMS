# 🎯 **SAVEARTICLE REFACTORING & TEST MIGRATION PLAN**

## **STATUS: READY TO EXECUTE**

---

## **PART 1: CONTROLLER REFACTORING**

### ✅ **CURRENT STATUS: EditorController Already CQRS-Compliant**

After analysis, `EditorController.cs` is **already using `SaveArticleCommand`** in all relevant methods:

#### **Method: Designer() [POST]** - ✅ ALREADY REFACTORED
```csharp
var command = new SaveArticleCommand
{
    ArticleNumber = article.ArticleNumber,
    Title = model.Title,
    Content = html,
    // ... other properties
};
var result = await mediator.SendAsync<CommandResult<ArticleUpdateResult>>(command);
```

#### **Method: Edit() [POST]** - ✅ ALREADY REFACTORED
```csharp
var command = new SaveArticleCommand
{
    ArticleNumber = model.ArticleNumber,
    Title = model.Title,
    Content = article.Content,
    // ... other properties
};
var result = await mediator.SendAsync<CommandResult<ArticleUpdateResult>>(command);
```

#### **Method: EditCode() [POST]** - ✅ ALREADY REFACTORED
```csharp
var command = new SaveArticleCommand
{
    ArticleNumber = model.ArticleNumber,
    Title = model.Title,
    Content = model.Content,
    HeadJavaScript = model.HeadJavaScript,
    FooterJavaScript = model.FooterJavaScript,
    // ... other properties
};
var result = await mediator.SendAsync<CommandResult<ArticleUpdateResult>>(command);
```

### ✅ **CONCLUSION: No Controller Changes Needed**

All usages of `SaveArticle()` in controllers have been successfully migrated to `SaveArticleCommand`.

---

## **PART 2: TEST MIGRATION STRATEGY**

### **Current Test Situation**

**File:** `Tests/Services/ArticleEditLogicTests.cs`
- **Status:** Marked [Obsolete] at class level
- **Test Count:** 5+ test methods
- **Test Status:** All marked [Ignore]
- **Methods Used:** 
  - `CreateArticleAsync()` - [Obsolete]
  - `Logic.SaveArticle()` - [Obsolete]
  - `Logic.PublishArticle()` - [Obsolete]
  - `Logic.DeleteArticle()` - [Obsolete]
  - `Logic.RestoreArticle()` - [Obsolete]

### **Test Analysis**

| Test Method | Current Status | Uses Method | Action |
|-------------|---|---|---|
| `CreateArticle_NewArticle_GeneratesUniqueArticleNumber()` | [Ignore] | `CreateArticleAsync()` | Keep [Ignore], Create handler test |
| `CreateArticle_NewArticle_StartsWithVersionOne()` | [Ignore] | `CreateArticleAsync()` | Keep [Ignore], Create handler test |
| `CreateArticle_NewArticle_CreatesAsDraft()` | [Ignore] | `CreateArticleAsync()` | Keep [Ignore], Create handler test |
| `SaveArticle_UpdateContent_PersistsChanges()` | [Ignore] | `Logic.SaveArticle()` | **FOCUS: Migrate this test** |
| `PublishArticle_DraftArticle_SetsPublishedTimestamp()` | [Ignore] | `Logic.PublishArticle()` | Keep [Ignore], Create handler test |
| `DeleteArticle_ExistingArticle_MarksAsDeleted()` | [Ignore] | `Logic.DeleteArticle()` | Keep [Ignore], Create handler test |

### **Three-Phase Test Migration Approach**

#### **PHASE 2A: Keep Legacy Tests (Current State)**
- ✅ Keep all tests in `ArticleEditLogicTests.cs`
- ✅ Mark with [Ignore] attribute
- ✅ Preserve for reference/documentation
- ✅ Document migration path in XML comments

#### **PHASE 2B: Create Handler Tests (Next Step)**
Create new test files in `Tests/Features/Articles/`:
- `SaveArticleHandlerTests.cs` ← **PRIORITY: Focus Here First**
- `CreateArticleHandlerTests.cs`
- `PublishArticleHandlerTests.cs`
- `DeleteArticleHandlerTests.cs`
- `RestoreArticleHandlerTests.cs`

#### **PHASE 3: Migrate Tests at v3.0 Release**
- Remove [Ignore] attributes
- Delete legacy `ArticleEditLogicTests.cs`
- Keep handler test suite as primary tests

---

## **EXECUTION PLAN: Create SaveArticleHandlerTests.cs**

### **Template Structure**

```csharp
[TestClass]
[DoNotParallelize]
public class SaveArticleHandlerTests : SkyCmsTestBase
{
    private ArticleViewModel testArticle;

    [TestInitialize]
    public async Task Setup()
    {
        InitializeTestContext(seedLayout: true);
        
        // Create test article using CreateArticleCommand
        var createCommand = new CreateArticleCommand
        {
            Title = "Test Article",
            UserId = Guid.Parse(TestUserId),
            ArticleType = ArticleType.General,
            BlogKey = string.Empty,
            TemplateId = null
        };
        
        var createResult = await Mediator.SendAsync<CommandResult<ArticleViewModel>>(createCommand);
        testArticle = createResult.Data;
    }

    [TestMethod]
    public async Task SaveArticleCommand_UpdateContent_PersistsChanges()
    {
        // Arrange
        var newContent = "<p>Updated content</p>";
        var command = new SaveArticleCommand
        {
            ArticleNumber = testArticle.ArticleNumber,
            Title = testArticle.Title,
            Content = newContent,
            HeadJavaScript = testArticle.HeadJavaScript,
            FooterJavaScript = testArticle.FooterJavaScript,
            BannerImage = testArticle.BannerImage,
            UrlPath = testArticle.UrlPath,
            ArticleType = testArticle.ArticleType,
            Category = testArticle.Category,
            Introduction = testArticle.Introduction,
            Published = testArticle.Published,
            UserId = Guid.Parse(TestUserId)
        };

        // Act
        var result = await Mediator.SendAsync<CommandResult<ArticleUpdateResult>>(command);

        // Assert
        Assert.IsTrue(result.IsSuccess, "Save should succeed");
        Assert.AreEqual(newContent, result.Data.Model.Content, "Content should be updated");
    }
}
```

### **Test Coverage for SaveArticleHandlerTests**

1. ✅ `SaveArticleCommand_UpdateContent_PersistsChanges()`
   - Maps to: `SaveArticle_UpdateContent_PersistsChanges()`
   - Tests: Content update and persistence

2. ✅ `SaveArticleCommand_ChangeTitle_PreservesArticleNumber()`
   - Tests: Title changes while preserving article number
   - Maps to legacy test pattern

3. ✅ `SaveArticleCommand_UpdateArticle_UpdatesTimestamp()`
   - Tests: Updated timestamp is set correctly
   - Maps to legacy test pattern

4. ✅ `SaveArticleCommand_UpdateArticle_PreservesArticleType()`
   - Tests: Article type remains unchanged
   - Maps to legacy test pattern

5. ✅ `SaveArticleCommand_UpdateHeadJavaScript_PersistsChanges()`
   - Tests: Head JavaScript updates
   - New comprehensive test

6. ✅ `SaveArticleCommand_UpdateFooterJavaScript_PersistsChanges()`
   - Tests: Footer JavaScript updates
   - New comprehensive test

7. ✅ `SaveArticleCommand_UpdateMetadata_PersistsChanges()`
   - Tests: Category and introduction updates
   - New comprehensive test

8. ✅ `SaveArticleCommand_NonExistentArticle_ReturnsError()`
   - Tests: Error handling for missing article
   - Maps to error handling pattern

---

## **RECOMMENDED EXECUTION ORDER**

### **Step 1: Create SaveArticleHandlerTests.cs** ← **START HERE**
- [ ] Create new file: `Tests/Features/Articles/SaveArticleHandlerTests.cs`
- [ ] Copy structure from template above
- [ ] Implement 8 test methods
- [ ] Verify all tests pass
- [ ] Build solution to confirm 0 errors

### **Step 2: (Optional) Create Other Handler Tests**
- [ ] Create `CreateArticleHandlerTests.cs`
- [ ] Create `PublishArticleHandlerTests.cs`
- [ ] Create `DeleteArticleHandlerTests.cs`
- [ ] Create `RestoreArticleHandlerTests.cs`

### **Step 3: Mark Tests for Eventual Removal** (at v3.0)
- [ ] Add comment in `ArticleEditLogicTests.cs`: "Remove at v3.0, replaced by handler tests"
- [ ] Update README.md with completion status
- [ ] Document in migration guide

### **Step 4: Update Documentation**
- [ ] Update `Tests/Features/Articles/README.md`
- [ ] Update `CQRS_MIGRATION_COMPLETE.md`
- [ ] Update `.github/copilot-instructions.md`

---

## **KEY DECISIONS**

### **✅ Decision 1: Keep Legacy Tests [Ignore]**
**Rationale:**
- Documentation of legacy behavior
- Reference for team understanding
- Safety net during transition
- Easy rollback if needed

**Alternative Considered:** Delete immediately
**Rejected Because:** Lose historical context and learning resource

### **✅ Decision 2: Create New Handler Tests**
**Rationale:**
- Validates CQRS implementation
- Tests current recommended pattern
- Follows modern test structure
- CQRS-first approach

**Alternative Considered:** Just ignore legacy tests
**Rejected Because:** Need validation that handlers work correctly

### **✅ Decision 3: Focus on SaveArticleHandlerTests First**
**Rationale:**
- Most heavily used method in controllers
- Already refactored in 3 controller methods
- Clearest test requirements
- Can be extended pattern for others

---

## **DEPENDENCIES & PREREQUISITES**

✅ **Already in Place:**
- `SaveArticleCommand` defined
- `SaveArticleHandler` implemented
- `SaveArticleValidator` configured
- Mediator properly registered
- Test infrastructure (`SkyCmsTestBase`)
- `CreateArticleCommand` for test setup

❓ **To Verify:**
- Test project references correct namespaces
- `CommandResult<ArticleUpdateResult>` imports correct
- Mediator can execute commands in test context
- Database properly seeded for tests

---

## **SUCCESS CRITERIA**

✅ All new SaveArticleHandlerTests pass  
✅ Build succeeds with 0 errors  
✅ Legacy [Ignore] tests still discoverable  
✅ No conflicts between old and new tests  
✅ Clear migration path documented  

---

## **NEXT ACTION**

**👉 Ready to create `SaveArticleHandlerTests.cs`?**

Say "Yes" and I'll:
1. Create the full test file with 8 comprehensive test methods
2. Run build to verify 0 errors
3. Document in README
4. Confirm all tests pass

---

## **SUMMARY**

| Aspect | Status | Action |
|--------|--------|--------|
| **Controller Refactoring** | ✅ COMPLETE | No changes needed |
| **Legacy Tests** | ✅ MARKED [Ignore] | Keep for reference |
| **Handler Tests** | 📋 READY | Create SaveArticleHandlerTests.cs |
| **Build Status** | ✅ SUCCESSFUL | Current: 0 errors |
| **Documentation** | 📝 UPDATED | This plan |

**Overall Status: ✅ READY TO PROCEED**
