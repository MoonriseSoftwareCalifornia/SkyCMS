---
title: Live Editor Documentation
description: Complete documentation for SkyCMS WYSIWYG Live Editor with CKEditor 5
keywords: live-editor, CKEditor, WYSIWYG, editor, content-editing
audience: [content-creators, developers]
---

# SkyCMS Live Editor Documentation (2025)



---

## Table of Contents

1. [Overview](#overview)
2. [End User Guide](#end-user-guide)
3. [Developer & Administrator Guide](#developer--administrator-guide)
4. [Troubleshooting & Resources](#troubleshooting--resources)

---

## Overview

The SkyCMS Live Editor is an inline, WYSIWYG content editor built on CKEditor 5 (Balloon Block Editor) and extended with custom SkyCMS plugins. It enables direct, in-context editing of web pages, supporting rich formatting, media, collaboration, and advanced developer features.

**Key Features:**
- Inline editing in published context
- Balloon and block toolbars for formatting
- Real-time collaboration (SignalR)
- Auto-save and manual save
- Rich media: images, tables, videos, code blocks
- Custom plugins: PageLink, FileLink, InsertImage, VS Code Editor
- Version control and recovery

---

## End User Guide

### Getting Started

1. Log in to SkyCMS and navigate to the Editor → Index
2. Click **Edit** on any page with editable regions (`data-ccms-ceid`)
3. Editable areas show dashed borders on hover; click to activate
4. Use the balloon toolbar (appears on text selection) and block toolbar (⊕ button at left) for formatting and inserting content

**System Requirements:**
- Modern browser (Chrome, Firefox, Edge, Safari)
- JavaScript enabled
- User role: Author, Editor, Administrator, Team Member

### Editing Content

- **Text Formatting:** Bold, italic, underline, headings (H1-H6), lists (bulleted, numbered, todo), block quotes, code blocks
- **Links:**
  - Standard: Select text, click Link, enter URL, choose "Open in new tab" if needed
  - **PageLink (SkyCMS):** Insert internal page links via searchable modal, set link text, target, CSS class, styles
- **Images:**
  - Insert via upload (file manager modal) or URL
  - Crop, resize, caption, alt text, style, alignment
  - Banner image selection on upload
- **Tables:** Insert, format, merge/split cells, set properties
- **Media:** Embed videos (YouTube, Vimeo, etc.)
- **Files:** Insert downloadable file links (FileLink plugin)
- **Code Editor:** Switch to Monaco (VS Code) editor for direct HTML editing

### Toolbars

- **Main Toolbar:** Heading, bold, italic, underline, page link, image insert, media embed, table, block quote, code block, lists, indent/outdent
- **Balloon Toolbar:** Appears on selection; bold, italic, underline, page link, link, image insert, lists
- **Block Toolbar:** ⊕ button for block-level actions

### Saving & Version Control

- **Auto-save:** Changes saved after 1s idle
- **Manual save:** Ctrl+S / Cmd+S or Save button
- **Versioning:** Each save creates a new version; access via Versions button
- **Recovery:** Unsaved changes restored on browser reopen

### Collaboration

- Real-time editing with region locking (red border if another user is editing)
- Toast notifications for save status, collaboration, and link clicks

### Keyboard Shortcuts

| Action      | Windows/Linux | Mac      |
|-------------|--------------|----------|
| Save        | Ctrl+S       | Cmd+S    |
| Bold        | Ctrl+B       | Cmd+B    |
| Italic      | Ctrl+I       | Cmd+I    |
| Underline   | Ctrl+U       | Cmd+U    |
| Undo        | Ctrl+Z       | Cmd+Z    |
| Redo        | Ctrl+Y       | Cmd+Y    |
| Link        | Ctrl+K       | Cmd+K    |

---

## Developer & Administrator Guide {#developer--administrator-guide}

### Architecture

- **Frontend:** CKEditor 5 (Balloon Block Editor), Monaco Editor, custom plugins
- **Backend:** ASP.NET Core MVC, Razor views, SignalR for real-time

### CKEditor Configuration

- **File:** `Editor/wwwroot/lib/cosmos/ckeditor/ckeditor-widget.301.js`
- **Plugins:**
  - Core: Autoformat, Autosave, Essentials, Paragraph, PasteFromOffice
  - Formatting: Bold, Italic, Underline, Heading
  - Block: BlockQuote, CodeBlock
  - Lists: List, ListProperties, TodoList
  - Images: ImageBlock, ImageCaption, ImageInline, ImageInsert, ImageInsertViaUrl, ImageResize, ImageStyle, ImageTextAlternative, ImageToolbar, ImageUpload
  - Media: MediaEmbed
  - Tables: Table, TableCaption, TableCellProperties, TableColumnResize, TableProperties, TableToolbar
  - Utilities: Indent, IndentBlock, Link, LinkImage, TextTransformation
  - **Custom:** PageLink, FileLink, InsertImage, VsCodeEditor, SignalR

- **Toolbar Items:**
  - Main: heading, pageLink, imageInsert, insertImage, mediaEmbed, insertTable, blockQuote, codeBlock, bulletedList, numberedList, todoList, outdent, indent
  - Balloon: bold, italic, underline, bookmark, pageLink, link, insertImage, bulletedList, numberedList

- **Image Upload:** `/FileManager/SimpleUpload/{articleNumber}` (with credentials)
- **Autosave:** 1000ms debounce, calls parent frame save function
- **HTML Support:** All styles, attributes, classes allowed
- **Link Decorators:** External links open in new tab; downloadable links supported

### Custom Plugins

- **PageLink:** Internal page linking modal (`pagelink/`)
- **FileLink:** Downloadable file links (`filelink/`)
- **InsertImage:** File manager, cropping, upload (`insertimage/`)
- **VsCodeEditor:** Monaco code editor modal (`vscodeeditor/`)
- **SignalR:** Real-time collaboration (`signalr/`)

### Extending the Editor

To add plugins or toolbar items:
1. Import your plugin in `ckeditor-widget.301.js`
2. Add to the `plugins` array
3. Add to `toolbar.items` or `balloonToolbar` as needed

### Server-Side Components

- **Controller:** `Editor/Controllers/EditorController.cs`
  - `Edit`, `EditSaveRegion`, `EditSaveBody`, `CcmsContent` endpoints
- **Views:** `Edit.cshtml`, `CcmsContent.cshtml`
- **SignalR Hub:** `Sky.Cms.Hubs.LiveEditorHub` (`/___cwps_hubs_live_editor`)

### Security & Permissions

- Content encrypted (AES) before transmission
- Roles: Author, Editor, Administrator, Team Member
- Server and client-side validation

### Data Models

- `HtmlEditorViewModel`, `HtmlEditorPostViewModel`, `EditorRegionViewModel`
- See TechnicalReference.md for schema details

### Performance

- Auto-save throttling, local backup, SignalR connection pooling

### Testing & Deployment

- Manual: Editor load, region activation, toolbar, save, auto-save, collaboration, recovery, image upload, links, tables, code editor
- Automated: Controller actions, SignalR, JS save, encryption, permissions
- Deployment: License key, SignalR scaling, backup storage, SSL/TLS, CORS, cache headers, JS minification

---

## Troubleshooting & Resources {#troubleshooting--resources}

### Common Issues

- **Editor not loading:** Check JS enabled, permissions, refresh, clear cache
- **Cannot save:** Check connection, session, region lock, manual save, browser extensions
- **Formatting issues:** Preview before save, avoid pasting from Word, check CSS, use code editor
- **Image upload fails:** Check file size/format, storage, permissions
- **Toolbar missing:** Click editable region, refresh, try another browser
- **Collaboration conflicts:** Edit different regions, use version history
- **Recovery modal issues:** Restore/discard, check localStorage

### Getting Help

1. Check documentation (README, QuickStart, TechnicalReference)
2. Review troubleshooting guides
3. Check browser console (F12)
4. Contact administrator
5. Review server logs

### Resources

- [CKEditor 5 Documentation](https://ckeditor.com/docs/ckeditor5/latest/)
- [CKEditor 5 Balloon Block Editor](https://ckeditor.com/docs/ckeditor5/latest/examples/builds/balloon-block-editor.md)
- [Monaco Editor](https://microsoft.github.io/monaco-editor/)

---

## Document Index

- **README.md** – Complete User & Developer Guide
- **QuickStart.md** – Fast start for new users
- **VisualGuide.md** – Interface reference
- **TechnicalReference.md** – Developer details

---

*Last Updated: October 2025*  
*SkyCMS Version: Latest*  
*CKEditor Version: 5 (Balloon Block Editor)*
Once an image is inserted:

