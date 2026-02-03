# Sky.Editor Test Coverage Strategy

## Current Coverage Status
- **Sky.Editor Assembly**: 15.4% (5,704 / 36,808 lines)
- **Total Project Coverage**: 19.2%

## Priority Test Areas (High Impact, Low Coverage)

### 🎯 Tier 1: Critical Business Logic (Immediate Focus)

#### 1. **EditorController** (14.3% → Target: 80%)
**Lines**: 161/1120 covered | **Impact**: Core editing functionality

**Test Cases to Add**:
- ✅ `Edit_ReturnsViewModel_WhenArticleExists` 
- ✅ `Edit_Post_UpdatesArticle_WhenValid`
- ✅ `Create_Post_CreatesNewArticle_WithValidData`
- ✅ `Delete_Post_MarksArticleAsDeleted`
- ✅ `Publish_Post_PublishesArticle_WithCdnPurge`
- ✅ `Clone_Post_CreatesArticleCopy_WithNewTitle`
- ✅ `Versions_Get_ReturnsVersionHistory`
- ✅ `Compare_Get_ShowsDiffBetweenVersions`
- ✅ `EditCode_Post_UpdatesHtmlAndHead`
- ✅ `Scheduler_Post_SetsPublishSchedule`

**Estimated Coverage Gain**: ~900 lines (+9% overall)

---

#### 2. **HomeController** (0% → Target: 70%)
**Lines**: 0/186 covered | **Impact**: Public-facing pages

**Test Cases to Add**:
- ✅ `Index_ReturnsHomepage_WhenTitleIsHome`
- ✅ `Index_Returns404_WhenPageNotFound`
- ✅ `Preview_RendersArticle_WithLayoutApplied`
- ✅ `Export_Post_GeneratesZipArchive`
- ✅ `NotFound_Returns404View`

**Estimated Coverage Gain**: ~130 lines (+1.3% overall)

---

#### 3. **TemplatesController** (14.3% → Target: 75%)
**Lines**: 51/356 covered | **Impact**: Template management

**Test Cases to Add**:
- ✅ `Index_ReturnsTemplateList`
- ✅ `Create_Post_CreatesNewTemplate`
- ✅ `EditCode_Post_UpdatesTemplateContent`
- ✅ `Delete_Post_RemovesTemplate`
- ✅ `Designer_Get_ReturnsGrapesJsEditor`
- ✅ `Pages_Get_ShowsArticlesUsingTemplate`
- ✅ `PreviewImpact_Get_ShowsAffectedPages`
- ✅ `Edit_Post_UpdatesTemplateProperties`

**Estimated Coverage Gain**: ~260 lines (+2.6% overall)

---

#### 4. **FileManagerController** (38.9% → Target: 80%)
**Lines**: 294/754 covered | **Impact**: File upload/management

**Test Cases to Add**:
- ✅ `Upload_Post_SavesFileToStorage`
- ✅ `Delete_Post_RemovesFileFromStorage`
- ✅ `Rename_Post_RenamesFileInStorage`
- ✅ `CreateFolder_Post_CreatesNewFolder`
- ✅ `Move_Post_MovesFilesToNewFolder`
- ✅ `EditImage_Post_SavesEditedImage`
- ✅ `ImportPage_Post_ImportsHtmlAsArticle`

**Estimated Coverage Gain**: ~370 lines (+3.7% overall)

---

### 🔧 Tier 2: Services & Business Logic (Next Priority)

#### 5. **PublishingService** (51.7% → Target: 85%)
**Lines**: 226/437 covered | **Impact**: Publishing workflow

**Test Cases to Add**:
- ✅ `PublishAll_PublishesAllPages_ToBlobStorage`
- ✅ `PublishAll_PurgesCdn_WhenConfigured`
- ✅ `PublishArticle_GeneratesHtml_WithLayoutApplied`
- ✅ `PublishArticle_CopiesAssets_ToPublishFolder`
- ✅ `UnpublishArticle_RemovesFromBlobStorage`

**Estimated Coverage Gain**: ~200 lines (+2% overall)

---

#### 6. **ArticleScheduler** (73.1% → Target: 95%)
**Lines**: 131/179 covered | **Impact**: Scheduled publishing

