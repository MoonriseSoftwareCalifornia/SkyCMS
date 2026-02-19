---
title: SkyCMS Layouts User Guide
description: Complete guide to creating and managing site-wide layouts in SkyCMS
keywords: layouts, site-design, templates, structure, responsive
audience: [developers, designers]
---

# SkyCMS Layouts - User Guide

## Table of Contents
- [Overview](#overview)
- [What is a Layout?](#what-is-a-layout)
- [Getting Started](#getting-started)
- [Working with Layouts](#working-with-layouts)
  - [Creating Your First Layout](#creating-your-first-layout)
  - [Viewing Layout List](#viewing-layout-list)
  - [Layout Versions](#layout-versions)
- [Editing Layouts](#editing-layouts)
  - [Code Editor](#code-editor)
  - [Visual Designer](#visual-designer)
  - [Editing Notes](#editing-notes)
- [Publishing Layouts](#publishing-layouts)
- [Community Layouts](#community-layouts)
- [Advanced Features](#advanced-features)
  - [Exporting Layouts](#exporting-layouts)
  - [Promoting Layout Versions](#promoting-layout-versions)
  - [Deleting Layouts](#deleting-layouts)
- [Layout Structure](#layout-structure)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

---

## Overview

Layouts in SkyCMS provide a consistent look, feel, and functionality across all pages on your website. A layout defines the common elements that appear on every page, such as headers, footers, navigation menus, and stylesheets.

---

## What is a Layout?

A **Layout** is a template that controls the overall structure and appearance of your website. It consists of three main sections:

1. **Head Content** - Content injected into the HTML `<head>` tag (stylesheets, meta tags, scripts)
2. **Header Content** - Content that appears at the top of every page (navigation, logo, etc.)
3. **Footer Content** - Content that appears at the bottom of every page (copyright, links, etc.)

Each page you create uses a layout, and the page content is inserted between the header and footer sections.

---

## Getting Started

### First Time Setup

When you first access the Layouts section, you'll be presented with two options:

#### Option 1: Choose a Pre-built Design (Recommended)
- **Best for:** Non-technical users or those who want a quick start
- Pre-built designs are ready to use with minimal configuration
- Click **"Choose pre-built"** to browse the Community Layouts catalog

#### Option 2: Build Your Own Design (Advanced)
- **Best for:** Web developers with HTML, CSS, and JavaScript experience
- Start with a blank layout and customize to your specific needs
- Click **"Choose custom"** to create a new blank layout

---

## Working with Layouts

### Creating Your First Layout

#### Using a Community Layout (Recommended)
1. Navigate to **Layouts** from the main menu
2. Click **"Import design"** or **"Choose pre-built"**
3. Browse the available community layouts
4. Click the preview image to see a full demonstration
5. Click **"Install"** to add the layout to your website
6. The layout and its associated page templates will be imported

#### Creating a Custom Layout
1. Navigate to **Layouts** from the main menu
2. Click **"New design"** or **"Choose custom"**
3. A new layout is created with a default name (e.g., "New Layout 1")
4. You'll be automatically redirected to the Code Editor to customize your layout

### Viewing Layout List

The Layout List displays all available layouts in your website with the following information:

- **Actions** - Quick action buttons:
  - **View Code** (eye icon) - View layout code in read-only mode
  - **Published Status** (checkmark/X) - Shows if this is the active published layout
  - **Promote** (up arrow) - Create a new version from this layout
- **Version** - Version number of the layout
- **Published** - Date and time when the layout was published (if published)
- **Modified** - Date and time when the layout was last modified
- **Name** - The friendly name of the layout

#### Available Actions from the Layout List
- **New design** - Create a new blank layout
- **Code** - Edit the code of the latest unpublished layout version
- **Design** - Edit with the visual designer tool
- **Notes** - Edit layout name and notes
- **Import design** - Browse and import community layouts
- **Page list** - Return to the page list view

---

### Layout Versions

SkyCMS uses a versioning system to manage layout changes:

- **Version Numbers** - Each layout has a version number that increments with each change
- **Published Version** - Only one layout version can be published (active) at a time
- **Draft Versions** - Unpublished versions allow you to make changes without affecting your live site
- **Version History** - All versions are retained, allowing you to review or restore previous versions

#### Working with Versions
- When you edit a published layout, a new draft version is automatically created
- The published layout remains active until you explicitly publish a new version
- You can create a new version from any existing layout using the **Promote** button

---

## Editing Layouts

### Code Editor

The Code Editor provides direct access to the HTML, CSS, and JavaScript that make up your layout.

#### Accessing the Code Editor
1. Navigate to **Layouts**
2. Click the **"Code"** button in the toolbar

#### Code Editor Interface
The editor features multiple tabs for editing different sections:

1. **Head Tab**
   - Contains content inserted into the HTML `<head>` tag
   - Add stylesheets, meta tags, external scripts, and other head elements
   - Example content:
     ```html
     <link rel="stylesheet" href="/pub/styles/main.css">
     <meta name="viewport" content="width=device-width, initial-scale=1.0">
     ```

2. **Header Content Tab**
   - Contains HTML for the page header section
   - Typically includes navigation, logo, and top-level page elements
   - Appears at the top of every page using this layout

3. **Footer Content Tab**
   - Contains HTML for the page footer section
   - Typically includes copyright information, links, and contact details
   - Appears at the bottom of every page using this layout

#### Code Editor Features
- **Syntax Highlighting** - Color-coded HTML, CSS, and JavaScript
- **Auto-save** - Changes are automatically saved after you stop typing (1.5 second delay)
- **Manual Save** - Press `Ctrl+S` (Windows) or `Cmd+S` (Mac) to save immediately
- **Validation** - HTML is automatically validated and cleaned when saved
- **Insert Tools** - Quick insert buttons for:
  - Page links
  - File links
  - Images
- **Read-only Mode** - View historical versions without making changes

#### Code Editor Best Practices
- Keep HTML semantically correct and well-formatted
- Use external stylesheets rather than inline styles when possible
- Test your changes by previewing before publishing
- Use meaningful comments to document complex sections

### Visual Designer

The Visual Designer provides a drag-and-drop interface for creating and editing layouts without writing code.

#### Accessing the Designer
1. Navigate to **Layouts**
2. Click the **"Design"** button in the toolbar

#### Designer Interface
The Designer uses GrapeJS, a powerful visual editor that allows you to:

- **Drag and Drop Components** - Add elements by dragging from the component panel
- **Style Elements** - Use the style panel to modify colors, fonts, spacing, and more
- **Edit Content** - Double-click elements to edit text and content
- **Responsive Design** - Preview and adjust for different screen sizes
- **Upload Images** - Directly upload and manage images from the designer

#### Designer Sections
The designer displays three distinct sections:

1. **Header Section**
   - Marked with `<!--CCMS--START--HEADER-->` and `<!--CCMS--END--HEADER-->`
   - Editable in the designer
   - Appears at the top of every page

2. **Content Section**
   - Displays a placeholder: "PAGE CONTENT GOES IN THIS BLOCK"
   - Cannot be edited in the layout designer (content is managed per-page)
   - Automatically inserted between header and footer

3. **Footer Section**
   - Marked with `<!--CCMS--START--FOOTER-->` and `<!--CCMS--END--FOOTER-->`
   - Editable in the designer
   - Appears at the bottom of every page

#### Designer Features
- **Component Library** - Pre-built elements (headers, buttons, forms, etc.)
- **Asset Manager** - Manage images and files
- **CSS Styling** - Visual style editor with live preview
- **Undo/Redo** - Easily reverse changes
- **Auto-save** - Changes are automatically saved

#### Important Designer Notes
- **No Nested Editable Regions** - The designer will prevent you from creating nested editable regions, which could cause conflicts
- **Header and Footer Only** - The designer is specifically for editing layout headers and footers, not page content

### Editing Notes

Layout notes help you document the purpose and details of each layout.

#### Accessing Notes Editor
1. Navigate to **Layouts**
2. Click the **"Notes"** button in the toolbar

#### Notes Editor Features
- **Layout Name** - Change the friendly name of your layout
- **Rich Text Editor** - Format your notes with a balloon-block style editor
- **HTML Support** - Notes support basic HTML formatting

#### When to Use Notes
- Document the purpose of the layout
- Note any special features or requirements
- Record configuration details or instructions
- Track changes and reasons for modifications

---

## Publishing Layouts

Publishing a layout makes it the active, default layout for your website. All pages will use the published layout unless specified otherwise.

### How to Publish a Layout

#### Method 1: From Layout List
1. Navigate to **Layouts**
2. Locate the layout version you want to publish
3. Click the **Promote** button (up arrow icon) if needed to create a new version
4. The system will prompt you to confirm publishing

#### Method 2: After Editing
1. After making changes in the Code Editor or Designer
2. The system automatically creates a new draft version
3. Navigate to **Layouts** > **Index**
4. Find your new version and publish it

### Publishing Behavior
- **Single Published Layout** - Only one layout version can be published at a time
- **Previous Version** - The previously published layout becomes unpublished but remains available
- **Immediate Effect** - Publishing takes effect immediately on all pages
- **Backup** - If a backup storage connection is configured, the database is automatically backed up after publishing

### Important Publishing Notes
- **Test Before Publishing** - Always preview your layout before publishing
- **Default Layout** - The published layout becomes the default for all new pages
- **Existing Pages** - Existing pages continue to use their assigned layout unless changed

---

## Community Layouts

Community Layouts are pre-built, open-source designs created by the SkyCMS community.

### Browsing Community Layouts

1. Navigate to **Layouts** > **Community Layouts**
2. Browse the catalog of available designs
3. Each layout displays:
   - **Preview Image** - Visual thumbnail of the design
   - **Name** - Design name
   - **License** - Open-source license type
   - **Description** - Details about the design and its features

### Installing a Community Layout

1. Click **"Install"** next to the desired layout
2. The layout and its associated page templates are automatically imported
3. You can now customize the layout to fit your needs
4. If this is your first layout, it will automatically be set as the default

### Features of Community Layouts
- **Ready to Use** - Pre-configured and tested designs
- **Page Templates** - Many include starter page templates
- **Documentation** - Each layout has its own documentation
- **Open Source** - Free to use and modify
- **Professional Quality** - Designed by experienced web developers

### Community Layout Best Practices
- Review the preview before installing
- Read the layout's documentation for specific features or requirements
- Customize the layout to match your brand colors and content
- Check the license to understand usage terms

---

## Advanced Features

### Exporting Layouts

You can export a layout as a standalone HTML file for use outside of SkyCMS.

#### How to Export
1. Navigate to the layout you want to export
2. The system will prompt you for export options
3. The layout is exported as a complete HTML file with:
   - All head content
   - Header HTML
   - A placeholder for page content
   - Footer HTML
   - Relative URLs converted to absolute URLs

#### Use Cases for Exporting
- Creating static HTML sites
- Sharing layouts with others
- Backing up layout designs
- Testing layouts in external tools

### Promoting Layout Versions

The Promote feature creates a new version based on an existing layout.

#### How to Promote
1. Navigate to **Layouts** > **Index**
2. Locate the layout version you want to promote
3. Click the **Promote** button (up arrow icon)
4. A new version is created with incremented version number
5. The new version becomes editable while the original remains unchanged

#### When to Promote
- Creating a new design based on an existing one
- Branching a design for testing
- Preserving a specific version before making major changes

### Deleting Layouts

You can delete layouts that are not currently published.

#### How to Delete
1. Navigate to **Layouts** > **Index**
2. Ensure the layout is not the published (default) layout
3. Select the delete option for the layout
4. Confirm the deletion

#### Important Notes
- **Cannot Delete Published Layout** - The currently published layout cannot be deleted
- **Associated Templates** - Page templates associated with the layout are also deleted
- **Permanent Action** - Deletion cannot be undone
- **Version Safety** - Only delete layouts you're certain you won't need

---

## Layout Structure

Understanding the technical structure of layouts helps you create and customize them effectively.

### HTML Structure

A complete rendered page has this structure:

```html
<!DOCTYPE html>
<html>
<head>
    <!-- Layout Head Content -->
    <link rel="stylesheet" href="/pub/styles/main.css">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <!-- Page-specific head content -->
</head>
<body>
    <!-- Layout Header Content -->
    <header>
        <nav>...</nav>
    </header>
    
    <!-- Page Content (editable per page) -->
    <main>
        <!-- This is where individual page content appears -->
    </main>
    
    <!-- Layout Footer Content -->
    <footer>
        <p>&copy; 2025 Your Company</p>
    </footer>
</body>
</html>
```

### Layout Properties

Each layout has the following properties:

| Property | Description | Required |
|----------|-------------|----------|
| **Id** | Unique identifier (GUID) | Yes |
| **Version** | Version number | Yes |
| **IsDefault** | Whether this is the published layout | Yes |
| **LayoutName** | Friendly name (max 128 characters) | Yes |
| **Notes** | Documentation and description | No |
| **Head** | HTML content for `<head>` tag | No |
| **BodyHtmlAttributes** | Attributes for `<body>` tag (max 256 characters) | No |
| **HtmlHeader** | Header HTML content | No |
| **FooterHtmlContent** | Footer HTML content | No |
| **CommunityLayoutId** | Reference to community layout (if imported) | No |
| **LastModified** | Last modification date/time | Auto |
| **Published** | Publication date/time | Auto |

### Special HTML Comments

SkyCMS uses HTML comments to mark layout sections:

```html
<!--CCMS--START--HEADER-->
<!-- Header content goes here -->
<!--CCMS--END--HEADER-->

<!--CCMS--START--FOOTER-->
<!-- Footer content goes here -->
<!--CCMS--END--FOOTER-->
```

These markers are used by the Designer and should not be removed when editing manually.

---

## Best Practices

### Design Best Practices

1. **Keep It Simple**
   - Start with a simple layout and add complexity as needed
   - Avoid overly complex navigation structures
   - Use consistent spacing and alignment

2. **Responsive Design**
   - Test your layout on multiple screen sizes
   - Use responsive CSS frameworks (Bootstrap, Tailwind, etc.)
   - Ensure navigation works on mobile devices

3. **Performance**
   - Minimize the number of external stylesheets and scripts
   - Optimize images and assets
   - Use CDNs for common libraries
   - Defer or async load non-critical JavaScript

4. **Accessibility**
   - Use semantic HTML elements
   - Provide alt text for images
   - Ensure sufficient color contrast
   - Test with screen readers

5. **SEO**
   - Include proper meta tags in the Head section
   - Use structured data markup where appropriate
   - Optimize page load times

### Development Best Practices

1. **Version Control**
   - Always create a new version before making significant changes
   - Use descriptive names and notes for each version
   - Test thoroughly before publishing

2. **Code Quality**
   - Write clean, well-formatted HTML
   - Comment complex sections
   - Validate HTML to avoid errors
   - Use external CSS files for maintainability

3. **Testing**
   - Preview layouts before publishing
   - Test on multiple browsers
   - Verify all links work correctly
   - Check console for JavaScript errors

4. **Documentation**
   - Keep layout notes up to date
   - Document any special requirements or dependencies
   - Note which external resources are required

### Content Best Practices

1. **Header Content**
   - Keep navigation clear and concise
   - Include your logo/branding
   - Ensure mobile-friendly menu
   - Consider sticky headers for long pages

2. **Footer Content**
   - Include copyright information
   - Add important links (Privacy Policy, Terms of Service)
   - Consider contact information or social media links
   - Keep footer height reasonable

3. **Head Content**
   - Include viewport meta tag for responsive design
   - Add favicon references
   - Include analytics scripts
   - Load critical CSS inline for performance

---

## Troubleshooting

### Common Issues and Solutions

#### Issue: Cannot Save Layout
**Symptoms:** Error message when saving, changes not persisting

**Solutions:**
- Check for HTML validation errors in the error log
- Ensure you're not trying to save a read-only/published version
- Verify no nested editable regions exist
- Check browser console for JavaScript errors

#### Issue: Layout Not Applying to Pages
**Symptoms:** Changes to layout don't appear on website pages

**Solutions:**
- Verify the layout is published (marked as default)
- Clear browser cache and refresh the page
- Check if the page is specifically using a different layout
- Ensure no page-level overrides are in place

#### Issue: Designer Cannot Save
**Symptoms:** "Cannot have nested editable regions" error

**Solutions:**
- Review your layout structure for nested editable components
- Use the Code Editor to inspect and fix the HTML
- Remove any editable regions inside other editable regions
- Simplify complex nested structures

#### Issue: Broken Layout After Import
**Symptoms:** Community layout appears broken or unstyled

**Solutions:**
- Check if external resources (CSS, fonts) are loading correctly
- Review browser console for 404 errors
- Verify asset paths are correct
- Check if required files were imported with the layout

#### Issue: Cannot Delete Layout
**Symptoms:** Delete option disabled or error when deleting

**Solutions:**
- Verify the layout is not the currently published layout
- Publish a different layout first, then delete the unwanted one
- Check if you have sufficient permissions (Administrators, Editors only)

#### Issue: Preview Doesn't Match Live Site
**Symptoms:** Preview shows different appearance than published site

**Solutions:**
- Ensure you're previewing the correct version
- Check for browser caching issues
- Verify the correct layout is published
- Review any page-specific overrides

### Getting Help

If you encounter issues not covered here:

1. **Check the Documentation** - Review related documentation in the Docs folder
2. **Review Logs** - Check application logs for detailed error messages
3. **Browser Console** - Check for JavaScript errors or warnings
4. **Community** - Visit the SkyCMS community forums or GitHub repository
5. **Contact Support** - Reach out to your system administrator or SkyCMS support

---

## Additional Resources

- **Templates Documentation** - Learn about page templates that work with layouts
- **File Manager** - Understanding asset management for layouts
- **CSS Frameworks** - Popular frameworks compatible with SkyCMS layouts
- **JavaScript Integration** - Adding interactivity to your layouts
- **GitHub Repository** - [https://github.com/CWALabs/SkyCMS](https://github.com/CWALabs/SkyCMS)

---

**Document Version:** 1.0  
**Last Updated:** October 2025  
**Applies to:** SkyCMS Editor

For questions or feedback about this documentation, please visit the SkyCMS GitHub repository or contact your system administrator.
