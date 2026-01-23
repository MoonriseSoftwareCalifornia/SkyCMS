---
title: Live Editor Visual Guide
description: Visual reference guide for Live Editor interface elements and components
keywords: live-editor, visual-guide, interface, UI
audience: [content-creators, developers]
---

# Live Editor Visual Guide

This guide provides descriptions of the Live Editor's visual elements to help you identify and use various interface components.

## Interface Overview

### Main Screen Layout

![Live Editor main layout](../../images/screenshots/live-editor-dashboard.webp)

```
┌─────────────────────────────────────────────────────────────────┐
│ SkyCMS Navigation Bar                                           │
│ [Logo] [Page Title] [Version] [Save] [Preview] [Close] [More]  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│                    Editable Content Area                        │
│                    (displayed in iframe)                        │
│                                                                 │
│  ┌───────────────────────────────────────────────────────┐    │
│  │ ⊕  Your page content appears here...                  │    │
│  │                                                        │    │
│  │    Editable regions have subtle dashed borders       │    │
│  │    when you hover over them.                         │    │
│  │                                                        │    │
│  │    Click any editable area to start editing.         │    │
│  └───────────────────────────────────────────────────────┘    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Toolbar Components

### 1. Main Toolbar (Top)

![Live Editor toolbar over editable region](../../images/screenshots/live-editor-editing-toolbar.webp)

Located at the very top of the editor interface when editing an editable region:

```
┌────────────────────────────────────────────────────────────────┐
│ [Heading ▼] | [B] [I] [U] | [🔗] [📄] [🖼️] [📹] [▦] [""] [<>] |
│ [• ‣] [1. 2.] [☐ To-do] [←→]                                   │
└────────────────────────────────────────────────────────────────┘
```

**Icons explained:**
- **Heading ▼** - Paragraph style selector (H1, H2, H3, etc.)
- **B** - Bold text
- **I** - Italic text
- **U** - Underline text
- **🔗** - Insert/edit link
- **📄** - Insert page link (SkyCMS custom)
- **🖼️** - Insert image
- **📹** - Embed media (video)
- **▦** - Insert table
- **""** - Block quote
- **<>** - Code block
- **• ‣** - Bulleted list
- **1. 2.** - Numbered list
- **☐ To-do** - Todo/checklist
- **←→** - Indent/outdent

### 2. Balloon Toolbar

Appears above selected text in a floating "balloon":

```
     ╭────────────────────────────────────╮
     │ [B] [I] | [🔗] [📄] [🖼️] | [•] [1.] │
     ╰────────────┬───────────────────────╯
                  │
                  ▼
         Your selected text
```

**Simplified controls for:**
- Bold / Italic
- Link insertion
- Image insertion
- List creation

### 3. Block Toolbar Button

Appears in the left margin when you hover over or click into a paragraph or block:

```
│
│  ⊕ ← Click this button
│  │
│  │  This is a paragraph or heading.
│  │  The block toolbar button appears
│  │  on the left when you interact with
│  │  this block-level element.
│
```

**Clicking ⊕ opens a menu with:**
- Paragraph styles (P, H1-H6)
- Insert options (table, image, media, code block)
- List options

## Editable Region Indicators

### Default State (Not Editing)

```
┌ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐
   
│  Your content here...              │
   Hover to see the dashed border
│                                    │
   
└ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘

    Tooltip: "Editable."
```

### Active State (You Are Editing)

```
┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
┃ ⊕                                 ┃
┃   Your content here...            ┃
┃   Solid border indicates active   ┃
┃   Cursor blinking                 ┃
┃                                   ┃
┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛
```

### Locked State (Someone Else Editing)

```
┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓  RED BORDER
┃                                   ┃
┃   Another user is editing...      ┃
┃   [🔒 Locked - user@example.com]  ┃
┃                                   ┃
┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

    Tooltip: "user@example.com"
```

## Image Editing Interface

![Insert image flow with toolbar](../../images/screenshots/live-editor-insert-image.webp)

### Inserted Image with Toolbar

```
┌─────────────────────────────────────────┐
│  ╭────────────────────────────────────╮ │
│  │ [Caption] [Alt] | [Styles ▼] [↔️]  │ │
│  ╰──────────┬─────────────────────────╯ │
│             ▼                            │
│  ┌──────────────────────────────┐       │
│  │                              │       │
│  │    [Your Image Here]         │       │
│  │                              │       │
│  └──────────────────────────────┘       │
│                                         │
│  Type caption here                      │
└─────────────────────────────────────────┘
```

**Image toolbar buttons:**
- **Caption** - Toggle caption visibility
- **Alt** - Set alternative text for accessibility
- **Styles** - Choose image style (inline, wrapped, break text)
- **↔️** - Resize image

### Image Resize Handles

```
    ○─────────────────○
    │                 │
    │   Your Image    │
    │                 │
    ○─────────────────○
    
    Drag corners to resize
