---
title: Phase 3 - Creating Templates
description: Page type blueprints, reusable components, editable regions, and File Manager integration
keywords: templates, page-types, components, editable-regions, reusability, phase-3
audience: [developers]
---

# Phase 3: Creating Templates Guide

**Timeline:** 8-16 hours  
**Typical Effort:** 4-8 templates for varied content types  
**Output:** Complete template library with editable regions for content editors

## Overview

In Phase 2, you created your Site Designs (Layouts) — the structural containers for all pages. In Phase 3, you create Templates that sit inside those Site Designs and define content areas for specific page types.

A **Template** is:
- A page structure that defines where content goes (using editable regions)
- A bridge between your Site Design and individual pages
- A reusable blueprint for similar pages (blog posts, service pages, product pages, etc.)
- A way to empower content editors to create consistent pages without touching code

By the end of Phase 3, you'll have created multiple templates that content editors can use to create pages in Phase 5.

---

## The Template Hierarchy Recap

Before diving in, remember how templates fit into the architecture:

```
SITE DESIGN (Layout)
  └─ TEMPLATE 1
       └─ Page 1 (created from Template 1)
       └─ Page 2 (created from Template 1)
       └─ Page 3 (created from Template 1)
  
  └─ TEMPLATE 2
       └─ Page 4 (created from Template 2)
       └─ Page 5 (created from Template 2)
  
  └─ TEMPLATE 3
       └─ Page 6 (created from Template 3)
```

Each page created from a template is an **independent instance** with its own content. When you edit a template, it affects the structure of ALL pages using that template.

---

## Philosophy: Keep Templates Simple

### Templates Are Primarily for Page Structure

**The most important thing to understand:** Templates are designed to organize **page structure** — the arrangement and layout of content areas on individual pages.

**What Templates Should Focus On:**
```html
<!-- Good: Structural focus -->
<article class="blog-post">
  <!-- Header section -->
  <header data-ccms-ceid="post-header">
    <h1>Post Title</h1>
    <p class="meta">Author and date</p>
  </header>
  
  <!-- Main content -->
  <div data-ccms-ceid="post-content">
    <p>Article content...</p>
  </div>
  
  <!-- Author bio -->
  <aside data-ccms-ceid="author-bio">
    <p>About the author...</p>
  </aside>
</article>
```

**What Templates Should Provide:**
- ✅ Logical content sections (header, body, sidebar, footer-like areas)
- ✅ Editable regions for content creators
- ✅ Basic HTML structure and semantic markup
- ✅ Simple, clear organization

**What Site Designs (Layouts) Handle:**
- Site-wide elements (main header, navigation, footer)
- Global CSS and JavaScript
- Overall page wrapper and structure
- Common dependencies (Bootstrap, jQuery, etc.)

### Adding Template-Specific CSS and JavaScript

While structure is the primary focus, you **can** add template-specific CSS and JavaScript:

```html
<!-- Template-specific styles -->
<style>
  .blog-post {
    max-width: 800px;
    margin: 0 auto;
    line-height: 1.8;
  }
  
  .blog-post .meta {
    color: #666;
    font-size: 0.9rem;
  }
</style>

<!-- Template-specific script -->
<script>
  // Example: Auto-generate table of contents for this template
  document.addEventListener('DOMContentLoaded', function() {
    // Template-specific functionality
  });
</script>
```

**When to add template-specific CSS/JS:**
- ✅ Styling unique to this template type (not used elsewhere)
- ✅ Functionality specific to this content type
- ✅ Small, focused enhancements

**When NOT to add template-specific CSS/JS:**
- ❌ Global styles (those belong in Site Design)
- ❌ Large JavaScript libraries (add to Site Design instead)
- ❌ Styles shared across multiple templates (consolidate in Site Design)

### Simplicity Is Critical for Template Updates

**Remember the template update risk from earlier in this guide?** The complexity of your template directly impacts the risk when updating pages.

```
SIMPLE TEMPLATE = LOW UPDATE RISK
COMPLEX TEMPLATE = HIGH UPDATE RISK
```

**Simple Template Example:**
```html
<!-- Low risk: Clear structure, minimal complexity -->
<article>
  <div data-ccms-ceid="title">
    <h1>Title</h1>
  </div>
  <div data-ccms-ceid="content">
    <p>Content here</p>
  </div>
</article>
```

**Complex Template Example:**
```html
<!-- High risk: Nested structure, multiple dependencies -->
<div class="wrapper">
  <div class="container">
    <div class="row">
      <div class="col-8">
        <section class="primary">
          <div class="header-group">
            <div data-ccms-ceid="title">
              <h1>Title</h1>
            </div>
          </div>
          <div class="content-wrapper">
            <div class="content-inner">
              <div data-ccms-ceid="content">
                <p>Content</p>
              </div>
            </div>
          </div>
        </section>
      </div>
      <div class="col-4">
        <aside class="sidebar">
          <div data-ccms-ceid="sidebar">...</div>
        </aside>
      </div>
    </div>
  </div>
</div>
```

