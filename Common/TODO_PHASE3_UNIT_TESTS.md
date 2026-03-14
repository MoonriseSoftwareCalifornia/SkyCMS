# Phase 3 - Unit Testing TODO

## Overview
This document tracks unit test creation for Cosmos.Common project.  
**NOTE:** Test creation is deferred to avoid conflicts with ongoing unit test refactoring session.

## Test Project Setup
- Test project `Cosmos.Common.Tests` was created but should be removed/recreated in the test refactoring session
- Uses MSTest framework with Moq for mocking
- Uses InMemory database for EF Core tests
- Package references needed:
  - Microsoft.NET.Test.Sdk
  - MSTest.TestAdapter
  - MSTest.TestFramework
  - Moq
  - Microsoft.EntityFrameworkCore.InMemory
  - coverlet.collector

---

## Priority 1: CQRS Query Handler Tests (Phase 1 & 2)

### Phase 1 - ArticleLogic Migration Tests

#### GetSitemapQueryHandler Tests
**File:** `Common.Tests/Features/Sitemap/GetSitemapQueryHandlerTests.cs`

Test Scenarios:
- ✅ `HandleAsync_WithPublishedPages_ReturnsSitemap` - Verify sitemap generation with published articles
- ✅ `HandleAsync_WithNoPublishedPages_ReturnsEmptySitemap` - Handle empty state
- ✅ `HandleAsync_WithFuturePublishedDate_ExcludesFromSitemap` - Verify future articles excluded
- 📝 Add: Test with multiple articles, different update dates, URL encoding

#### GetDefaultLayoutQueryHandler Tests
**File:** `Common.Tests/Features/Layouts/GetDefaultLayoutQueryHandlerTests.cs`

Test Scenarios:
- ✅ `HandleAsync_WithDefaultLayout_ReturnsLayout` - Returns correct default layout
- ✅ `HandleAsync_WithMultipleVersions_ReturnsLatestVersion` - Version ordering works
- ✅ `HandleAsync_WithNoDefaultLayout_ReturnsNull` - Handle missing layout
- ✅ `HandleAsync_WithFuturePublishDate_ReturnsNull` - Future layouts excluded
- 📝 Add: Test caching behavior, non-default layouts filtered out

#### BuildArticleViewModelQueryHandler Tests
**File:** `Common.Tests/Features/Articles/Queries/BuildArticleViewModelQueryHandlerTests.cs`

Test Scenarios:
- 📝 Test delegation to IArticleViewModelBuilder service
- 📝 Test with valid article and language
- 📝 Test with/without layout inclusion
- 📝 Mock IArticleViewModelBuilder and verify calls
- 📝 Test null article handling
- 📝 Test different language codes

#### BuildPublishedPageViewModelQueryHandler Tests
**File:** `Common.Tests/Features/Articles/Queries/BuildPublishedPageViewModelQueryHandlerTests.cs`

Test Scenarios:
- 📝 Test delegation to IArticleViewModelBuilder service
- 📝 Test with valid PublishedPage
- 📝 Test with/without layout inclusion
- 📝 Mock IArticleViewModelBuilder and verify calls
- 📝 Test null PublishedPage handling

### Phase 2a - LayoutHelper Migration Tests

#### CheckDefaultLayoutExistsQueryHandler Tests
**File:** `Common.Tests/Features/Layouts/CheckDefaultLayoutExistsQueryHandlerTests.cs`

Test Scenarios:
- 📝 `HandleAsync_WithDefaultLayout_ReturnsTrue` - Layout exists
- 📝 `HandleAsync_WithNoDefaultLayout_ReturnsFalse` - No layout
- 📝 `HandleAsync_WithFutureLayout_ReturnsFalse` - Future layouts ignored
- 📝 `HandleAsync_WithMultipleLayouts_ReturnsTrue` - Any published default counts

#### GetLayoutByIdQueryHandler Tests
**File:** `Common.Tests/Features/Layouts/GetLayoutByIdQueryHandlerTests.cs`

Test Scenarios:
- 📝 `HandleAsync_WithValidId_ReturnsLayout` - Layout found
- 📝 `HandleAsync_WithInvalidId_ReturnsNull` - Not found
- 📝 `HandleAsync_WithEmptyGuid_ReturnsNull` - Empty GUID handling
- 📝 `HandleAsync_WithNonDefaultLayout_ReturnsLayout` - Non-default layouts also returned

### Phase 2c - CosmosUtilities Migration Tests

#### AuthorizeUserForArticleQueryHandler Tests
**File:** `Common.Tests/Features/Articles/AuthorizeUserForArticleQueryHandlerTests.cs`

