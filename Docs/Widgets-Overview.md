---
title: Widgets Overview
description: Reusable UI components and functionality for content creators and developers
keywords: widgets, components, UI, reusable, custom-widgets
audience: [developers, content-creators]
---

# Widgets in SkyCMS

Widgets are reusable, encapsulated UI components that provide specific functionality for content creators and developers. SkyCMS includes a variety of built-in widgets for common tasks and supports custom widget development.

---

## Table of Contents

- [What Are Widgets?](#what-are-widgets)
- [Available Widgets](#available-widgets)
- [For Content Creators](#for-content-creators)
- [For Developers](#for-developers)
- [Creating Custom Widgets](#creating-custom-widgets)
- [Widget Best Practices](#widget-best-practices)

---

## What Are Widgets?

Widgets are self-contained components that:

- **Encapsulate Functionality** - Each widget handles one specific task
- **Are Reusable** - Use the same widget multiple times on different pages
- **Provide Configuration** - Accept parameters to customize behavior
- **Manage State** - Handle their own data and updates
- **Work Independently** - Don't interfere with other widgets or page content

### Widget vs. Component

| Aspect | Widget | Component |
|--------|--------|-----------|
| **Scope** | Content/template-level UI | Application-level architecture |
| **Usage** | Inserted into pages by editors | Used by developers in views |
| **Configuration** | User-facing settings | Code-based configuration |
| **Examples** | Image widget, search widget | HomeControllerBase, identity service |

---

## Available Widgets

SkyCMS includes the following built-in widgets:

### 1. Image Widget

**Purpose:** Upload, store, and display images with management and optimization.

**Features:**
- Image upload and storage
- Automatic optimization
- Responsive sizing
- Image transformation
- Permissions/access control

**When to use:**
- Adding images to content
- Product photos
- Gallery displays
- Blog post illustrations

**Documentation:** [Image Widget Guide](./Widgets/Image-Widget.md)

---

### 2. Breadcrumbs Widget

**Purpose:** Display navigation breadcrumbs showing current page location in site hierarchy.

**Features:**
- Automatic hierarchy generation
- Customizable formatting
- SEO-friendly markup
- Mobile-friendly display

**When to use:**
- Navigation aids on product pages
- Category hierarchy display
- Multi-level site navigation
- Improving user orientation

**Documentation:** [Crumbs Widget Guide](./Widgets/Crumbs-Widget.md)

---

### 3. Forms Widget

**Purpose:** Create and handle form submissions securely.

**Features:**
- Form builder
- Anti-forgery protection
- Server-side validation
- Email notifications
- Submission storage

**When to use:**
- Contact forms
- Newsletter signup
- Surveys and polls
- Data collection

**Documentation:** [Forms Widget Guide](./Widgets/Forms-Widget.md)

---

### 4. Navigation Builder Widget

**Purpose:** Generate and display site navigation menus.

**Features:**
- Hierarchical menu creation
- Responsive mobile menus
- Active link highlighting
- Custom styling
- Dynamic menu generation

**When to use:**
- Main site navigation
- Footer menus
- Sidebar navigation
- Mobile navigation drawers

**Documentation:** [Nav Builder Widget Guide](./Widgets/Nav-Builder-Widget.md)

---

### 5. Search Widget

**Purpose:** Search site content with full-text search and filtering.

**Features:**
- Full-text search
- Search result highlighting
- Filtering options
- Search analytics
- Autocomplete suggestions

**When to use:**
- Site-wide search
- Content discovery
- Blog post searching
- Product searches

**Documentation:** [Search Widget Guide](./Widgets/Search-Widget.md)

---

### 6. Table of Contents Widget

**Purpose:** Automatically generate table of contents from page headings.

**Features:**
- Automatic heading detection
- Clickable jump links
- Customizable styling
- Responsive display
- Depth control

**When to use:**
- Long documentation pages
- Blog posts with sections
- Knowledge base articles
- Tutorial pages

**Documentation:** [ToC Widget Guide](./Widgets/ToC-Widget.md)

---

### 7. Crypto Widget

**Purpose:** Helper widget for AES encryption and decryption in forms.

**Features:**
- Client-side encryption
- Key management
- Secure data transmission
- Token generation

**When to use:**
- Protecting sensitive form data
- Secure communication
- Password reset flows
- Token-based features

**Documentation:** [Crypto Widget Guide](./Widgets/Crypto-Widget.md)

---

### 8. Custom Widgets

Beyond built-in widgets, you can create custom widgets for specialized needs.

**Documentation:** [Creating Custom Widgets](#creating-custom-widgets)

---

## For Content Creators

### Inserting Widgets

Widgets are typically inserted into pages through the Live Editor:

1. **Open Page in Editor**
   - Navigate to the page you want to edit
   - Click "Edit in Live Editor"

2. **Insert Widget**
   - Click the "+" button or widget menu
   - Choose the desired widget from the list
   - Click to insert

3. **Configure Widget**
   - Each widget has configuration options
   - Set values according to your needs
   - Preview changes in real-time

4. **Save**
   - Click "Save" to save your changes
   - Publish when ready

### Widget Configuration

Each widget has different configuration options:

**Example: Image Widget**
```
- Image: [Choose image file]
- Alt Text: "Description of image"
- Size: Small / Medium / Large
- Alignment: Left / Center / Right
- Link: [Optional link URL]
```

**Example: Search Widget**
```
- Search Type: Global / Current Section
- Results Per Page: 10 / 25 / 50
- Show Filters: Yes / No
- Placeholder Text: "Search..."
```

Refer to individual widget documentation for complete configuration options.

### Best Practices

- **Use Appropriate Widgets** - Choose widgets designed for your purpose
- **Configure Completely** - Fill in all required fields
- **Test Before Publishing** - Preview widgets before publishing
- **Use Descriptive Text** - Provide clear alt text and labels
- **Keep It Simple** - Don't overuse widgets; focus on content

---

## For Developers

### Widget Architecture

Widgets in SkyCMS follow a consistent architecture:

```
Widget
├── View (.cshtml)           - HTML markup
├── View Model               - Data binding
├── Configuration Options    - Customizable parameters
└── Supporting Scripts/Styles - JavaScript and CSS
```

### Widget Components

Each widget includes:

1. **Markup** - HTML structure
2. **Styling** - CSS for appearance
3. **Behavior** - JavaScript for interactivity
4. **Configuration** - Parameters for customization
5. **Documentation** - Usage guides

### Available Widgets (Developer View)

| Widget | Location | Complexity | Key Files |
|--------|----------|-----------|-----------|
| Image | `Widgets/Image` | Medium | Image-Widget.md |
| Crumbs | `Widgets/Crumbs` | Low | Crumbs-Widget.md |
| Forms | `Widgets/Forms` | High | Forms-Widget.md |
| Navigation | `Widgets/Navigation` | Medium | Nav-Builder-Widget.md |
| Search | `Widgets/Search` | High | Search-Widget.md |
| ToC | `Widgets/ToC` | Low | ToC-Widget.md |
| Crypto | `Widgets/Crypto` | Low | Crypto-Widget.md |

### Widget Documentation for Developers

Each widget has comprehensive developer documentation including:

- **Architecture Overview** - How the widget is structured
- **API Reference** - Available methods and properties
- **Events** - Available event hooks
- **Styling** - CSS classes and customization points
- **Integration** - How to use in your own code
- **Examples** - Code samples and patterns

See [Widgets Directory](./Widgets/) for complete developer documentation.

---

## Creating Custom Widgets

### When to Create a Custom Widget

Create a custom widget when you need:

- Specialized functionality not covered by built-in widgets
- Reusable component across multiple pages
- Encapsulated behavior and styling
- Configuration options for different use cases
- Third-party service integration

### Custom Widget Development

To create a custom widget:

1. **Plan Your Widget**
   - Define functionality and purpose
   - Identify configuration options
   - Design user experience

2. **Create Widget Structure**
   - Create `.cshtml` view file
   - Create view model/configuration class
   - Add supporting JavaScript/CSS if needed

3. **Implement Functionality**
   - Write markup
   - Add styling
   - Implement behavior

4. **Register Widget**
   - Register in dependency injection
   - Make discoverable in editor
   - Add configuration UI

5. **Document**
   - Write user documentation
   - Write developer documentation
   - Include examples

6. **Test**
   - Test in live editor
   - Test on mobile devices
   - Test different configurations

### Custom Widget Example

```csharp
// Widget View Model
public class MyWidgetConfiguration
{
    public string Title { get; set; }
    public string Content { get; set; }
    public bool ShowBorder { get; set; }
}
```

```html
<!-- Widget View -->
<div class="my-widget @(Model.ShowBorder ? "bordered" : "")">
    <h3>@Model.Title</h3>
    <p>@Model.Content</p>
</div>
```

### Widget Best Practices

1. **Self-Contained** - Widget should work independently
2. **Configurable** - Provide options for customization
3. **Accessible** - Follow WCAG accessibility guidelines
4. **Responsive** - Work on all device sizes
5. **Documented** - Clear documentation for users and developers
6. **Tested** - Thoroughly test before publishing
7. **Performant** - Optimize for speed and efficiency
8. **Secure** - Handle user input safely

---

## Widget Best Practices

### For Content Creators

- Use widgets for their intended purpose
- Configure all required fields
- Test widgets before publishing
- Provide descriptive alt text and labels
- Don't overcomplicate pages with too many widgets
- Use consistent styling across similar widgets

### For Developers

- Keep widgets focused on single responsibility
- Provide sensible configuration defaults
- Include proper error handling
- Make widgets mobile-friendly
- Document thoroughly
- Consider accessibility from the start
- Test with various data sizes and types

---

## See Also

- **[Widgets Directory](./Widgets/)** - Complete widget documentation and API reference
- **[Image Widget Deep Dive](./Developers/ImageWidget.md)** - Detailed widget implementation example
- **[Developer Overview](./Developers/README.md)** - Developer documentation hub
- **[Content Editors: Live Editor](./Editors/LiveEditor/README.md)** - How editors use widgets
- **[LEARNING_PATHS: Developer](./LEARNING_PATHS.md#developer)** - Widget development learning path
- **[LEARNING_PATHS: Content Editor](./LEARNING_PATHS.md#content-editor-non-technical)** - Using widgets as content creator
- **[CONTRIBUTING.md](./CONTRIBUTING.md)** - Documentation standards for widget documentation
- **[Main Documentation Hub](./index.md)** - Browse all documentation

---

## Related Documentation (To Be Created)

- Widget security and permissions
- Widget performance optimization
- Third-party widget integration
- Advanced widget configuration
- Widget testing strategies
- Custom widget templates and scaffolding

---

**Last Updated:** December 17, 2025  
**Owner:** @toiyabe
