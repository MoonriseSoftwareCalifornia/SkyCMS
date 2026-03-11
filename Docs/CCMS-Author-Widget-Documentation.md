# CCMS Author Widget Documentation

## Overview

The CCMS Author Widget provides two approaches for displaying author information in SkyCMS:
1. **JavaScript Widget** - Client-side rendering with dynamic updates
2. **Razor Partial View** - Server-side rendering for SEO and accessibility

Both solutions support multiple display modes and configurable field visibility.

---

## JavaScript Widget

### Location
- **JS**: `Editor/wwwroot/js/ccms-author-widget.js`
- **CSS**: `Editor/wwwroot/css/ccms-author-widget.css`

### Features
- Parses both single-quoted and double-quoted JSON formats
- Four display modes: `card`, `inline`, `compact`, `detailed`
- Configurable field visibility
- Event system for widget interactions
- XSS protection via HTML escaping
- Dark mode support
- Responsive design

### Basic Usage

```html
<!-- Include the widget files in your layout -->
<link rel="stylesheet" href="~/css/ccms-author-widget.css" />
<script src="~/js/ccms-author-widget.js"></script>

<!-- Add the widget container -->
<div class="ccms-author-widget-container"
     data-editor-config="author-widget"
     data-ccms-ceid="skycms-author-info"
     data-author-json='{"AuthorName":"John Doe","EmailAddress":"john@example.com",...}'
     data-display-mode="card"
     data-show-email="true"
     data-show-social="true"
     data-show-description="true"
     data-show-website="true">
</div>
```

### Display Modes

#### 1. Compact
Minimal display - just the name, optionally linked to website.

```html
<div class="ccms-author-widget-container"
     data-editor-config="author-widget"
     data-author-json="@Model.AuthorInfo"
     data-display-mode="compact">
</div>
```

**Output**: `John Doe` or `<a href="...">John Doe</a>`

#### 2. Inline
Name with optional email and website links, separated by dots.

```html
<div class="ccms-author-widget-container"
     data-editor-config="author-widget"
     data-author-json="@Model.AuthorInfo"
     data-display-mode="inline"
     data-show-email="true"
     data-show-website="true">
</div>
```

**Output**: `John Doe · john@example.com · Website`

#### 3. Card (Default)
Standard card layout with icon buttons for contact methods.

```html
<div class="ccms-author-widget-container"
     data-editor-config="author-widget"
     data-author-json="@Model.AuthorInfo"
     data-display-mode="card"
     data-show-email="true"
     data-show-social="true"
     data-show-description="true">
</div>
```

#### 4. Detailed
Extended layout with all available information and labeled fields.

```html
<div class="ccms-author-widget-container"
     data-editor-config="author-widget"
     data-author-json="@Model.AuthorInfo"
     data-display-mode="detailed">
</div>
```

### Configuration Options

| Attribute | Type | Default | Description |
|-----------|------|---------|-------------|
| `data-author-json` | string | required | JSON string of author info (single or double quotes) |
| `data-display-mode` | string | `"inline"` | Display mode: `compact`, `inline`, `card`, or `detailed` |
| `data-show-email` | boolean | `true` | Show email address |
| `data-show-social` | boolean | `true` | Show social media links (Twitter, Instagram) |
| `data-show-description` | boolean | `true` | Show author description |
| `data-show-website` | boolean | `true` | Show website link |

### JavaScript API

```javascript
// Initialize all widgets on the page
window.CCMSAuthorWidget.initialize();

// Update a specific widget
window.CCMSAuthorWidget.update('#my-widget', {
    AuthorName: "Jane Smith",
    EmailAddress: "jane@example.com",
    Website: "https://janesmith.com"
}, {
    displayMode: 'card',
    showEmail: true
});

// Parse author JSON (handles single or double quotes)
const authorInfo = window.CCMSAuthorWidget.parseAuthorJson(jsonString);

// Access display modes
const modes = window.CCMSAuthorWidget.DisplayMode;
// modes.CARD, modes.INLINE, modes.COMPACT, modes.DETAILED
```

### Event System