Test Scenarios:
- ✅ `HandleAsync_AnonymousAccessAllowed_ReturnsTrue` - ANONYMOUS permission
- ✅ `HandleAsync_AuthenticatedAccessRequired_AuthenticatedUserReturnsTrue` - AUTHENTICATED permission
- ✅ `HandleAsync_UserSpecificPermission_GrantsAccess` - User-specific permissions
- ✅ `HandleAsync_RoleBasedPermission_GrantsAccessToUserInRole` - Role-based permissions
- ✅ `HandleAsync_NoPermissions_ReturnsFalse` - No access by default
- ✅ `HandleAsync_ArticleNotFound_ReturnsFalse` - Missing article handling
- 📝 Add: Test multiple permissions, test unauthenticated user vs AUTHENTICATED permission

#### GetArticleFolderContentsQueryHandler Tests
**File:** `Common.Tests/Features/Articles/Queries/GetArticleFolderContentsQueryHandlerTests.cs`

Test Scenarios:
- 📝 `HandleAsync_WithValidArticleNumber_ReturnsContents` - Successful retrieval
- 📝 `HandleAsync_WithPath_ReturnsSubfolderContents` - Subfolder navigation
- 📝 `HandleAsync_WithEmptyPath_ReturnsRootContents` - Default path
- 📝 Mock IStorageContext and verify path construction
- 📝 Test path sanitization/security
- ⚠️ **IMPORTANT:** Document that authorization must be done separately (security note)

#### GetArticlesForUserQueryHandler Tests
**File:** `Common.Tests/Features/Articles/Queries/GetArticlesForUserQueryHandlerTests.cs`

Test Scenarios:
- 📝 `HandleAsync_WithUserRoles_ReturnsAuthorizedArticles` - Role-based filtering
- 📝 `HandleAsync_WithAnonymousUser_ReturnsPublicArticles` - Public articles only
- 📝 `HandleAsync_WithAuthenticatedUser_ReturnsAuthenticatedArticles` - Authenticated access
- 📝 `HandleAsync_WithNoArticles_ReturnsEmptyList` - Empty state
- 📝 Test articles with multiple permissions
- 📝 Test user with multiple roles
- 📝 Verify TableOfContentsItem projection

---

## Priority 2: Utility Class Tests

### ArticleLogicUtilities Tests
**File:** `Common.Tests/Utilities/ArticleLogicUtilitiesTests.cs`

Test Scenarios:
- ✅ `Serialize_ValidObject_ReturnsJsonString` - JSON serialization
- ✅ `Serialize_NullObject_ReturnsEmptyJsonObject` - Null handling
- ✅ `Deserialize_ValidJson_ReturnsObject` - JSON deserialization
- ✅ `Deserialize_NullJson_ThrowsArgumentNullException` - Null JSON throws
- ✅ `GetPublisherHealth_ReturnsNonEmptyString` - Health check returns value
- ✅ `GetPublisherHealth_ReturnsConsistentValue` - Consistent results
- ✅ `Serialize_ComplexObject_PreservesStructure` - Round-trip serialization
- 📝 Add: Test with special characters, test with circular references (should fail gracefully)

### SecurePasswordGenerator Tests
**File:** `Common.Tests/Utilities/SecurePasswordGeneratorTests.cs`

Test Scenarios:
- ✅ `GeneratePassword_DefaultLength_Returns32CharacterPassword` - Default behavior
- ✅ `GeneratePassword_CustomLength_ReturnsCorrectLength` - Custom length
- ✅ `GeneratePassword_LengthLessThan16_ThrowsArgumentException` - Validation
- ✅ `GeneratePassword_WithSpecialChars_ContainsSpecialCharacters` - Special chars included
- ✅ `GeneratePassword_WithoutSpecialChars_NoSpecialCharacters` - No special chars
- ✅ `GeneratePassword_HasUppercaseLetter` - Complexity requirement
- ✅ `GeneratePassword_HasLowercaseLetter` - Complexity requirement
- ✅ `GeneratePassword_HasDigit` - Complexity requirement
- ✅ `GeneratePassword_MultipleCallsGenerateDifferentPasswords` - Randomness
- ✅ `GenerateUrlSafeToken_DefaultLength_Returns43Characters` - Token length
- ✅ `GenerateUrlSafeToken_NoUrlUnsafeCharacters` - URL-safe validation
- ✅ `GenerateUrlSafeToken_OnlyValidBase64UrlCharacters` - Character set validation
- ✅ `GenerateUrlSafeToken_CustomByteLength_ReturnsAppropriateLength` - Custom length
- ✅ `GenerateUrlSafeToken_MultipleCallsGenerateDifferentTokens` - Randomness
- ✅ `GeneratePassword_MinimumLength16_WorksCorrectly` - Boundary test

