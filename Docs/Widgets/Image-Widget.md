---
title: Image Widget
description: Interactive image upload and management widget with drag-and-drop, library picker, and alt-text editor
keywords: widget, image, upload, FilePond, editor, drag-and-drop
audience: [developers]
---
## Cosmos CMS Image Widget

Interactive image upload and management widget used by the Sky Editor. It provides drag‑and‑drop uploads (via FilePond), an image library picker, in-place replacement, and an alt-text editor, then saves the widget’s HTML back to the editor region.

- Location (source): `Editor/wwwroot/lib/cosmos/image-widget/`
  - `image-widget.js`
  - `image-widget.css`
- Auto-init: On `DOMContentLoaded`, any `<div data-editor-config="image-widget">` on the page is initialized automatically.

## Features

- Drag‑and‑drop upload with progress (FilePond)
- Replace image via upload or library picker
- Alt text and title editor modal
- Delete image and reset back to upload state
- Custom event system for integration with parent applications

## Dependencies

Make sure these are loaded on pages using the widget:

- FilePond core
  - `Editor/wwwroot/lib/filepond/filepond.min.css`
  - `Editor/wwwroot/lib/filepond/filepond.min.js`
- FilePond File Metadata plugin
  - `Editor/wwwroot/lib/filepond-plugin-file-metadata/dist/filepond-plugin-file-metadata.min.js`
- Image widget CSS/JS
  - `Editor/wwwroot/lib/cosmos/image-widget/image-widget.css`
  - `Editor/wwwroot/lib/cosmos/image-widget/image-widget.js`
- Styling libraries (recommended in the Editor UI and used by the widget’s markup)
  - Bootstrap 5 (alerts, buttons, progress styles)
  - Font Awesome 6 (toolbar icons). If not present, replace the icon HTML in `CCMS_IMAGE_WIDGET_CONFIG`.

## Server endpoints and defaults

The widget posts uploads to your server and expects back a string URL to the uploaded file.

- Upload endpoint: `/FileManager/UploadImage`
- Image library endpoint: `/FileManager/GetImageAssets?path=...`
- Accepted file types: `.png, .jpg, .jpeg, .webp, .gif`
- Max file size: 25 MB (server should enforce)
- Upload path resolution:
  - If a global `articleNumber` exists: `/pub/articles/{articleNumber}/`
  - Otherwise: `/pub/images/`

These values are defined in `CCMS_IMAGE_WIDGET_CONFIG` inside `image-widget.js`.

## Parent page integration

The widget can optionally interact with the parent window (useful when the editor runs in an iframe). If you're not using an iframe, `parent` resolves to `window`.

Optional integration:

- `parent.saveInProgress` — boolean flag the widget sets to `false` after upload completes or fails
- `window.articleNumber` — if set, directs uploads to `/pub/articles/{articleNumber}/`

**Note:** The widget no longer requires `parent.saveEditorRegion()`, `parent.saveChanges()`, or `parent.saving()` callbacks. Use the `CCMSImageWidgetEvents` event dispatcher (see Custom events section below) to respond to image upload and deletion events.

## Markup API

Add a container div that the widget will manage. Include the container class for proper toolbar visibility.

Required attributes/classes:

- `data-editor-config="image-widget"`
- One of:
  - `data-ccms-ceid="your-stable-id"` — use a fixed ID you manage
  - `data-ccms-new="true"` — widget will generate a new GUID and replace this attribute
- `class="ccms-img-widget-container"` — enables toolbar visibility via CSS

Pre-populated state (show an existing image): place an `<img>` inside. The widget will attach actions (edit alt, replace, library, delete).

## Simple HTML example

This example shows a page that loads required assets, wires the minimal save callbacks, and renders a new image widget that generates its own ID.

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Cosmos Image Widget Demo</title>

    <!-- Optional (recommended in Editor UI) -->
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@6.5.2/css/all.min.css" />

    <!-- FilePond -->
    <link rel="stylesheet" href="/lib/filepond/filepond.min.css" />

    <!-- Image Widget styles -->
    <link rel="stylesheet" href="/lib/cosmos/image-widget/image-widget.css" />
  </head>
  <body class="p-4">
    <h1 class="h4 mb-3">Image Widget</h1>

    <!-- New widget: auto-generates data-ccms-ceid -->
    <div class="ccms-img-widget-container"
         data-editor-config="image-widget"
         data-ccms-new="true"></div>

    <!-- Scripts (order matters) -->
    <script src="/lib/filepond/filepond.min.js"></script>
    <script src="/lib/filepond-plugin-file-metadata/dist/filepond-plugin-file-metadata.min.js"></script>
    <script src="/lib/cosmos/image-widget/image-widget.js"></script>

    <script>
      // Minimal parent integration for demo purposes
      // The widget no longer requires save callbacks
      // Use CCMSImageWidgetEvents.on('imageChanged', ...) to respond to image events

      // Optional: route uploads under articles/{articleNumber}
      // window.articleNumber = 123;
    </script>
  </body>
</html>
```

## Pre-populated example

If you already have an image and want the widget to attach controls to it:

```html
<div class="ccms-img-widget-container"
     data-editor-config="image-widget"
     data-ccms-ceid="img-123">
  <img src="/pub/images/sample.jpg" class="ccms-img-widget-img" alt="Sample image" />