```

## Table Interface

### Table with Active Cell

```
┌─────────────────────────────────────────────────┐
│ Table Toolbar: [+Col] [+Row] [Merge] [Props]   │
└─────────────────────────────────────────────────┘
    
    ┏━━━━━━━━━┯━━━━━━━━━┯━━━━━━━━━┓
    ┃ Header 1│ Header 2│ Header 3┃
    ┣━━━━━━━━━┿━━━━━━━━━┿━━━━━━━━━┫
    ┃ Cell 1  │ Cell 2  │ Cell 3  ┃  ← Active cell
    ┠─────────┼─────────┼─────────┨    (being edited)
    ┃ Cell 4  │ Cell 5  │ Cell 6  ┃
    ┗━━━━━━━━━┷━━━━━━━━━┷━━━━━━━━━┛
```

## Modal Dialogs

### VS Code Editor Modal

```
┌───────────────────────────────────────────────────────┐
│ ╔═══════════════════════════════════════════════════╗ │
│ ║ [VS Code Icon] Code Editor      [Cancel] [Apply] ║ │
│ ╠═══════════════════════════════════════════════════╣ │
│ ║  1  <div class="container">                       ║ │
│ ║  2    <h1>Your Content</h1>                       ║ │
│ ║  3    <p>Edit HTML directly...</p>                ║ │
│ ║  4  </div>                                        ║ │
│ ║  5                                                ║ │
│ ║                                                   ║ │
│ ║  [Monaco Editor with syntax highlighting]        ║ │
│ ║                                                   ║ │
│ ╚═══════════════════════════════════════════════════╝ │
└───────────────────────────────────────────────────────┘
```

### Page Link Modal

```
┌───────────────────────────────────────────────────────┐
│ ╔═══════════════════════════════════════════════════╗ │
│ ║ Insert Page Link                     [X] Close   ║ │
│ ╠═══════════════════════════════════════════════════╣ │
│ ║                                                   ║ │
│ ║  Search pages: [________________] 🔍              ║ │
│ ║                                                   ║ │
│ ║  ┌─────────────────────────────────────────────┐ ║ │
│ ║  │ ☐ Home                                      │ ║ │
│ ║  │ ☐ About Us                                  │ ║ │
│ ║  │ ☑ Contact                     ← Selected    │ ║ │
│ ║  │ ☐ Services                                  │ ║ │
│ ║  └─────────────────────────────────────────────┘ ║ │
│ ║                                                   ║ │
│ ║  Link Text: [Contact Us____________]             ║ │
│ ║  ☐ Open in new window                            ║ │
│ ║  CSS Class: [____________________]               ║ │
│ ║                                                   ║ │
│ ║                        [Cancel] [Insert Link]    ║ │
│ ╚═══════════════════════════════════════════════════╝ │
└───────────────────────────────────────────────────────┘
```

### Image Upload Modal

```
┌───────────────────────────────────────────────────────┐
│ ╔═══════════════════════════════════════════════════╗ │
│ ║ File Manager                         [X] Close   ║ │
│ ╠═══════════════════════════════════════════════════╣ │
│ ║                                                   ║ │
│ ║  Path: /pub/articles/123/                        ║ │
│ ║                                                   ║ │
│ ║  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐            ║ │
│ ║  │ 📁   │ │ 🖼️   │ │ 🖼️   │ │ 🖼️   │            ║ │
│ ║  │ img  │ │img1  │ │img2  │ │img3  │            ║ │
│ ║  └──────┘ └──────┘ └──────┘ └──────┘            ║ │
│ ║                                                   ║ │
│ ║  ┌─────────────────────────────────────────────┐ ║ │
│ ║  │ Drop files here or click to upload         │ ║ │
│ ║  │         [FilePond Upload Area]             │ ║ │
│ ║  └─────────────────────────────────────────────┘ ║ │
│ ║                                                   ║ │
│ ║                               [Cancel] [Select]  ║ │
│ ╚═══════════════════════════════════════════════════╝ │
└───────────────────────────────────────────────────────┘
```

## Toast Notifications

### Save Status (Top Right)

```
                             ┌─────────────────────┐
                             │ ✓ Changes Saved     │
                             └─────────────────────┘
```

### Link Click Warning

```
                             ┌─────────────────────────────┐
                             │ Links are disabled          │
                             │ while editing        [X]    │
                             └─────────────────────────────┘
```

### Error Notification

```
                             ┌─────────────────────────────┐
                             │ ⚠ Error Saving              │
                             │ Please try again     [X]    │
                             └─────────────────────────────┘