```javascript
// Listen for widget events
window.CCMSAuthorWidgetEvents.on('authorRendered', function(data) {
    console.log('Author rendered:', data.authorInfo);
    console.log('Container:', data.container);
    console.log('Config:', data.config);
});

window.CCMSAuthorWidgetEvents.on('authorUpdated', function(data) {
    console.log('Author updated:', data.authorInfo);
});
```

---

## Razor Partial View

### Location
- **View**: `Sky.Shared.Razor/Views/Shared/_AuthorInfoPartial.cshtml`
- **CSS**: Uses same styles as JavaScript widget

### Features
- Accepts both `AuthorInfo` objects and JSON strings
- Four display modes (same as JS widget)
- Optional schema.org microdata for SEO
- Server-side rendering for better SEO and accessibility
- Type-safe with C# models

### Basic Usage

#### With AuthorInfo Object

```razor
@* Pass an AuthorInfo object *@
<partial name="_AuthorInfoPartial" 
         model="authorInfoObject" 
         view-data='@(new ViewDataDictionary(ViewData) { 
             { "DisplayMode", "Card" },
             { "ShowEmail", true },
             { "ShowSocial", true }
         })' />
```

#### With JSON String

```razor
@* Pass the JSON string from ArticleViewModel *@
<partial name="_AuthorInfoPartial" 
         model="@Model.AuthorInfo" 
         view-data='@(new ViewDataDictionary(ViewData) { 
             { "IsJsonString", true },
             { "DisplayMode", "Inline" },
             { "ShowEmail", false }
         })' />
```

#### Using Html.PartialAsync

```razor
@await Html.PartialAsync("_AuthorInfoPartial", Model.AuthorInfoObject, 
    new ViewDataDictionary(ViewData) {
        { "DisplayMode", "Detailed" },
        { "IncludeSchema", true },
        { "ShowEmail", true },
        { "ShowSocial", true },
        { "ShowDescription", true },
        { "ShowWebsite", true }
    })
```

### ViewData Options

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `IsJsonString` | bool | `false` | Set to `true` if model is a JSON string |
| `DisplayMode` | string | `"Inline"` | Display mode: `Compact`, `Inline`, `Card`, or `Detailed` |
| `ShowEmail` | bool | `true` | Show email address |
| `ShowSocial` | bool | `true` | Show social media links |
| `ShowDescription` | bool | `true` | Show author description |
| `ShowWebsite` | bool | `true` | Show website link |
| `IncludeSchema` | bool | `false` | Include schema.org microdata (for SEO) |

### Display Modes

Same four modes as the JavaScript widget:
- **Compact**: Name only or with link
- **Inline**: Name with optional email/website
- **Card**: Standard card with icon buttons
- **Detailed**: Extended layout with all fields

---

## When to Use Which Approach

### Use JavaScript Widget When:
- Building interactive editor interfaces
- Need dynamic updates without page reload
- Working within the CKEditor ecosystem
- Want client-side rendering to reduce server load
- Building single-page application features

### Use Razor Partial View When:
- Rendering published/public-facing pages
- SEO is important (server-side rendering)
- Need schema.org microdata
- Building traditional server-rendered pages
- Want strongly-typed, compile-time checked code

---

## Example Scenarios

### Scenario 1: Blog Post Editor (Interactive)
Use JavaScript widget for dynamic author display in the editor:

```razor
@* _BlogPostEditPartial.cshtml *@
<header>
    <h1 id="post-title" contenteditable>@Model.Title</h1>
    <p>
        <small>
            <time>@Model.Published.ToString("D")</time>
            <span class="ccms-author-widget-container"
                  data-editor-config="author-widget"
                  data-author-json="@Model.AuthorInfo"
                  data-display-mode="inline"
                  data-show-email="false"></span>
        </small>
    </p>
</header>
```

### Scenario 2: Published Blog Post (SEO-Friendly)
Use Razor partial for public-facing published posts:

