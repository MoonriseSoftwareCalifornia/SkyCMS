# DRY Analysis: FileManagerController & VsCodeController

**Date:** 2025-01-XX  
**Analyzed Files:**
- `Editor/Controllers/FileManagerController.cs` (4220 lines)
- `Editor/Controllers/VsCodeController.cs` (2154 lines)

---

## Executive Summary

Both controllers are reasonably DRY within themselves, but there are **cross-controller opportunities** for shared abstractions and several **intra-controller** refactoring opportunities.

**Priority:**
1. 🔴 **High**: Copyright header error (FileManagerController)
2. 🟡 **Medium**: Shared folder listing logic between controllers
3. 🟡 **Medium**: Repetitive CQRS fallback pattern in FileManagerController
4. 🟢 **Low**: Minor validation/error pattern consolidation

---

## 🔴 Critical Issues

### 1. Incorrect Copyright Header in FileManagerController

**Location:** `FileManagerController.cs:1`

```csharp
// <copyright file="ElFinderConnectorController.cs" company="Moonrise Software, LLC">
```

**Issue:** File header references old controller name `ElFinderConnectorController.cs` instead of `FileManagerController.cs`.

**Fix:**
```csharp
// <copyright file="FileManagerController.cs" company="Moonrise Software, LLC">
```

**Impact:** Documentation accuracy, file identification.

---

## 🟡 Medium Priority Issues

### 2. Shared Dependencies - IFolderListingService Usage

**Both controllers inject and use `IFolderListingService`:**

**FileManagerController** (line 132, 164, 179):
```csharp
private readonly IFolderListingService folderListingService;
```

**VsCodeController** (line 72, 101, 114):
```csharp
private readonly IFolderListingService folderListingService;
```

**Observation:** Both controllers share the same folder-listing service for article/template folder enumeration. This is already DRY through shared service injection, **but there may be duplicated _usage patterns_** worth consolidating.

**Recommendation:** ✅ Already DRY via DI. No action needed unless usage patterns are duplicated (requires deeper analysis of usage sites).

---

### 3. Tenant Domain Resolution Pattern

**Both controllers need tenant domain:**

**FileManagerController:**
```csharp
private readonly IDynamicConfigurationProvider configProvider;
// Used in Index method and elsewhere
var tenantDomain = this.configProvider.GetTenantDomainNameFromRequest();
```

**VsCodeController:**
```csharp
private readonly IDynamicConfigurationProvider configProvider;
// Used in CompleteBrowserAuth (line 199)
var domain = configProvider.GetTenantDomainNameFromRequest();
var connection = await configProvider.GetTenantConnectionAsync(domain);
```

**Recommendation:** ✅ Already DRY via DI. Both correctly inject and use `IDynamicConfigurationProvider`. Pattern is consistent.

---

### 4. Authorization Pattern in VsCodeController

**VsCodeController has a repetitive authorization guard pattern:**

**Repetition:** Lines 385, 419, 464, 495, 516, 558, 609, 642, 678, 725, 756, etc.

```csharp
var authResult = EnsureVsCodeRequestAuthorized();
if (authResult != null)
{
	return authResult;
}
```

**DRY Violation:** This pattern appears **20+ times** across the controller.

**Recommendation:** Extract to an authorization filter or use ASP.NET Core authorization policies.

**Potential Fix:**
```csharp
// Create custom authorization attribute
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class VsCodeAuthorizeAttribute : TypeFilterAttribute
{
	public VsCodeAuthorizeAttribute() : base(typeof(VsCodeAuthorizationFilter))
	{
	}
}

// Authorization filter
public class VsCodeAuthorizationFilter : IAsyncAuthorizationFilter
{
	public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
	{
		var controller = context.Controller as VsCodeController;
		if (controller == null) return;

		var authResult = controller.EnsureVsCodeRequestAuthorized();
		if (authResult != null)
		{
			context.Result = authResult;
		}
	}
}

// Then apply once per controller or globally:
[VsCodeAuthorize]
[HttpGet("layouts")]
public async Task<IActionResult> GetLayouts()
{
	// No auth check needed here
	var layout = await GetLayoutForEdit();
	return Ok(...);
}
```

