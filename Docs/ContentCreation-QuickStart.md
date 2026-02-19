---
title: Content Creator Quick Start
description: Get started creating content in SkyCMS—from site design to your first published page
keywords: quickstart, content creation, layouts, templates, pages, editors, getting-started
audience: [content-creators, editors, designers]
version: 2.0
updated: 2026-02-18
canonical: /ContentCreation-QuickStart.html
status: stable
---

# Content Creator Quick Start

Ready to create content in SkyCMS? This guide walks you through the essential building blocks: **Layouts** (site-wide design) → **Templates** (page structure) → **Pages** (your content) → **Editing** (using the right tool for the job).

By the end, you'll have published your first page. ⚡

---

## Prerequisites

✅ SkyCMS is **installed and running**  
✅ You have **admin or editor access** to the dashboard  
✅ A **layout** has been installed (via the Setup Wizard or manually imported)

**Not installed yet?** Start with [Installation Quick Start](./QuickStart.md) first.

---

## The Content Creation Workflow

```
┌─────────────────────────────────────────────┐
│ Step 1: Choose/Create a Layout              │
│ (Defines site-wide header, footer, design)  │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│ Step 2: Create a Page Template              │
│ (Defines editable regions for page types)   │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│ Step 3: Create a Page                       │
│ (Add your content using the template)       │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│ Step 4: Edit & Format Content               │
│ (Use Live Editor, Designer, or Code Editor) │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│ Step 5: Publish & Schedule                  │
│ (Go live immediately or schedule for later) │
└─────────────────────────────────────────────┘
```

---

## Step 1: Choose or Create a Layout {#step-1-layout}

A **Layout** defines the look and feel of every page on your site—headers, footers, navigation, and global assets.

### If a layout is already installed:
1. Navigate to **Layouts** from the main menu
2. You'll see the currently active layout
3. ✅ **You're ready to move to Step 2**

### If you need to create or import a layout:
1. Navigate to **Layouts** → **Import Design** (or **Add New**)
2. Choose a pre-built design from the Community Layouts library, OR start from scratch
3. Preview and install
4. Once installed, it becomes the default layout for all new pages

📖 **Full guide:** [Layouts User Guide](./Layouts/)

---

## Step 2: Create a Page Template {#step-2-template}

A **Page Template** defines the structure of your pages—where the title goes, where the body content goes, where images can be placed, etc.

### Quick path:

1. Navigate to **Templates** from the main menu
2. Click **New Template** or **Add Template**
3. Choose an editor:
   - **Visual Designer** (drag-and-drop, GrapeJS)
   - **Code Editor** (raw HTML with syntax highlighting)
4. Define **editable regions** where content editors can add content:
   ```html
   <!-- Title area (minimal toolbar) -->
   <h1 data-ccms-ceid="title" data-editor-config="title">Page Title</h1>
   
   <!-- Main content area (full toolbar) -->
   <div data-ccms-ceid="body" class="ck-content">
     Your content goes here
   </div>
   ```
5. Click **Save**

📖 **Full guide:** [Page Templates Guide](./Templates/)  
💡 **Tips:** Use meaningful region names (e.g., `title`, `body`, `sidebar`), keep editable regions large enough for content

---

## Step 3: Create a Page {#step-3-page}

Now you'll create your first **actual page** using the template you just set up.

### Quick path:

1. From the main menu, click **Pages** or **New Page**
2. Enter a **page title**
3. Select your **template** from the dropdown
4. Click **Create**
5. You'll be taken to the **Live Editor** with your template loaded
6. ✅ **Move to Step 4 to add content**

---

## Step 4: Edit & Format Content {#step-4-editing}

Now it's time to add your content. SkyCMS provides three editors for different needs:

### Live Editor (Recommended for most users) {#live-editor}
- **Best for:** Quick content updates, formatting, and previews
- **Features:** Rich-text editing, drag-and-drop widgets, real-time preview
- **How to use:**  
  1. Click into any editable region
  2. Type or paste content
  3. Use the toolbar to **bold**, *italicize*, add links, headings, etc.
  4. See changes in real-time

📖 [Live Editor Guide](./Editors/LiveEditor/README.md) · [Live Editor Quick Start](./Editors/LiveEditor/QuickStart.md)

### Designer (For layouts and complex designs) {#designer}
- **Best for:** Building page layouts, adding components, visual design
- **Features:** Drag-and-drop canvas, component blocks, responsive preview
- **When to use:** Creating new sections, adding sidebars, restructuring layout

📖 [Designer Guide](./Editors/Designer/README.md) · [Designer Quick Start](./Editors/Designer/QuickStart.md)

### Code Editor (For developers) {#code-editor}
- **Best for:** Fine-grained control over HTML, custom attributes, advanced layouts
- **Features:** Syntax highlighting, Monaco editor, HTML validation
- **When to use:** Working with custom code, adding data attributes, debugging markup

📖 [Code Editor Guide](./Editors/CodeEditor/README.md)

### Image Editing {#image-editing}
- Crop, rotate, and optimize images directly in SkyCMS
- No need to pre-edit in external tools

📖 [Image Editing Guide](./Editors/ImageEditing/README.md)

---

## Step 5: Publish & Schedule {#step-5-publish}

### Publish immediately:
1. Click **Publish** (in the top toolbar)
2. Your page is **live instantly** — no build pipeline, no delay
3. Share the URL

### Schedule for later:
1. Click **Schedule** (instead of Publish)
2. Choose date and time
3. SkyCMS will publish automatically at that time
4. You can cancel or update the schedule anytime

📖 [Page Scheduling Guide](./Editors/PageScheduling.md)

---

## File Management {#file-management}

Need to upload images, PDFs, or other assets?

1. From the main menu, click **Files** or **File Manager**
2. **Upload** files by dragging and dropping or clicking the upload button
3. Organize files into folders
4. Use the file browser in the Live Editor or Designer to insert media

📖 [File Management Guide](./FileManagement/index.md) · [File Management Quick Start](./FileManagement/Quick-Start.md)

---

## Tips for Success

### Layout best practices
- ✅ Use a professional, responsive layout (most community layouts are mobile-ready)
- ✅ Choose a layout that matches your site's purpose (blog, business, portfolio)

### Template design
- ✅ Create templates for common page types (post, landing page, product, etc.)
- ✅ Use descriptive region names so editors know what content goes where
- ✅ Keep templates simple at first—you can always add more structure later

### Content editing
- ✅ Use the **Live Editor** for 90% of your content work
- ✅ Preview on mobile before publishing
- ✅ Use meaningful **page titles** (they become part of the URL)
- ✅ Schedule important announcements in advance

### Publishing
- ✅ Publish when ready; there's no review cycle (or add one via your workflow)
- ✅ Use scheduling for time-sensitive announcements
- ✅ Test links and images before publishing to production

---

## What's Next?

Once you've published your first page, explore:

- **[Blog Management](./blog/BlogPostLifecycle.md)** — If you're managing articles or posts
- **[Navigation Builder Widget](./Widgets/Nav-Builder-Widget.md)** — For site menus
- **[Advanced Editing](./Editors/LiveEditor/TechnicalReference.md)** — For power users
- **[Widgets & Components](./Widgets/README.md)** — For embeds, forms, search, and more

---

## Need Help?

- **Can't find a feature?** Check the [Complete Documentation Index](./index.md#complete-documentation-index)
- **Have a question?** See the [FAQ](./FAQ.md)
- **Found a bug?** Report it on [GitHub](https://github.com/CWALabs/SkyCMS/issues)

---

**You're ready to create!** Start with [Step 1: Choose a Layout](#step-1-layout) above. 🚀