```razor
@* _BlogPostPartial.cshtml *@
<article>
    <header>
        <h1>@Model.Title</h1>
        <div class="meta">
            <time>@Model.Published.ToString("D")</time>
            <partial name="_AuthorInfoPartial" 
                     model="@Model.AuthorInfo" 
                     view-data='@(new ViewDataDictionary(ViewData) { 
                         { "IsJsonString", true },
                         { "DisplayMode", "Inline" },
                         { "ShowEmail", false },
                         { "ShowWebsite", true }
                     })' />
        </div>
    </header>
    <div class="content">
        @Html.Raw(Model.Content)
    </div>
</article>
```

### Scenario 3: Author Profile Page
Use detailed mode with schema.org microdata:

```razor
<partial name="_AuthorInfoPartial" 
         model="authorInfo" 
         view-data='@(new ViewDataDictionary(ViewData) { 
             { "DisplayMode", "Detailed" },
             { "IncludeSchema", true },
             { "ShowEmail", true },
             { "ShowSocial", true },
             { "ShowDescription", true },
             { "ShowWebsite", true }
         })' />
```

### Scenario 4: Blog Card/Teaser
Use compact mode for blog listings:

```razor
<div class="blog-card">
    <h3>@article.Title</h3>
    <p class="meta">
        By <partial name="_AuthorInfoPartial" 
                    model="@article.AuthorInfo" 
                    view-data='@(new ViewDataDictionary(ViewData) { 
                        { "IsJsonString", true },
                        { "DisplayMode", "Compact" }
                    })' />
        on @article.Published.ToString("D")
    </p>
</div>
```

---

## Styling Customization

The widget uses CSS custom properties for easy theming:

```css
/* Custom theme example */
.ccms-author-card {
    --author-card-bg: #f0f0f0;
    --author-card-border: #ccc;
    --author-name-color: #333;
    --author-description-color: #666;
    --author-link-bg: #fff;
    --author-link-border: #ddd;
    --author-link-color: #555;
    --author-link-hover-bg: #e0e0e0;
}
```

---

## Migrating from Raw JSON Display

### Before (Current Implementation)
```razor
<time>@displayDate.ToString("D")</time>
@if (!string.IsNullOrWhiteSpace(Model.AuthorInfo))
{
    <text> · @Model.AuthorInfo</text>
}
```

**Output**: `January 15, 2024 · {"AuthorName":"John Doe","EmailAddress":"...",...}`

### After (Using JavaScript Widget)
```razor
<time>@displayDate.ToString("D")</time>
@if (!string.IsNullOrWhiteSpace(Model.AuthorInfo))
{
    <text> · </text>
    <span class="ccms-author-widget-container"
          data-editor-config="author-widget"
          data-author-json="@Model.AuthorInfo"
          data-display-mode="inline"
          data-show-email="false"></span>
}
```

**Output**: `January 15, 2024 · John Doe · Website`

### After (Using Razor Partial)
```razor
<time>@displayDate.ToString("D")</time>
@if (!string.IsNullOrWhiteSpace(Model.AuthorInfo))
{
    <text> · </text>
    <partial name="_AuthorInfoPartial" 
             model="@Model.AuthorInfo" 
             view-data='@(new ViewDataDictionary(ViewData) { 
                 { "IsJsonString", true },
                 { "DisplayMode", "Inline" },
                 { "ShowEmail", false }
             })' />
}
```

**Output**: `January 15, 2024 · John Doe · Website`

---

## Future Enhancement: Passing AuthorInfo Object Instead of JSON

### Current Architecture
```
AuthorInfoService → ArticleViewModelBuilder → View
    (AuthorInfo)         (JSON string)      (parse JSON)
```

### Proposed Architecture
```
AuthorInfoService → ArticleViewModelBuilder → View
    (AuthorInfo)         (AuthorInfo)       (use object)
```

### Required Changes

#### 1. Update ArticleViewModel Property
```csharp
// Common/Models/ArticleViewModel.cs

// Current:
public virtual string AuthorInfo { get; set; } = string.Empty;

// Proposed:
public virtual AuthorInfo AuthorInfoObject { get; set; }

// Keep string property for backward compatibility if needed:
[Obsolete("Use AuthorInfoObject instead")]
public virtual string AuthorInfo 
{ 
    get => AuthorInfoObject != null ? JsonConvert.SerializeObject(AuthorInfoObject) : string.Empty;
    set => AuthorInfoObject = !string.IsNullOrEmpty(value) 
        ? JsonConvert.DeserializeObject<AuthorInfo>(value.Replace("'", "\"")) 
        : null;
}
```