```

## Navigation Bar Breakdown

```
┌──────────────────────────────────────────────────────────────────────┐
│                                                                      │
│  [Logo]  📄 Page Title: "About Us"  v. 5                            │
│                                                                      │
│  [💾 Save] [👁️ Preview] [✕ Close] [📋 Other Pages ▼]              │
│                                                                      │
│  [📜 Versions] [</> Code Editor] [🎨 Designer]                      │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

**Navigation Elements:**
- **Logo** - SkyCMS branding
- **Page Title** - Current page being edited
- **Version Number** - Current working version
- **Save Button** - Manual save (💾)
- **Preview Button** - View as visitor sees it (👁️)
- **Close Button** - Exit editor (✕)
- **Other Pages** - Quick navigation dropdown
- **Versions** - Version history (📜)
- **Code Editor** - Switch to Monaco editor (</>)
- **Designer** - Switch to GrapesJS visual designer (🎨)

## Link Creation Workflow

### Step 1: Select Text

```
  The quick brown fox jumps over the lazy dog.
         ^^^^^^^^^^^^
         Selected text
```

### Step 2: Toolbar Appears

```
     ╭─────────────────────────────╮
     │ [B] [I] | [🔗] [🖼️] | [•]  │
     ╰─────────┬───────────────────╯
               │
               ▼
  The quick brown fox jumps over the lazy dog.
         ^^^^^^^^^^^^
```

### Step 3: Link Dialog

```
┌────────────────────────────────────┐
│ Link URL: [https://example.com___] │
│ ☑ Open in new tab                  │
│                                    │
│           [Cancel] [Save]          │
└────────────────────────────────────┘
```

### Step 4: Linked Text

```
  The quick brown fox jumps over the lazy dog.
         ‾‾‾‾‾‾‾‾‾‾‾‾
         Underlined (link created)
```

## Save Status Indicator

Located in navigation bar, shows current save state:

```
Status: Saving...
┌─────────────────────┐
│ [⟲] Saving...       │
└─────────────────────┘

Status: Saved
┌─────────────────────┐
│ [✓] Saved at 2:34pm │
└─────────────────────┘

Status: Error
┌─────────────────────┐
│ [⚠] Error saving    │
└─────────────────────┘
```

## Media Embed Preview

### Before Embedding

```
┌───────────────────────────────────────┐
│ Media Embed URL:                      │
│ [https://youtube.com/watch?v=xxxxx__] │
│                                       │
│                  [Insert]             │
└───────────────────────────────────────┘
```

### After Embedding

```
┌─────────────────────────────────────────┐
│ ╔═════════════════════════════════════╗ │
│ ║                                     ║ │
│ ║     [Video Preview or Player]       ║ │
│ ║                                     ║ │
│ ╚═════════════════════════════════════╝ │
│                                         │
│  ↔️ Resize handles on corners           │
└─────────────────────────────────────────┘
```

## Code Block Display

### Inserting Code

```
Click [<>] Code Block button in toolbar

┌──────────────────────────────────────┐
│ function example() {                 │
│   console.log("Hello World");        │
│   return true;                       │
│ }                                    │
└──────────────────────────────────────┘
```

### Rendered Code Block

```
┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
┃ function example() {                ┃
┃   console.log("Hello World");       ┃
┃   return true;                      ┃
┃ }                                   ┃
┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛
    Monospace font, special background
```

## Recovery Modal

Appears when unsaved changes are detected:

```
┌─────────────────────────────────────────────────┐
│ ╔═══════════════════════════════════════════╗   │
│ ║ 💾 Unsaved Changes Detected               ║   │
│ ╠═══════════════════════════════════════════╣   │
│ ║                                           ║   │
│ ║  We found unsaved changes from a          ║   │
│ ║  previous editing session.                ║   │
│ ║                                           ║   │
│ ║  Would you like to restore them?          ║   │
│ ║                                           ║   │
│ ║                                           ║   │
│ ║          [Discard] [Restore Changes]      ║   │
│ ╚═══════════════════════════════════════════╝   │
└─────────────────────────────────────────────────┘
```

## Color Key for Visual Elements

Throughout this guide, different visual styles indicate:

- **Dashed borders (┌ ─ ─ ┐)** - Editable regions (inactive)
- **Solid borders (┌────┐)** - Active UI elements
- **Double borders (┏━━━┓)** - Focused/selected elements
- **Dotted lines (┊)** - Hidden or background elements
- **Heavy borders (╔════╗)** - Modal dialogs and overlays
- **Red notation** - Warning or locked state
- **Green notation** - Success or available state

## Tips for Interface Recognition

1. **Look for the ⊕ symbol** - This is always your block toolbar button
2. **Dashed borders on hover** - Indicates editable content
3. **Red borders** - Someone else is editing that region
4. **Balloon appears on selection** - You've selected editable text
5. **Icons in toolbar** - Standard formatting and insert functions

---

*This visual guide complements the [Complete User Guide](README.md) and [Quick Start Guide](QuickStart.md).*

*Last Updated: October 2025*
