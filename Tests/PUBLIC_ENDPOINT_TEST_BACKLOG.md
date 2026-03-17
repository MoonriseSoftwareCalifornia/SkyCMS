# Public Endpoint Unit Test Backlog

## Objective
Achieve 100% unit test coverage of all public controller endpoints in Sky.Editor and Sky.Publisher, with no duplicate test intent and safe parallel execution.

## Scope and rules
1. Scope includes public action methods that are routable from controller classes in Editor/Controllers and Publisher/Controllers.
2. Existing coverage should be extended, not duplicated.
3. Every endpoint must have at least one unit test.
4. Endpoints with auth, validation, or branching behavior should have multiple tests that cover distinct behavior paths.

## Non-duplication policy
1. One owning test class per controller.
2. One canonical test per endpoint behavior path.
3. Reuse helper methods for Arrange steps instead of cloning setup blocks.
4. Use parameterized tests (DataRow or DynamicData) for equivalent path variants.
5. Do not create a second test for the same assertion shape unless it covers a different branch, role, or input class.

## Parallel-safe policy
1. No shared mutable static state across tests.
2. Keep per-test isolated storage paths and tenant context.
3. Use fresh DbContext and controller instance per test initialize.
4. Avoid time-sensitive assertions that depend on global clock precision.
5. Do not depend on test execution order.
6. Mark tests as non-parallel only when isolation is not possible, and document why.

## Priority P0: Untested endpoints
These are currently missing direct controller-level unit tests and should be completed first.

### EditorController
1. Endpoint: Designer
2. Source: Editor/Controllers/EditorController.cs
3. Candidate tests:
- Designer_ReturnsView_ForValidArticle
- Designer_ReturnsNotFound_ForMissingArticle
- Designer_RejectsUnauthorizedRole

### EmailAdminController
1. Endpoint: Index (GET)
2. Endpoint: Index (POST)
3. Source: Editor/Controllers/EmailAdminController.cs
4. Candidate tests:
- Index_Get_ReturnsViewModel
- Index_Post_SendsTestEmail_ReturnsResult
- Index_Post_InvalidModelState_ReturnsBadRequestOrView
- Index_RejectsUnauthorizedRole

### FileManagerController
1. Endpoint: Create
2. Endpoint: EditCode (GET)
3. Endpoint: EditCode (POST)
4. Endpoint: GetImageAssets
5. Endpoint: ImportPage (GET)
6. Endpoint: ImportPage (POST)
7. Endpoint: Process (POST form)
8. Endpoint: Process (PATCH)
9. Source: Editor/Controllers/FileManagerController.cs
10. Candidate tests:
- Create_CreatesEntry_ReturnsSuccess
- Create_RejectsInvalidPath
- EditCode_Get_ReturnsContentModel
- EditCode_Post_SavesContent
- EditCode_Post_RejectsInvalidModel
- GetImageAssets_ReturnsFilteredAssets
- ImportPage_Get_ReturnsImportView
- ImportPage_Post_ImportsAndReturnsResult
- Process_Post_HandlesUploadPayload
- Process_Patch_AppliesPatch
- Process_RejectsUnauthorizedPath

### TemplatesController
1. Endpoint: Designer
2. Endpoint: GetDesignerData
3. Endpoint: DesignerData (POST)
4. Endpoint: PreviewImpact
5. Endpoint: PreviewImpactJson
6. Endpoint: PublishDrafts
7. Source: Editor/Controllers/TemplatesController.cs
8. Candidate tests:
- Designer_ReturnsView_ForExistingTemplate
- Designer_ReturnsNotFound_ForMissingTemplate
- GetDesignerData_ReturnsTemplatePayload
- DesignerData_Post_UpdatesTemplateContent
- PreviewImpact_ReturnsViewWithAffectedPages
- PreviewImpactJson_ReturnsExpectedJsonShape
- PublishDrafts_PublishesSelectedArticleSet
- PublishDrafts_RejectsUnauthorizedRole

### UsersController
1. Endpoint: Privacy
2. Endpoint: Error
3. Source: Editor/Controllers/UsersController.cs
4. Candidate tests:
- Privacy_ReturnsView
- Error_ReturnsViewWithRequestId

### Sky.Publisher HomeController
1. Endpoint: CCMS___Head (HEAD Index)
2. Endpoint: Index (GET)
3. Endpoint: Error
4. Endpoint: GetMicrosoftIdentityAssociation
5. Source: Publisher/Controllers/HomeController.cs
6. Candidate tests:
- CCMS___Head_ReturnsUnauthorized_WhenAuthRequired
- CCMS___Head_ReturnsNotFound_WhenNoPublishedPage
- CCMS___Head_ReturnsOkAndSetsCacheHeaders_WhenPageExists
- Index_ReturnsBadRequest_WhenModelStateInvalid
- Index_ReturnsJson_WhenModeJson
- Index_ReturnsRedirectView_ForRedirectStatus
- Index_ReturnsNotFoundOrUnderConstruction_ForMissingArticle
- Error_ReturnsViewWithRequestId
- GetMicrosoftIdentityAssociation_ReturnsJsonFile

### Sky.Publisher StaticProxyController
1. Endpoint: Index (GET)
2. Source: Publisher/Controllers/StaticProxyController.cs
3. Candidate tests:
- Index_ReturnsRequestedFile_WhenFileExists
- Index_UsesSpaFallback_WhenRouteIsClientSide
- Index_ReturnsNotFound_WhenNoFileAndNoSpaFallback
- Index_ReturnsForbidden_OnUnauthorizedAccessException
- Index_ReturnsServerError_OnUnexpectedException

### Sky.Publisher PubController
1. Endpoint: Index (inherited from Common/PubControllerBase)
2. Source: Publisher/Controllers/PubController.cs
3. Coverage note:
- Validate this endpoint via Publisher controller instantiation path if base-only tests do not already assert derived-controller routing behavior.

## Priority P1: Adequacy upgrades for already-tested endpoints
These endpoints have coverage signals but appear shallow and should be strengthened after P0.

### Add missing auth-denial coverage where role restrictions exist
1. EditorController selected admin/editor-only endpoints.
2. TemplatesController selected admin/editor-only endpoints.
3. UsersController admin-only endpoints.
4. Add one denial test per endpoint family using role matrix, not one per tiny variant.

### Strengthen low-depth endpoints (single invocation signal)
1. BlogController: ConfirmDeleteEntry, GetBlogs, Index.
2. EditorController: GetTrashList, Restore, TrashPermanently, Versions.
3. TemplatesController: Create.
4. UsersController: GetRoles, UnconfirmEmail.
5. Upgrade pattern: happy path + invalid input path + auth path when applicable.

## Priority P2: Consistency and maintainability pass
1. Consolidate repeated setup into shared builder helpers in existing test infrastructure.
2. Standardize naming pattern: Action_Scenario_ExpectedResult.
3. Add a lightweight endpoint-to-test map document update after each batch.

## Definition of done
1. Every public endpoint in Editor/Controllers and Publisher/Controllers has one or more direct unit tests.
2. Every protected endpoint family has explicit deny-access coverage.
3. No duplicate tests with equivalent arrange-act-assert behavior.
4. Test suite remains parallel-safe and deterministic.
5. All new tests pass in the current Sky.Tests project.

## Recommended execution order
1. P0 FileManagerController
2. P0 TemplatesController
3. P0 EmailAdminController
4. P0 EditorController.Designer and UsersController view endpoints
5. P0 Sky.Publisher StaticProxyController and HomeController
6. P0 Sky.Publisher PubController derived-routing validation
7. P1 adequacy upgrades
8. P2 cleanup pass