**Test Cases to Add**:
- ✅ `ProcessScheduledPublishes_PublishesArticles_AtScheduledTime`
- ✅ `ProcessScheduledPublishes_SkipsArticles_NotYetDue`
- ✅ `CancelScheduledPublish_RemovesFromQueue`

**Estimated Coverage Gain**: ~40 lines (+0.4% overall)

---

#### 7. **ArticleEditLogic** (78.1% → Target: 90%)
**Lines**: 332/425 covered | **Impact**: Article business logic

**Test Cases to Add**:
- ✅ `ValidateArticle_ReturnsErrors_WhenInvalidTitle`
- ✅ `UpdateArticleTitle_CreatesRedirect_WhenPublished`
- ✅ `DuplicateArticle_PreservesContent_ChangesTitle`

**Estimated Coverage Gain**: ~50 lines (+0.5% overall)

---

#### 8. **TitleChangeService** (82.5% → Target: 95%)
**Lines**: 293/355 covered | **Impact**: URL redirect management

**Test Cases to Add**:
- ✅ `ChangeTitle_CreatesRedirect_WhenTitleChanges`
- ✅ `ChangeTitle_UpdatesCatalog_WithNewUrl`
- ✅ `GetRedirects_ReturnsAllRedirects_ForArticle`

**Estimated Coverage Gain**: ~50 lines (+0.5% overall)

---

### ⚙️ Tier 3: Setup & Configuration (Medium Priority)

#### 9. **SetupService** (50.3% → Target: 75%)
**Lines**: 425/844 covered | **Impact**: First-run setup

**Test Cases to Add**:
- ✅ `ConfigureStorage_SavesAzureBlobConfig`
- ✅ `CreateAdminAccount_CreatesUserAndRole`
- ✅ `ConfigureEmail_SavesSendGridConfig`
- ✅ `ConfigureCdn_SavesCloudflareCdnConfig`
- ✅ `CompleteSetup_MarksSetupComplete`

**Estimated Coverage Gain**: ~320 lines (+3.2% overall)

---

#### 10. **Step1_Storage through Step6_Review Pages** (0% → Target: 60%)
**Lines**: 0/~600 combined | **Impact**: Setup wizard

**Test Cases to Add**:
- ✅ `Step1_Get_ReturnsStorageConfigPage`
- ✅ `Step1_Post_SavesStorageConfig_RedirectsToStep2`
- ✅ `Step2_Post_CreatesAdminAccount_RedirectsToStep3`
- ✅ `Step6_Post_CompletesSetup_RedirectsToHome`

**Estimated Coverage Gain**: ~360 lines (+3.6% overall)

---

### 🛠️ Tier 4: SignalR Hubs & Real-Time Features (Lower Priority)

#### 11. **ChatHub & LiveEditorHub** (0% → Target: 50%)
**Lines**: 0/128 combined | **Impact**: Real-time collaboration

**Test Cases to Add**:
- ✅ `ChatHub_BroadcastMessage_ToAllUsers`
- ✅ `LiveEditorHub_NotifyEdit_ToOtherEditors`
- ✅ `LiveEditorHub_LockArticle_WhenEditorJoins`

**Estimated Coverage Gain**: ~64 lines (+0.6% overall)

---

### 📊 Not Worth Testing (Low ROI)

These areas have low coverage but are not cost-effective to test:

- **Razor Views/Pages** (0% coverage): Auto-generated ASP.NET Core code; better tested via integration tests
- **EF Migrations** (~18,000 lines at 0%): Auto-generated database migration code; tested via migration execution
- **Program.cs** (0% coverage): Startup code; better tested via integration tests
- **Models/DTOs** (many at 0%): Simple POCOs; low business logic
- **Middleware** (0%-50%): Better tested via integration tests

---

## Implementation Plan

### Phase 1: Quick Wins (1-2 weeks, +12% coverage)
- EditorController core actions
- HomeController basic actions
- TemplatesController CRUD
- FileManagerController upload/delete

### Phase 2: Business Logic (2-3 weeks, +6% coverage)
- PublishingService
- ArticleScheduler
- TitleChangeService
- ArticleEditLogic