**Why complexity increases risk:**

1. **More places for errors** — Deep nesting creates more points of failure
2. **Harder to update safely** — Structural changes affect more elements
3. **Difficult to debug** — Problems harder to isolate
4. **Content preservation issues** — Complex structures make ID tracking harder
5. **Performance impact** — More DOM elements = slower rendering

### The Simplicity Rule

> **If you anticipate updating a template in the future, keep it as simple as possible.**

**Good Simplicity Practices:**

```html
<!-- ✅ SIMPLE: Flat structure -->
<article>
  <header data-ccms-ceid="header">...</header>
  <main data-ccms-ceid="content">...</main>
  <aside data-ccms-ceid="sidebar">...</aside>
</article>

<!-- ❌ COMPLEX: Unnecessary nesting -->
<div class="article-wrapper">
  <div class="article-container">
    <div class="article-inner">
      <article>
        <div class="header-wrapper">
          <header data-ccms-ceid="header">...</header>
        </div>
        <div class="content-wrapper">
          <main data-ccms-ceid="content">...</main>
        </div>
      </article>
    </div>
  </div>
</div>
```

**Design for Updates:**
- Use minimal wrapper divs (only what's needed for styling)
- Keep editable regions at consistent depth
- Avoid complex grid systems inside templates (use CSS Grid/Flexbox simply)
- Don't over-engineer structure "just in case"

### Apply DRY and Best Practices from Site Designs

**Many tips from Phase 2 (Creating Layouts) also apply to templates:**

#### Use CSS Variables
```html
<style>
  /* Define template-specific variables */
  .blog-post {
    --post-max-width: 800px;
    --post-line-height: 1.8;
  }
  
  .blog-post article {
    max-width: var(--post-max-width);
    line-height: var(--post-line-height);
  }
</style>
```

#### Avoid Inline Styles
```html
<!-- ❌ Bad: Inline styles -->
<div data-ccms-ceid="content" style="padding: 20px; background: #f0f0f0;">
  Content
</div>

<!-- ✅ Good: Use classes -->
<style>
  .content-area { padding: 20px; background: #f0f0f0; }
</style>
<div data-ccms-ceid="content" class="content-area">
  Content
</div>
```

#### Keep Selectors Simple
```css
/* ❌ Complex selector */
.template-wrapper .container .content-area .post .article .body p {
  font-size: 1rem;
}

/* ✅ Simple selector */
.post-body p {
  font-size: 1rem;
}
```

#### Semantic HTML
```html
<!-- ✅ Good: Semantic elements -->
<article>
  <header data-ccms-ceid="post-header">...</header>
  <main data-ccms-ceid="post-content">...</main>
  <footer data-ccms-ceid="post-footer">...</footer>
</article>

<!-- ❌ Avoid: Generic divs -->
<div class="article">
  <div data-ccms-ceid="post-header">...</div>
  <div data-ccms-ceid="post-content">...</div>
  <div data-ccms-ceid="post-footer">...</div>
</div>
```

#### Performance Tips
- ✅ Optimize images in template examples
- ✅ Minimize template-specific JavaScript
- ✅ Use lazy loading for template images
- ✅ Keep CSS concise and focused

**For more comprehensive best practices, see:**
- [Phase 2: Creating Layouts - Tips and Best Practices](./02-Creating-Layouts.md#tips-and-best-practices-for-site-designs)
- Includes: Performance optimization, accessibility, security, development workflow

### Template Complexity vs. Update Risk: Decision Matrix

Use this guide when deciding template complexity:

| Scenario | Recommended Approach | Complexity Level |
|----------|---------------------|------------------|
| **Blog posts, articles** | Simple 2-3 editable regions, flat structure | **Low** ✅ |
| **Product pages** | Moderate structure with 4-6 regions | **Medium** ⚠️ |
| **Landing pages** | Multiple sections but clear structure | **Medium** ⚠️ |
| **Complex data pages** | Consider multiple simple templates vs. one complex | **High** ❌ |
| **One-off pages** | Use blank page or minimal template | **Low** ✅ |

**Rule of Thumb:**
- If you can't easily explain the template structure in 2-3 sentences → **It's too complex**
- If you have more than 6-8 editable regions → **Consider splitting into multiple templates**
- If nested more than 3-4 levels deep → **Simplify the structure**

### The Bottom Line

> **Templates organize page structure. Keep them simple. The simpler the template, the safer and easier updates become, and the more maintainable your site remains over time.**

This simplicity principle is **critical** for long-term success with SkyCMS. Complex templates may seem powerful initially, but they create maintenance nightmares later.

---

## Understanding Editable Regions

The most critical concept in template creation is **editable regions** — areas where content editors can add, modify, or delete content.

### What Makes a Region Editable?

In SkyCMS, you mark regions as editable by adding either:

#### Method 1: `contenteditable` Attribute

```html
<div contenteditable="true">
  Content editors can click here and edit this text directly
</div>
```

**How it works:**
- Enables inline editing
- Editors click on the region and type
- Works for simple text and formatting
- Simple but basic

#### Method 2: `data-ccms-ceid` Attribute (Recommended)

```html
<div data-ccms-ceid="unique-region-id">
  This region has a unique identifier
</div>
```

**How it works:**
- Provides a unique ID for the region
- Enables rich editing with the Live Editor
- Required for template update tracking (explained later)
- More powerful — allows multiple editing modes

### Creating CKEditor Blocks: Rich Text Editable Regions

When you need editors to create rich content (formatted text, lists, links, images), create **CKEditor blocks** using editable regions:

```html
<!-- EDITABLE REGION FOR RICH TEXT CONTENT -->
<div data-ccms-ceid="main-content">
  <h2>Page Title</h2>
  <p>Start typing your content here. This region supports:</p>
  <ul>
    <li>Formatted text (bold, italic, underline)</li>
    <li>Headings and paragraphs</li>
    <li>Lists and tables</li>
    <li>Links and images</li>
    <li>And more CKEditor features</li>
  </ul>
</div>
```

When an editor opens a page using this template:
- The region with `data-ccms-ceid="main-content"` becomes a full-featured rich text editor
- They can format text, add images, create lists, embed content
- The Live Editor toolbar provides all CKEditor tools
- Content is saved automatically

### Multiple Editable Regions in One Template

Most templates need multiple editable regions for different content areas:

```html
<!DOCTYPE html>
<html>
<head>
  <!-- Template head (not editable) -->
</head>
<body>
  <!-- REGION 1: Page headline -->
  <header class="page-header">
    <div data-ccms-ceid="page-headline">
      <h1>Your Page Title Here</h1>
      <p class="subtitle">Subtitle or brief description</p>
    </div>
  </header>

  <!-- REGION 2: Main content -->
  <main class="content-area">
    <div data-ccms-ceid="main-content">
      <p>Main page content goes here. Editors can add formatted text, images, links, and more.</p>
    </div>
  </main>

  <!-- REGION 3: Sidebar content -->
  <aside class="sidebar">
    <div data-ccms-ceid="sidebar-content">
      <h3>Related Information</h3>
      <p>Optional sidebar content</p>
    </div>
  </aside>

  <!-- REGION 4: Call-to-action -->
  <section class="cta-section">
    <div data-ccms-ceid="call-to-action">
      <h2>Take Action</h2>
      <p>Add your call-to-action here</p>
      <button>Click me</button>
    </div>
  </section>
</body>
</html>
```

### Critical Rules for Editable Regions

**Rule 1: Unique IDs**
```html
<!-- Good: Each region has unique ID -->
<div data-ccms-ceid="headline">...</div>
<div data-ccms-ceid="main-content">...</div>
<div data-ccms-ceid="sidebar">...</div>

<!-- Bad: Duplicate IDs break tracking -->
<div data-ccms-ceid="content">...</div>
<div data-ccms-ceid="content">...</div>
```

**Rule 2: No Nesting**
```html
<!-- Good: Regions are separate -->
<div data-ccms-ceid="header">...</div>
<div data-ccms-ceid="content">...</div>

<!-- Bad: Nested regions don't work -->
<div data-ccms-ceid="outer">
  <div data-ccms-ceid="inner">...</div>
</div>
```

**Rule 3: Use Supported Elements**
```html
<!-- Good: Use common container elements -->
<div data-ccms-ceid="main">...</div>
<section data-ccms-ceid="featured">...</section>
<article data-ccms-ceid="post">...</article>
<h1 data-ccms-ceid="heading">...</h1>

<!-- Avoid: Some elements work but are not recommended -->
<span data-ccms-ceid="bad">...</span>
<p data-ccms-ceid="poor">...</p>
```

**Rule 4: Meaningful IDs**
```html
<!-- Good: Descriptive IDs -->
<div data-ccms-ceid="hero-section">...</div>
<div data-ccms-ceid="featured-products">...</div>
<div data-ccms-ceid="testimonials">...</div>

<!-- Works but less clear -->
<div data-ccms-ceid="region-1">...</div>
<div data-ccms-ceid="region-2">...</div>
```

---

## Common Template Patterns

Rather than building from scratch, use proven patterns. Here are the most common template structures:

### Pattern 1: Simple Content Page

**When to use:** Blog posts, news articles, simple informational pages

**Structure:**
```
Hero/Header Image
Page Title
Author & Date
Main Content (rich text)
```

**Template Code:**
```html
<!-- Hero Section -->
<section class="hero-section">
  <div data-ccms-ceid="hero-image" style="height: 300px; background-size: cover;">
    <!-- Hero background image -->
  </div>
</section>

<!-- Main Content -->
<article class="article-container">
  <div class="article-header">
    <div data-ccms-ceid="article-title">
      <h1>Article Title</h1>
      <p class="meta">By Author | Published Date</p>
    </div>
  </div>

  <div data-ccms-ceid="article-content">
    <p>Article content starts here. This section supports rich text formatting, images, and more.</p>
  </div>
</article>
```

### Pattern 2: Hero + Content

**When to use:** Landing pages, campaign pages, service pages

**Structure:**
```
Hero Section (large image + headline + CTA)
Content Section
Call-to-Action Section
```

**Template Code:**
```html
<!-- HERO SECTION -->
<section class="hero" style="background: linear-gradient(rgba(0,0,0,0.5), rgba(0,0,0,0.5)), url(hero.jpg); height: 400px; display: flex; align-items: center;">
  <div class="container">
    <div data-ccms-ceid="hero-content" style="color: white;">
      <h1>Your Headline</h1>
      <p class="lead">Your subheading or description</p>
      <button class="btn btn-primary btn-lg">Call to Action</button>
    </div>
  </div>
</section>

<!-- CONTENT SECTION -->
<section class="content-section" style="padding: 3rem 0;">
  <div class="container">
    <div data-ccms-ceid="main-content">
      <h2>Section Title</h2>
      <p>Main content area with rich text editing support.</p>
    </div>
  </div>
</section>

<!-- CTA SECTION -->
<section class="cta-section" style="background-color: #f0f0f0; padding: 3rem 0;">
  <div class="container">
    <div data-ccms-ceid="cta-block" style="text-align: center;">
      <h2>Ready to Get Started?</h2>
      <p>Description of what happens next</p>
      <button class="btn btn-primary btn-lg">Get Started</button>
    </div>
  </div>
</section>
```

### Pattern 3: Two-Column (Content + Sidebar)

**When to use:** Blog sidebars, related content, reference material

**Structure:**
```
Page Header
Two Columns:
  - Main Content (wider)
  - Sidebar (narrow)
```

**Template Code:**
```html
<div class="page-header">
  <div data-ccms-ceid="page-title">
    <h1>Page Title</h1>
  </div>
</div>

<div class="container" style="display: grid; grid-template-columns: 2fr 1fr; gap: 2rem;">
  <!-- MAIN CONTENT -->
  <main>
    <div data-ccms-ceid="main-content">
      <p>Main content area</p>
    </div>
  </main>

  <!-- SIDEBAR -->
  <aside>
    <div data-ccms-ceid="sidebar">
      <h3>Sidebar Title</h3>
      <p>Sidebar content</p>
    </div>
  </aside>
</div>
```

### Pattern 4: Three-Column (Header + 3 Features)

**When to use:** Feature showcase, service highlights, product comparison

**Structure:**
```
Header/Description
Three Equal Columns:
  - Feature 1 (icon + text)
  - Feature 2 (icon + text)
  - Feature 3 (icon + text)
```

**Template Code:**
```html
<!-- HEADER -->
<section class="features-header" style="text-align: center; padding: 2rem;">
  <div data-ccms-ceid="features-header">
    <h2>Our Features</h2>
    <p>Describe your three main features</p>
  </div>
</section>

<!-- FEATURES GRID -->
<section class="features-grid" style="padding: 2rem;">
  <div class="container">
    <div style="display: grid; grid-template-columns: repeat(3, 1fr); gap: 2rem;">
      
      <!-- FEATURE 1 -->
      <div class="feature-card">
        <div data-ccms-ceid="feature-1">
          <div style="font-size: 3rem; text-align: center;">🚀</div>
          <h3>Feature One</h3>
          <p>Description of feature one</p>
        </div>
      </div>

      <!-- FEATURE 2 -->
      <div class="feature-card">
        <div data-ccms-ceid="feature-2">
          <div style="font-size: 3rem; text-align: center;">💡</div>
          <h3>Feature Two</h3>
          <p>Description of feature two</p>
        </div>
      </div>

      <!-- FEATURE 3 -->
      <div class="feature-card">
        <div data-ccms-ceid="feature-3">
          <div style="font-size: 3rem; text-align: center;">⭐</div>
          <h3>Feature Three</h3>
          <p>Description of feature three</p>
        </div>
      </div>

    </div>
  </div>
</section>
```

### Pattern 5: Product/Service Card

**When to use:** E-commerce products, service offerings, portfolio items

**Structure:**
```
Product Image
Product Title
Description
Price/Details
Call-to-Action Button
```

**Template Code:**
```html
<section class="product-container" style="max-width: 400px; border: 1px solid #ddd; border-radius: 8px; overflow: hidden;">
  
  <!-- IMAGE -->
  <div class="product-image" style="height: 300px; background-size: cover;">
    <img src="placeholder.jpg" alt="Product">
  </div>

  <!-- CONTENT -->
  <div class="product-content" style="padding: 1.5rem;">
    <div data-ccms-ceid="product-title">
      <h3>Product Name</h3>
    </div>

    <div data-ccms-ceid="product-description">
      <p>Product description and details</p>
    </div>

    <div data-ccms-ceid="product-price" style="font-size: 1.5rem; font-weight: bold; color: #0066cc; margin: 1rem 0;">
      $99.99
    </div>

    <div data-ccms-ceid="product-cta">
      <button class="btn btn-primary btn-block">Add to Cart</button>
    </div>
  </div>

</section>
```

---

## Step-by-Step: Creating Your First Template

### Step 1: Access Template Manager

In SkyCMS Admin:
1. Navigate to **Content Management** → **Templates**
2. Click **Create New Template** button
3. Choose your editor mode

### Step 2: Choose Your Editor

#### Code Editor (Recommended for Developers)

```
┌─────────────────────────────────────────┐
│ SkyCMS Template Editor - Code Mode      │
├─────────────────────────────────────────┤
│                                         │
│  <div data-ccms-ceid="headline">      │
│    <h1>Title</h1>                      │
│  </div>                                │
│                                         │
│                                         │
├─────────────────────────────────────────┤
│ Preview | Save | Validate              │
└─────────────────────────────────────────┘
```

**Advantages:**
- Full control over HTML
- Easy to paste existing designs
- See exact code structure
- All template features available

**Disadvantages:**
- Requires HTML knowledge
- No drag-and-drop

#### Design Editor (Recommended for Designers)

```
┌─────────────────────────────────────────┐
│ SkyCMS Template Editor - Design Mode    │
├──────────┬──────────────────────────────┤
│ Blocks  │ [Page Layout Canvas]          │
│ Widgets │                               │
│ Styling │  [Drag components here]       │
│         │                               │
├──────────┴──────────────────────────────┤
│ Preview | Save | Code View              │
└─────────────────────────────────────────┘
```

**Advantages:**
- Visual drag-and-drop interface
- Preview as you build
- Easier for non-developers
- Pre-built component library

**Disadvantages:**
- May be slower for complex designs
- Less control over markup
- Fewer advanced features

### Step 3: Select Your Site Design

When creating a template, choose which Site Design it uses:
- Main Site Design → Most templates
- Landing Site Design → Landing pages
- Your custom Site Design → Specialized templates

### Step 4: Build Your Template

Using one of the patterns above, add your editable regions:

**Full Blog Post Template Example:**

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <style>
        .blog-article {
            max-width: 800px;
            margin: 0 auto;
            padding: 2rem;
        }
        
        .article-header {
            margin-bottom: 2rem;
            border-bottom: 2px solid #ddd;
            padding-bottom: 1rem;
        }
        
        .article-meta {
            color: #666;
            font-size: 0.9rem;
            margin-top: 0.5rem;
        }
        
        .article-content {
            line-height: 1.8;
            font-size: 1.1rem;
        }
        
        .article-content h2 {
            margin-top: 2rem;
            margin-bottom: 1rem;
        }
    </style>
</head>
<body>
    <article class="blog-article">
        <!-- EDITABLE REGION 1: Article Header -->
        <header class="article-header">
            <div data-ccms-ceid="article-header">
                <h1>Your Article Title</h1>
                <p class="article-meta">By Author Name | Published: Date | Category: Uncategorized</p>
            </div>
        </header>

        <!-- EDITABLE REGION 2: Article Content -->
        <div class="article-content" data-ccms-ceid="article-body">
            <p>Start your article content here. You can add:</p>
            <ul>
                <li>Formatted text with bold, italic, and underline</li>
                <li>Headings and subheadings</li>
                <li>Images and media</li>
                <li>Lists and tables</li>
                <li>Links and more</li>
            </ul>
            <p>Use the CKEditor toolbar to format your content.</p>
        </div>

        <!-- EDITABLE REGION 3: Author Bio (Optional) -->
        <section class="author-bio" style="margin-top: 3rem; padding-top: 2rem; border-top: 1px solid #ddd;">
            <div data-ccms-ceid="author-info">
                <h3>About the Author</h3>
                <p>Author biography goes here.</p>
            </div>
        </section>
    </article>
</body>
</html>
```

### Step 5: Name and Describe Your Template

Choose a clear, descriptive name:

**Good Names:**
- "Blog Post" (content type clearly identified)
- "Service Page" (indicates purpose)
- "Product Showcase" (indicates use case)
- "Team Member Bio" (specific content type)

**Poor Names:**
- "Template 1" (too generic)
- "Page" (not descriptive)
- "Content" (vague)

**Add a Description:**
```
Blog Post Template
For publishing blog articles, news posts, and similar long-form content.
Features: Hero image support, metadata (author/date), rich text content area.
Use when: Creating new blog or news articles.
```

### Step 6: Test Your Template

Before saving:
1. Use **Preview** to see how it looks
2. Check for:
   - Proper layout and spacing
   - Editable regions are marked correctly
   - No syntax errors in HTML
   - Responsive design works on mobile
3. Create a test page to verify editors can edit regions

### Step 7: Save Your Template

Click **Save Template**. The system will:
1. Validate HTML syntax
2. Verify editable regions
3. Store the template
4. Make it available for creating pages

---

## Creating Editable Block Collections

For complex pages with multiple distinct sections, organize editable regions into logical blocks:

### Example: Product Page with Multiple Blocks

```html
<!-- BLOCK 1: PRODUCT OVERVIEW -->
<section class="product-overview" style="padding: 2rem 0; border-bottom: 1px solid #ddd;">
  <div data-ccms-ceid="product-overview">
    <h1>Product Name</h1>
    <p class="price">$99.99</p>
    <p>Brief product description</p>
  </div>
</section>

<!-- BLOCK 2: DETAILED FEATURES -->
<section class="product-features" style="padding: 2rem 0; background: #f9f9f9;">
  <div data-ccms-ceid="product-features">
    <h2>Key Features</h2>
    <ul>
      <li>Feature 1</li>
      <li>Feature 2</li>
      <li>Feature 3</li>
    </ul>
  </div>
</section>

<!-- BLOCK 3: SPECIFICATIONS -->
<section class="product-specs" style="padding: 2rem 0; border-bottom: 1px solid #ddd;">
  <div data-ccms-ceid="product-specs">
    <h2>Specifications</h2>
    <table>
      <tr><td>Material</td><td>Cotton</td></tr>
      <tr><td>Size</td><td>Standard</td></tr>
    </table>
  </div>
</section>

<!-- BLOCK 4: CUSTOMER REVIEWS -->
<section class="product-reviews" style="padding: 2rem 0;">
  <div data-ccms-ceid="product-reviews">
    <h2>Customer Reviews</h2>
    <p>⭐⭐⭐⭐⭐ "Great product!" - Customer Name</p>
  </div>
</section>

<!-- BLOCK 5: CALL-TO-ACTION -->
<section class="product-cta" style="padding: 2rem; background: #0066cc; color: white; text-align: center;">
  <div data-ccms-ceid="product-cta">
    <h2>Ready to Purchase?</h2>
    <button style="background: white; color: #0066cc; padding: 10px 30px; border: none; border-radius: 5px; font-weight: bold;">Buy Now</button>
  </div>
</section>
```

---

## Template Naming Conventions

Establish a consistent naming system to keep templates organized:

### Option 1: Content Type + Purpose

```
Blog Post
Service Page
Product Page
Team Member Profile
Testimonial
FAQ Page
Contact Form
About Company
```

### Option 2: Hierarchical Names

```
Landing - Hero
Landing - Simple
Article - Blog
Article - News
Product - Basic
Product - Featured
Service - Full
Service - Brief
```

### Option 3: Business-Specific Names

E-commerce site:
```
Product - Standard
Product - Featured
Category - Grid
Category - List
Review - Customer
Brand - Profile
```

Service company:
```
Service - Main
Service - Add-on
Case Study - Full
Case Study - Summary
Team - Full Bio
Team - Brief Bio
Testimonial - Video
Testimonial - Text
```

**Best Practice:** Choose ONE convention and stick with it throughout your project.

---

## Template Update Mechanics and Risk Management

### ⚠️ CRITICAL WARNING: Template Updates Can Be Risky

Before we explain how template updates work, understand this: **Modifying a template after pages are created from it can have unintended consequences that are permanent and difficult to reverse.**

### How Template Updates Work

When you modify a template's structure, SkyCMS can update all pages using that template:

```
Original Template v1 (Pages A, B, C created from it)
         ↓
Edit Template → Template v2
         ↓
Update All Pages Using Template
         ↓
Pages A, B, C now use new structure
```

### What Gets Updated

When you update pages:
1. **Editable region IDs that still exist** → Content is preserved
2. **Editable region IDs that were removed** → Content is LOST
3. **New editable regions added** → Appear empty on updated pages
4. **Template structure** → All pages get new layout

### What Can Go Wrong

#### Risk 1: Content Loss from Removed Regions

**Scenario:**
```
ORIGINAL TEMPLATE:
<div data-ccms-ceid="intro">...</div>        ← 20 pages have intro content
<div data-ccms-ceid="body">...</div>
<div data-ccms-ceid="conclusion">...</div>

MODIFIED TEMPLATE (removed intro and conclusion):
<div data-ccms-ceid="body">...</div>

AFTER UPDATE:
20 pages lose their intro and conclusion content PERMANENTLY
```

**Impact:** High — Content is deleted and cannot be automatically recovered

#### Risk 2: Content Mismatch from Renamed IDs

**Scenario:**
```
ORIGINAL TEMPLATE:
<div data-ccms-ceid="article-title">...</div>

MODIFIED TEMPLATE:
<div data-ccms-ceid="page-heading">...</div>   ← ID changed!

AFTER UPDATE:
Pages have empty "page-heading" region
Original "article-title" content is orphaned and lost
```

**Impact:** High — Content becomes inaccessible

#### Risk 3: Structural Changes Breaking Pages

**Scenario:**
```
ORIGINAL TEMPLATE:
Two-column layout (content + sidebar)

MODIFIED TEMPLATE:
Three-column layout (completely different structure)

AFTER UPDATE:
All pages reorganize
Content may appear in wrong columns
Design breaks for existing page content
```

**Impact:** High — Visual and functional breaks across all pages

#### Risk 4: Formatting Loss from Container Changes

**Scenario:**
```
ORIGINAL TEMPLATE:
<div class="article-content" data-ccms-ceid="body">...</div>

MODIFIED TEMPLATE:
<section class="new-section" data-ccms-ceid="body">...</section>

AFTER UPDATE:
ID matches, so content is technically preserved
But CSS classes changed, so styling/layout breaks
```

**Impact:** Medium — Visual changes may require manual fixes

### Best Practices for Safe Template Management

#### Practice 1: Plan Carefully Upfront

Create templates correctly from the start:
- ✅ Think through editable regions before creating pages
- ✅ Use meaningful, permanent IDs
- ✅ Plan for content types and variations
- ✅ Test templates thoroughly with sample content

#### Practice 2: Only Update When Critical

Update templates only for:
- ✅ Bug fixes in template structure
- ✅ Minor CSS styling improvements
- ✅ Adding new optional regions (safe)
- ✅ Emergency layout corrections

Do NOT update for:
- ❌ Removing old regions (content loss)
- ❌ Renaming region IDs (content orphaned)
- ❌ Complete redesigns (use new template instead)
- ❌ Routine design changes (create new template)

#### Practice 3: Create New Templates for Major Changes

Instead of updating an existing template:

```
WRONG APPROACH:
Template v1 (50 pages using it)
  ↓ Modify extensively
  ↓ Update all 50 pages
  ↓ Risk losing content and breaking pages

RIGHT APPROACH:
Template v1 (50 pages still using it)
Create Template v2 (new design)
Gradually migrate pages from v1 to v2
  ↓ Allows testing and review
  ↓ Keeps old version if problems arise
  ↓ No automatic content loss
```

#### Practice 4: Test on Single Page First

If you must update a template:

```
Step 1: Modify template
Step 2: Go to Template → Pages list
Step 3: Click "Update" on ONE page
Step 4: Verify it worked correctly
Step 5: Review the updated page
Step 6: If good → Update all pages
Step 7: If problems → Revert changes
```

**Always test before bulk updates.**

#### Practice 5: Keep Backup/Versioning Strategy

Before major template updates:
- [ ] Have a backup of the current template code
- [ ] Know which pages use this template
- [ ] Understand what content they contain
- [ ] Have a plan to rollback if needed

#### Practice 6: Communicate Changes

If working with content editors:
- ⚠️ Notify them before updating templates
- ⚠️ Explain what's changing
- ⚠️ Tell them to review their pages after update
- ⚠️ Provide instructions for any manual fixes needed

#### Practice 7: Preserve Region IDs

If you need to modify a template while preserving content:

```
SAFE MODIFICATIONS:
✅ Change CSS classes (styling only)
✅ Add new editable regions (content creators fill them)
✅ Modify non-editable content (static text/design)
✅ Adjust HTML structure around editable regions

UNSAFE MODIFICATIONS:
❌ Remove editable regions (content lost)
❌ Change data-ccms-ceid values (content orphaned)
❌ Move editable regions to different HTML elements
❌ Change editable region containers
```

---

## Template Library Best Practices

### Organization

Keep templates organized by creating clear categories:

```
Content Templates
├─ Blog Post
├─ News Article
├─ Tutorial

Landing Templates
├─ Campaign - Hero Focus
├─ Campaign - Product Focus
├─ Webinar Registration

Product Templates
├─ Product - Basic
├─ Product - Featured
├─ Product Review

Service Templates
├─ Service - Full Description
├─ Service - Brief Overview
├─ Case Study

Utility Templates
├─ Contact Form
├─ FAQ
├─ Resource Download
```

### Documentation

For each template, document:

```
TEMPLATE NAME: Blog Post

DESCRIPTION:
Used for publishing blog articles, news, and long-form content.

USE CASES:
- Blog posts and articles
- News announcements
- Tutorial content
- Research summaries

EDITABLE REGIONS:
- article-header: Title, author, date
- article-body: Main article content (rich text)
- author-info: Author biography (optional)

NOTES:
- Supports featured image via CSS background
- Maximum content: ~2000 words recommended
- SEO-friendly structure
- Responsive design optimized for mobile

CREATED: [Date]
LAST UPDATED: [Date]
USED BY: [Number] pages
```

### Versioning

When you create multiple versions of a template:

```
POOR APPROACH:
Blog Post
Blog Post v2
Blog Post v3
[Which one to use? Confusing.]

BETTER APPROACH:
Blog Post - Standard
Blog Post - Featured
Blog Post - Interview
[Purpose is clear]
```

---

## Checklist: Template Completion

Before moving to Phase 4, verify:

- [ ] **All Planned Templates Created**
  - One template for each content type from Phase 1 planning
  - Naming convention applied consistently
  
- [ ] **Editable Regions Properly Configured**
  - All regions use `data-ccms-ceid` with unique IDs
  - No nested or duplicate regions
  - Meaningful region IDs
  
- [ ] **CKEditor Rich Text Setup**
  - Main content regions support rich text editing
  - Preview shows formatting works correctly
  
- [ ] **Templates Tested**
  - Created test pages from each template
  - Editors can click and edit regions
  - Live Editor works and displays toolbar
  - All formatting options available
  
- [ ] **Documentation Added**
  - Each template has clear name and description
  - Usage notes added (when to use this template)
  - Special features or requirements noted
  
- [ ] **Update Strategy Documented**
  - Team understands template update risks
  - Process for updates established
  - Backup strategy in place
  - Who can modify templates identified
  
- [ ] **Team Training Complete**
  - Content editors understand editable regions
  - They know how to use CKEditor
  - They understand template purpose
  - Support contact identified for issues

---

## Common Issues and Solutions

### Issue: Editable Region Not Showing in Live Editor

**Cause:** Region ID missing or incorrectly formatted
**Solution:**
```html
<!-- Bad: Missing data- prefix -->
<div ccms-ceid="title">...</div>

<!-- Good: Correct attribute -->
<div data-ccms-ceid="title">...</div>
```

### Issue: Content Lost After Template Update

**Cause:** Region ID was changed or removed
**Solution:**
- Do NOT use "Update all pages" if you've removed regions
- Create a new template instead
- Consider manual content migration

### Issue: CKEditor Not Available for Region

**Cause:** Element type not supported for rich editing
**Solution:**
```html
<!-- Works with rich editor -->
<div data-ccms-ceid="content">...</div>

<!-- May not work with rich editor -->
<span data-ccms-ceid="content">...</span>
```

### Issue: Template Not Available When Creating Page

**Cause:** Template might not be saved, published, or associated with layout
**Solution:**
- Verify template is saved (no validation errors)
- Check template is associated with a Site Design
- Try refreshing the page list

---

## Next Steps

Once templates are created:

1. **Review with team** — Make sure templates match Phase 1 planning
2. **Create test pages** — One page per template to verify functionality
3. **Train content editors** — Show them how to use templates and CKEditor
4. **Proceed to Phase 4** — Build your home page using a template

**Phase 4:** Building Home Page - Create your site's first published page using these templates.

---

## Resources

- [Phase 2: Creating Layouts Guide](./02-Creating-Layouts.md) - Site Design foundation
- [Phase 4: Building Home Page Guide](./04-Building-Home-Page.md) - Using templates
- [Phase 5: Building Initial Pages Guide](./05-Building-Initial-Pages.md) - Content strategy
- [Existing Templates Documentation](../Templates/Readme.md) - Technical reference
- [Website Launch Workflow](./Website-Launch-Workflow.md) - Full overview

### External Resources

- [CKEditor Documentation](https://ckeditor.com/ckeditor-5/documentation/)
- [HTML Best Practices](https://developer.mozilla.org/en-US/docs/Web/Guide/HTML/Introduction)
- [CSS Responsive Design](https://developer.mozilla.org/en-US/docs/Learn/CSS/CSS_layout/Responsive_Design)

---

**Duration Estimate:** 8-16 hours  
**Key Deliverables:** 4-8 complete templates with editable regions

**When to Proceed to Phase 4:** When all planned templates are created, tested, and content editors understand how to use them.