### CosmosLinqExtensions Tests
**File:** `Common.Tests/Data/CosmosLinqExtensionsTests.cs`

Test Scenarios:
- 📝 Test all LINQ extension methods
- 📝 Test with InMemory database
- 📝 Verify query translation
- 📝 Test edge cases (empty collections, null values)

---

## Priority 3: Service Interface Tests (Once Extracted)

### IArticleViewModelBuilder Tests
**File:** `Common.Tests/Features/Articles/Shared/ArticleViewModelBuilderTests.cs`

Test Scenarios:
- 📝 Mock dependencies (IMediator, IApplicationDbContext, etc.)
- 📝 Test BuildArticleViewModelAsync with Article
- 📝 Test BuildArticleViewModel with PublishedPage
- 📝 Test layout inclusion/exclusion
- 📝 Test language handling
- 📝 Test caching behavior (if applicable)

---

## Priority 4: Configuration Tests

### Email Configuration Tests
**File:** `Common.Tests/Services/Email/EmailSettingsTests.cs`

Test Scenarios:
- 📝 Test validation attributes (Required)
- 📝 Test property initialization
- 📝 Test IOptions<EmailSettings> binding from configuration

### OAuth Configuration Tests
**File:** `Common.Tests/Services/Configurations/OAuthTests.cs`

Test Scenarios:
- 📝 Test init accessors prevent modification
- 📝 Test Display attributes for UI rendering
- 📝 Test configuration binding

### AzureAD Configuration Tests
**File:** `Common.Tests/Services/Configurations/AzureADTests.cs`

Test Scenarios:
- 📝 Test init accessors prevent modification
- 📝 Test Display attributes
- 📝 Test configuration binding

---

## Testing Best Practices to Follow

### General Guidelines
1. **AAA Pattern:** Arrange-Act-Assert in all tests
2. **Single Responsibility:** One assertion per test where possible
3. **Descriptive Names:** Use `MethodName_Scenario_ExpectedResult` naming
4. **Test Independence:** Each test should be isolated, no shared state
5. **Mock External Dependencies:** Use Moq for IMediator, IStorageContext, etc.

### CQRS Testing Pattern
```csharp
[TestMethod]
public async Task HandleAsync_Scenario_ExpectedResult()
{
    // Arrange
    var dbContext = CreateInMemoryDbContext();
    var handler = new YourQueryHandler(dbContext);
    var query = new YourQuery(parameters);
    
    // Act
    var result = await handler.HandleAsync(query, CancellationToken.None);
    
    // Assert
    Assert.IsNotNull(result);
    Assert.AreEqual(expected, result.Property);
}
```

### Mocking IMediator
```csharp
var mediatorMock = new Mock<IMediator>();
mediatorMock
    .Setup(m => m.QueryAsync(It.IsAny<YourQuery>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(expectedResult);
```

### InMemory Database Setup
```csharp
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
    .Options;
var dbContext = new ApplicationDbContext(options);
```

---

## Coverage Goals

### Minimum Coverage Targets
- **Query Handlers:** 90%+ code coverage
- **Utility Classes:** 95%+ code coverage
- **Service Implementations:** 80%+ code coverage
- **Configuration Classes:** 70%+ (validation logic)

### Critical Paths to Test
1. ✅ Authorization logic (security-critical)
2. ✅ Data retrieval queries
3. ✅ Serialization/deserialization
4. ✅ Password generation (security-critical)
5. 📝 Layout selection logic
6. 📝 Article view model building
7. 📝 Sitemap generation

---

## Integration with Test Refactoring Session

**Coordination Notes:**
- Test project structure should follow existing Sky.Tests patterns
- Use same test base classes if applicable
- Follow existing naming conventions in Sky.Tests
- Ensure test categories/traits align with existing test organization
- Consider existing test helper utilities (e.g., `TestableConfigurationProvider`)

**Recommended Approach:**
1. Wait for test refactoring session to complete
2. Review new test patterns/structure
3. Create Cosmos.Common.Tests following established patterns
4. Use this TODO as a test coverage checklist
5. Add tests incrementally, validating with each batch

---

## Status Tracking

- ✅ **Completed:** Test scenarios fully defined
- ⏳ **Pending:** Test implementation (blocked by test refactoring session)
- 📝 **Future:** Additional scenarios discovered during implementation

**Last Updated:** Phase 3 - Enum extraction complete, test creation deferred
