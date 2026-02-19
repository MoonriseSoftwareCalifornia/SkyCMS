---
title: Creating Editable Areas
description: Developer guide for defining editable regions, headings, and image widgets in SkyCMS templates and articles
keywords: editable-regions, ckeditor, image-widget, data-ccms-ceid, duplicator
audience: [developers]
---

# Creating Editable Areas

A practical guide for developers to define editable regions in templates and stand-alone articles, using the code editor (Monaco) with CKEditor-based Live Editor.

## Quick Start (5 minutes)

- Mark editable regions with `data-ccms-ceid="my-id"` (or `data-ccms-new="true"` for auto-GUID on first save).
- Use block elements for rich content (e.g., `div`, `section`, `article`) and add `class="ck-content"` so CKEditor styles apply.
- Use headings (`h1`–`h6`) or `data-editor-config="title"`/`"heading"` for minimal toolbar (no embeds/tables).
- Use the image widget via `data-editor-config="image-widget"`; first image is typically treated as the page banner.
- Do **not** nest editable regions; backend rejects nested regions.

## Element Types & Toolbar Behavior

| Use Case | HTML | Config/Classes | Toolbar |
| --- | --- | --- | --- |
| Rich content | `<div>` / `<section>` / `<article>` / `<main>` / `<aside>` | `data-ccms-ceid` + `class="ck-content"` | Full toolbar (images, media, tables, lists, code blocks, etc.) |
| Title/subtitle | `<h1>`–`<h6>` or any block with `data-editor-config="title"`/`"heading"` | `data-ccms-ceid` | Minimal toolbar (bold/italic only) |
| Image widget | `<div data-editor-config="image-widget">` | `data-ccms-ceid` or `data-ccms-new` + `class="ck-content"` | FilePond-based upload UI |

Notes:
- `ck-content` keeps rendered content aligned with CKEditor’s CSS; avoid overriding its base rules.
- Additional block tags are allowed; prefer semantic blocks for layout.

## Required Data Attributes

- `data-ccms-ceid`: Stable ID for the region. Use descriptive IDs when practical.
- `data-ccms-new="true"`: Auto-assigns a GUID on first save (shared generator across widgets/editors).
- `data-editor-config` (optional): `title` / `heading` for minimal toolbar, `image-widget` for image uploads; omit for standard rich content.
- Optional for image widget: `data-ccms-enable-alt-editor="true"` to enable the alt/title modal per widget.

## Examples

Rich body block:
```html
<section data-ccms-ceid="body" class="ck-content">
  <p>Welcome to our product page.</p>
</section>
```

Title/subtitle:
```html
<h1 data-ccms-ceid="page-title" data-editor-config="title">Product Name</h1>
<h2 data-ccms-ceid="page-subtitle" data-editor-config="heading">Great tagline goes here</h2>
```

Image widget (auto-ID on first save):
```html
<div data-editor-config="image-widget" data-ccms-new="true" class="ck-content"></div>
```

Image widget with opt-in alt/title editor:
```html
<div data-editor-config="image-widget"
     data-ccms-new="true"
     data-ccms-enable-alt-editor="true"
     class="ck-content"></div>
```

## Rules & Conventions

- **No nesting:** Do not place an editable element inside another; backend validation will reject it and nested regions may be removed.
- **Stable IDs:** Keep `data-ccms-ceid` stable between template updates so content maps correctly.
- **Banner image convention:** The first image uploaded on a page is treated as the banner image by convention.
- **Iframe context:** Live Editor runs inside an iframe; parent functions handle saving and autosave.
- **Autosave:** CKEditor autosaves after ~1s idle when `parent.enableAutoSave` is true.

## Styling Guidance

- Apply `ck-content` to non-title editable areas so CKEditor’s content CSS (ckeditor5-content.css) styles lists, tables, images, etc.
- Avoid overriding `.ck-content` base rules; layer your component/page classes instead.
- Headings with minimal toolbar do **not** require `ck-content`, but can have it if you need consistent typography.

## Image Widget Highlights

- Uses FilePond uploads; supports drag/drop, replace, library browsing, delete.
- Upload path: `/pub/articles/{articleNumber}/` when `articleNumber` is available; otherwise `/pub/images/`.
- Alt/Title modal is enabled per widget via `data-ccms-enable-alt-editor="true"`; global toggle via `CCMS_IMAGE_WIDGET_CONFIG.enableAltTextEditor`.
- Emits custom events via `window.CCMSImageWidgetEvents` (`imageChanged` with `{ type, id, element, imageSrc }`).

## Duplicator Pattern (Reusable Blocks)

Use the duplicator to clone repeatable sections (e.g., cards, FAQs, feature rows):

- Wrap repeatable items with classes/components the duplicator expects (see `Editor/wwwroot/lib/cosmos/dublicator/dublicator.js`).
- Each cloned child should contain editable elements with `data-ccms-ceid` (or `data-ccms-new`).
- On clone, editors/widgets are re-initialized; on delete, editors/widgets are torn down to save the page body cleanly.
- New clones automatically get fresh `data-ccms-ceid` values to avoid ID collisions; avoid nesting editable regions inside each other.

Minimal pattern example:
```html
<div class="ccms-clone" data-ccms-ceid="feature-1">
  <h3 data-ccms-ceid="feature-1-title" data-editor-config="heading">Fast</h3>
  <div data-ccms-ceid="feature-1-body" class="ck-content">Our platform is fast.</div>
</div>
```
Then call `Duplicator.create(element)` on each clone container in script.

## Troubleshooting

- **Editable regions missing in Live Editor:** Check for `data-ccms-ceid` (or `data-ccms-new`) and ensure no nesting.
- **Content lost after template update:** IDs changed or regions removed; keep IDs stable.
- **Toolbar too minimal:** Remove `data-editor-config="title"/"heading"` or switch to a non-heading block for rich content.
- **Image widget not initializing:** Ensure `data-editor-config="image-widget"` and that FilePond scripts are loaded.
- **Styling looks off:** Confirm `class="ck-content"` on rich areas and avoid overriding CKEditor base styles.

## See Also

- Templates guide: [../Templates/Readme.md](../Templates/Readme.md)
- Live Editor reference: [../Editors/LiveEditor/README.md](../Editors/LiveEditor/README.md)
- Live Editor technical reference: [../Editors/LiveEditor/TechnicalReference.md](../Editors/LiveEditor/TechnicalReference.md)
- Duplicator code: [../../Editor/wwwroot/lib/cosmos/dublicator/dublicator.js](../../Editor/wwwroot/lib/cosmos/dublicator/dublicator.js)
- Image widget code: [../../Editor/wwwroot/lib/cosmos/image-widget/image-widget.js](../../Editor/wwwroot/lib/cosmos/image-widget/image-widget.js)
- CKEditor content CSS: [../../Editor/wwwroot/lib/ckeditor/ckeditor5-content.css](../../Editor/wwwroot/lib/ckeditor/ckeditor5-content.css)