**Impact:** Would eliminate ~20 guard blocks and improve maintainability.

---

### 5. CQRS Command Fallback Pattern in FileManagerController

**Repetitive pattern across 15+ command handlers:**

**Example from lines 343-362, 364-415, 417-458, 460-496, etc.:**

```csharp
private async Task<IActionResult> HandleTreeViaCqrsAsync()
{
	var mediator = GetElFinderMediatorOrNull();
	if (mediator == null)
	{
		logger.LogWarning("elFinder CQRS tree requested but MediatR.IMediator is not registered; falling back to legacy handler.");
		return await HandleTreeAsync();
	}

	var command = new TreeCommand
	{
		Target = GetParam("target"),
		Filter = GetParam("filter"),
		VolumeId = VolumeId,
	};

	var response = await mediator.Send(command);
	var mappedError = TranslateCqrsErrorToLegacy(this, response);
	return mappedError ?? JsonCqrs(response);
}
```

**DRY Violation:** The mediator null check, logging, and fallback pattern is repeated 15+ times.

**Recommendation:** Extract to a helper method:

```csharp
private async Task<IActionResult> ExecuteCqrsCommandOrFallback<TCommand, TResponse>(
	TCommand command,
	Func<Task<IActionResult>> fallbackHandler,
	string commandName)
	where TCommand : IRequest<TResponse>
	where TResponse : IElFinderResponse
{
	var mediator = GetElFinderMediatorOrNull();
	if (mediator == null)
	{
		logger.LogWarning("elFinder CQRS {CommandName} requested but MediatR.IMediator is not registered; falling back to legacy handler.", commandName);
		return await fallbackHandler();
	}

	var response = await mediator.Send(command);
	var mappedError = TranslateCqrsErrorToLegacy(this, response);
	return mappedError ?? JsonCqrs(response);
}

// Usage:
private async Task<IActionResult> HandleTreeViaCqrsAsync()
{
	var command = new TreeCommand
	{
		Target = GetParam("target"),
		Filter = GetParam("filter"),
		VolumeId = VolumeId,
	};

	return await ExecuteCqrsCommandOrFallback(command, HandleTreeAsync, "tree");
}
```

**Impact:** Would eliminate ~150 lines of boilerplate across 15 command handlers.

---

## 🟢 Low Priority Issues

### 6. Error View Model Construction in VsCodeController

**Repetitive error model creation pattern:**

**Lines 157-162, 167-172, 178-184:**

```csharp
var errorModel = new Sky.Cms.Models.VsCodeAuthViewModel
{
	ErrorMessage = "...",
	VsCodeCallbackUri = BuildVsCodeErrorUri("...", "..."),
};
return View("AuthFailed", errorModel);
```

**Recommendation:** Extract to helper:

```csharp
private IActionResult AuthFailed(string message, string errorCode, string errorDescription, int statusCode = 400)
{
	var errorModel = new Sky.Cms.Models.VsCodeAuthViewModel
	{
		ErrorMessage = message,
		VsCodeCallbackUri = BuildVsCodeErrorUri(errorCode, errorDescription),
	};
	Response.StatusCode = statusCode;
	return View("AuthFailed", errorModel);
}

// Usage:
if (string.IsNullOrWhiteSpace(state))
{
	return AuthFailed(
		"Missing auth state parameter. Please start sign-in again from VS Code.",
		"invalid_request",
		"Missing auth state parameter.");
}
```

---

### 7. Role Resolution Logic in VsCodeController

**Lines 1479-1492:**

```csharp
private string? ResolveUserRole()
{
	if (User.IsInRole("Administrators"))
	{
		return "Administrators";
	}

	if (User.IsInRole("Editors"))
	{
		return "Editors";
	}

	return null;
}
```

**Observation:** This is a simple priority-check pattern and is already fairly DRY. Could be made more extensible if more roles are added in the future using a role priority dictionary, but current implementation is acceptable.

---

### 8. Field Update Switch Statements

**VsCodeController has two nearly identical switch patterns:**