</div>
```

## How it works (lifecycle)

1. On page load, the script finds all `div[data-editor-config="image-widget"]`.
2. If no `<img>` is inside, it initializes a FilePond uploader.
3. During upload, it optionally sets `parent.saveInProgress = false` when complete.
4. On success, it replaces the uploader with an `<img>` and triggers the `imageChanged` event.
5. Hovering the image shows a toolbar with: Edit alt/title, Replace, Library, Delete.
6. Delete removes the image, triggers the `imageChanged` event, and returns to the upload state.

## Configuration notes

`image-widget.js` contains a `CCMS_IMAGE_WIDGET_CONFIG` constant with defaults (endpoints, file types, icons, etc.). If you need different endpoints or behavior, update this config in the script.

## Custom events (imageChanged)

The widget exposes a global event dispatcher at `window.CCMSImageWidgetEvents` that allows you to hook into image lifecycle events. This is useful for custom analytics, UI updates, or integration with other systems.

### Easiest subscription (recommended)

Use the helper function and read `info.imageSrc`.

```javascript
// Returns an unsubscribe function
const unsubscribe = window.ccmsOnImageChanged(function(info) {
  // info.imageSrc => image URL on upload/selection, '' on delete
  console.log('Current widget image URL:', info.imageSrc);
});

// Later (optional cleanup)
unsubscribe();
```

Alternative helper on the dispatcher:

```javascript
const unsubscribe = window.CCMSImageWidgetEvents.onImageChanged(function(info) {
  console.log(info.imageSrc, info.type, info.id);
});
```

### Supported event: `imageChanged`

Fired after an image is successfully uploaded or deleted.

**Event data object:**

- `type` (string): `'uploaded'` or `'deleted'`
- `id` (string): The widget's unique identifier (`data-ccms-ceid`)
- `element` (HTMLElement): The widget container DOM element
- `imageSrc` (string): The image src URL when `type === 'uploaded'`; blank (`''`) when `type === 'deleted'`

### Usage example

Register a listener in your page's `<script>` section:

```javascript
// Listen for image changes across all image widgets
window.CCMSImageWidgetEvents.on('imageChanged', function(info) {
    console.log('Image event:', info.type);
    console.log('Widget ID:', info.id);
    console.log('Container element:', info.element);
    
    if (info.type === 'uploaded') {
        console.log('New image uploaded:', info.imageSrc);
        // Custom logic: send analytics event, update UI, etc.
    } else if (info.type === 'deleted') {
        console.log('Image removed from widget', info.id);
        // Custom logic: clean up related data, update UI, etc.
    }
});

// URL-focused usage via imageChanged
window.CCMSImageWidgetEvents.on('imageChanged', function(info) {
  // info.imageSrc is either the current image URL or '' when removed
  console.log('Image URL changed:', info.imageSrc);
});
```

### Real-world integration examples

#### Example 1: Analytics tracking

```javascript
window.CCMSImageWidgetEvents.on('imageChanged', function(info) {
    if (typeof gtag !== 'undefined') {
        gtag('event', 'image_action', {
            event_category: 'image_widget',
            event_label: info.type,
            value: info.id
        });
    }
});
```

#### Example 2: Update a counter or status indicator

```javascript
window.CCMSImageWidgetEvents.on('imageChanged', function(info) {
    const counter = document.getElementById('image-count');
    if (counter) {
        const current = parseInt(counter.textContent) || 0;
        counter.textContent = info.type === 'uploaded' ? current + 1 : current - 1;
    }
});
```

#### Example 3: Sync with a custom form field

```javascript
// Optional: route uploads under articles/{articleNumber}
// window.articleNumber = 123;
// Handles the image widget events.
document.addEventListener('DOMContentLoaded', () => {
    // Handle the image changed event.
    if (window.CCMSImageWidgetEvents && typeof window.CCMSImageWidgetEvents.on === 'function') {
        window.CCMSImageWidgetEvents.on('imageChanged', function(info) {
            // Update a hidden input to track the current image URL
            const hiddenField = document.getElementById('HeroImage');
            if (hiddenField) {
                hiddenField.value = info.type === 'uploaded' ? info.imageSrc : '';
            } else {
                console.warn('Hidden field for HeroImage not found.');
            }
        });
    }
});
```

### Removing event listeners

If you need to unregister a listener (e.g., during page cleanup):

```javascript
function myHandler(info) {
    console.log('Image changed:', info.type);
}

// Register
window.CCMSImageWidgetEvents.on('imageChanged', myHandler);

// Later, unregister
window.CCMSImageWidgetEvents.off('imageChanged', myHandler);
```

**Note:** Events fire when an image is uploaded, selected from library, or deleted. The widget no longer calls `parent.saveEditorRegion` or `parent.saveChanges` - use this event system instead to respond to image lifecycle changes.

## Troubleshooting

- Icons not visible: ensure Font Awesome is loaded or replace the icon HTML in `CCMS_IMAGE_WIDGET_CONFIG`.
- Toolbar not appearing: ensure the container has `class="ccms-img-widget-container"` and that the widget found an `<img>`.
- Upload fails: verify `/FileManager/UploadImage` is reachable and returns the image URL as the response body. Check max size/type on server.
- Library empty: confirm `/FileManager/GetImageAssets?path=...` returns a JSON array of image URLs for the resolved path.
- Need to save changes: implement a listener using `window.CCMSImageWidgetEvents.on('imageChanged', ...)` to respond to upload and deletion events.