### Phase 3: Setup & Configuration (1-2 weeks, +7% coverage)
- SetupService
- Setup wizard pages

### Phase 4: Advanced Features (1 week, +1% coverage)
- SignalR hubs
- CDN integration edge cases

---

## Testing Approach

### Unit Tests (Primary Focus)
- **Target**: Controllers, Services, Business Logic
- **Tools**: xUnit, Moq, FluentAssertions
- **Pattern**: Arrange-Act-Assert
- **Dependencies**: Mock DbContext, IStorageContext, etc.

### Integration Tests (Secondary)
- **Target**: End-to-end workflows (publish, schedule, setup)
- **Tools**: WebApplicationFactory, TestContainers
- **Pattern**: Given-When-Then

### Test Fixtures
```csharp
public class EditorControllerTests
{
    private readonly Mock<ApplicationDbContext> _dbContextMock;
    private readonly Mock<IStorageContext> _storageMock;
    private readonly Mock<ArticleEditLogic> _articleLogicMock;
    private readonly EditorController _controller;

    public EditorControllerTests()
    {
        _dbContextMock = new Mock<ApplicationDbContext>();
        _storageMock = new Mock<IStorageContext>();
        _articleLogicMock = new Mock<ArticleEditLogic>();
        _controller = new EditorController(
            _dbContextMock.Object,
            _storageMock.Object,
            _articleLogicMock.Object
        );
    }

    [Fact]
    public async Task Edit_ReturnsViewModel_WhenArticleExists()
    {
        // Arrange
        var article = new Article { Id = 1, Title = "Test" };
        _dbContextMock.Setup(x => x.Articles.FindAsync(1))
            .ReturnsAsync(article);

        // Act
        var result = await _controller.Edit(1);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ArticleViewModel>(viewResult.Model);
        Assert.Equal("Test", model.Title);
    }
}
```

---

## Expected Outcomes

| Phase | Weeks | Lines Added | Coverage Gain | New Coverage |
|-------|-------|-------------|---------------|--------------|
| **Baseline** | - | - | - | **15.4%** |
| **Phase 1** | 2 | ~1,560 | +12% | **27.4%** |
| **Phase 2** | 3 | ~340 | +6% | **33.4%** |
| **Phase 3** | 2 | ~680 | +7% | **40.4%** |
| **Phase 4** | 1 | ~64 | +1% | **41.4%** |
| **Total** | **8 weeks** | **~2,644** | **+26%** | **41.4%** |

---

## Key Recommendations

1. **Start with EditorController**: Highest ROI, core functionality
2. **Use Test-Driven Development (TDD)**: Write tests before fixing bugs
3. **Focus on Business Value**: Test workflows users care about
4. **Avoid Over-Testing**: Skip auto-generated code and simple DTOs
5. **Integration Tests for Middleware**: Unit tests won't catch pipeline issues
6. **Mock External Dependencies**: Storage, CDN, Email services
7. **Use Test Data Builders**: Simplify test setup with builder pattern
8. **Parallelize Test Execution**: Use `[Collection]` attributes wisely

---

## Tools & Libraries Needed

```xml
<PackageReference Include="xunit" Version="2.9.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="FluentAssertions" Version="6.12.1" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.0" />
<PackageReference Include="Testcontainers" Version="3.10.0" />
```

---

## Success Metrics

- ✅ **Coverage**: 41%+ (from 15.4%)
- ✅ **Critical Paths**: 80%+ coverage on core workflows
- ✅ **Business Logic**: 85%+ coverage on services
- ✅ **Controllers**: 70%+ coverage on main controllers
- ✅ **Regressions**: Zero critical bugs in tested areas
- ✅ **CI Integration**: All tests pass on every commit

---

## Next Steps

1. **Review this strategy** with the team
2. **Create test project** `Sky.Editor.Tests` if not exists
3. **Set up test infrastructure** (mocks, fixtures, utilities)
4. **Implement Phase 1** (quick wins)
5. **Track progress** with coverage reports in CI
6. **Iterate** based on feedback and findings

---

**Author**: AI Analysis  
**Date**: 2026-02-02  
**Version**: 1.0