**GetLayoutField** (lines 621-629):
```csharp
return fieldKey.ToLowerInvariant() switch
{
	"layoutname" => Ok(new { value = layout.LayoutName }),
	"notes" => Ok(new { content = layout.Notes }),
	"head" => Ok(new { content = layout.Head }),
	"header" => Ok(new { content = layout.HtmlHeader }),
	"footer" => Ok(new { content = layout.FooterHtmlContent }),
	_ => NotFound(),
};
```

**SetLayoutField** (lines 690-709):
```csharp
switch (fieldKey.ToLowerInvariant())
{
	case "layoutname":
		layout.LayoutName = request.Value ?? string.Empty;
		break;
	case "notes":
		layout.Notes = request.Content ?? string.Empty;
		break;
	// etc.
}
```

**Same pattern for Template** (lines 737-743, 778-794).

**Recommendation:** Consider a field mapping dictionary:

```csharp
private static readonly Dictionary<string, LayoutFieldAccessor> LayoutFieldMap = new()
{
	["layoutname"] = new(l => l.LayoutName, (l, v) => l.LayoutName = v.Value ?? string.Empty, isValueField: true),
	["notes"] = new(l => l.Notes, (l, v) => l.Notes = v.Content ?? string.Empty),
	["head"] = new(l => l.Head, (l, v) => l.Head = v.Content ?? string.Empty),
	// etc.
};

record LayoutFieldAccessor(
	Func<Layout, string> Getter,
	Action<Layout, FieldUpdateRequest> Setter,
	bool isValueField = false);
```

**Impact:** Moderate complexity increase for moderate duplication reduction. Current explicit switches are acceptable for maintainability.

---

## ✅ Already DRY Patterns

### 1. Shared Services via DI
Both controllers correctly use dependency injection for shared services:
- `IStorageContext`
- `IFolderListingService`
- `IDynamicConfigurationProvider`
- `IMediator`
- `IMemoryCache`
- `ApplicationDbContext`

**Status:** ✅ Well-architected, no duplication.

---

### 2. Static Utility Methods in FileManagerController

**Lines 81-94 (FixPath), 103-119 (GetImageAssetArray):**

These are `public static` helpers, meaning they're designed for reuse. No DRY violation.

---

### 3. Constructor Overloading in FileManagerController

**Lines 187-206:** Test-friendly constructor that chains to main constructor with defaults.

**Status:** ✅ Good pattern for backward compatibility and testability.

---

## Recommendations Summary

| Priority | Issue | Lines of Code Saved | Complexity | Recommended? |
|----------|-------|---------------------|------------|--------------|
| 🔴 High | Fix copyright header | 1 line fix | Trivial | **Yes** |
| 🟡 Medium | VsCode auth guard extraction | ~40 lines | Low-Medium | **Yes** |
| 🟡 Medium | CQRS fallback helper | ~150 lines | Medium | **Yes** |
| 🟢 Low | Error view helper | ~20 lines | Low | Optional |
| 🟢 Low | Field mapping dictionary | ~30 lines | Medium | No (prefer clarity) |

---

## Proposed Action Plan

### Phase 1: Critical Fix
1. Fix `FileManagerController.cs` copyright header

### Phase 2: High-Value Refactoring
2. Extract `VsCodeAuthorizationFilter` attribute for VsCodeController
3. Extract `ExecuteCqrsCommandOrFallback<TCommand, TResponse>` helper in FileManagerController

### Phase 3: Polish (Optional)
4. Extract `AuthFailed` helper in VsCodeController
5. Monitor for additional shared patterns as codebase evolves

---

## Conclusion

**Overall Assessment:** Both controllers are **reasonably well-structured** with minimal DRY violations. The most impactful improvements are:

1. ✅ **Fix the copyright header** (trivial, high correctness value)
2. ✅ **Extract VsCode authorization filter** (eliminates 20+ guard blocks)
3. ✅ **Extract CQRS fallback helper** (eliminates 150+ lines of boilerplate)

The shared service dependencies (IFolderListingService, IDynamicConfigurationProvider) are already DRY via dependency injection, which is the correct architectural pattern.

**No major cross-controller duplication detected** — the controllers serve different purposes (file management vs. VS Code API) and their shared concerns are already abstracted into services.