#### 2. Update ArticleViewModelBuilder
```csharp
// Common/Features/Articles/Shared/ArticleViewModelBuilder.cs

// Current:
var author = string.Empty;
if (!string.IsNullOrEmpty(article.UserId))
{
    var authorInfo = await dbContext.AuthorInfos
        .AsNoTracking()
        .FirstOrDefaultAsync(f => f.Id == article.UserId);
    if (authorInfo != null)
    {
        author = JsonConvert.SerializeObject(authorInfo).Replace("\"", "'");
    }
}

// Proposed:
AuthorInfo authorInfo = null;
if (!string.IsNullOrEmpty(article.UserId))
{
    authorInfo = await dbContext.AuthorInfos
        .AsNoTracking()
        .FirstOrDefaultAsync(f => f.Id == article.UserId);
}

// Then in constructor:
AuthorInfoObject = authorInfo
```

#### 3. Update Views
```razor
@* Instead of parsing JSON *@
<partial name="_AuthorInfoPartial" model="@Model.AuthorInfoObject" />

@* Or for JavaScript widget *@
<div class="ccms-author-widget-container"
     data-editor-config="author-widget"
     data-author-json="@JsonConvert.SerializeObject(Model.AuthorInfoObject)">
</div>
```

### Benefits of Object Approach
- ✅ Type-safe, compile-time checking
- ✅ Better IntelliSense support
- ✅ No JSON parsing overhead in views
- ✅ Easier to work with in C# code
- ✅ Cleaner architecture

### Considerations
- ⚠️ Requires updating all views that use AuthorInfo
- ⚠️ May affect serialization/caching if ArticleViewModel is cached
- ⚠️ Need to ensure AuthorInfo is not tracked by EF when detached
- ⚠️ Consider impact on PublishedPage entity (currently stores as string)

### Migration Strategy
1. Add `AuthorInfoObject` property alongside existing `AuthorInfo` string
2. Update `ArticleViewModelBuilder` to populate both (temporary)
3. Update all views to use new property
4. Test thoroughly
5. Mark old property as `[Obsolete]`
6. Remove old property in next major version

---

## Testing

### JavaScript Widget Testing
```javascript
// Test parsing
const json1 = '{"AuthorName":"John","EmailAddress":"john@test.com"}';
const json2 = "{'AuthorName':'John','EmailAddress':'john@test.com'}";
const author1 = window.CCMSAuthorWidget.parseAuthorJson(json1);
const author2 = window.CCMSAuthorWidget.parseAuthorJson(json2);
console.assert(author1.AuthorName === author2.AuthorName);

// Test rendering
window.CCMSAuthorWidget.initialize();
// Verify widgets appear correctly

// Test updates
window.CCMSAuthorWidget.update('.ccms-author-widget-container', {
    AuthorName: "Updated Name"
});
```

### Razor Partial Testing
Create test pages with different configurations to verify:
- All display modes render correctly
- Both object and JSON string inputs work
- Schema.org markup appears when requested
- Visibility toggles work as expected

---

## Browser Compatibility

- Modern browsers (Chrome, Firefox, Safari, Edge)
- IE11+ (with polyfills for `Object.assign` if needed)
- Mobile browsers (responsive design)

---

## Accessibility

- Semantic HTML structure
- ARIA labels where appropriate
- Keyboard navigation support
- Screen reader friendly
- Schema.org microdata for search engines

---

## Support

For questions or issues:
1. Check this documentation
2. Review example usage in `_BlogPostEditPartial.cshtml`
3. Inspect browser console for JavaScript errors
4. Verify CSS is loaded correctly

---

## License

Copyright (c) Moonrise Software, LLC. All rights reserved.
Licensed under the MIT License (https://opensource.org/licenses/MIT)
