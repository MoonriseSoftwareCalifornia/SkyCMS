# Screenshot Capture & Creation Guide

> **For**: Anyone creating screenshots to fill the placeholders in SkyCMS documentation.
> **Purpose**: Ensure consistent, professional, and optimized screenshots across all docs.
> **Last Updated**: 2026-01-23

---

## Table of Contents

1. [Quick Reference](#quick-reference)
2. [Dimensions & Resolution](#dimensions--resolution)
3. [Format & Compression](#format--compression)
4. [Capture Tools & Techniques](#capture-tools--techniques)
5. [Style & Consistency Guidelines](#style--consistency-guidelines)
6. [Annotation Standards](#annotation-standards)
7. [File Naming & Organization](#file-naming--organization)
8. [Workflow Checklist](#workflow-checklist)
9. [Troubleshooting](#troubleshooting)

---

## Quick Reference

| Specification | Recommendation | Notes |
|---|---|---|
| **Resolution** | 1440×900 or 1600×1000 px | See [Dimensions](#dimensions--resolution) for details |
| **Aspect Ratio** | 16:9 widescreen | Natural browser window size |
| **DPI (capture)** | 96 DPI | Standard web DPI; avoid oversampling |
| **Format** | WebP (primary) | .webp extension; ~60-80KB target |
| **Fallback Format** | PNG (if WebP insufficient) | .png extension; only if text is blurry in WebP |
| **Color Space** | sRGB | Standard for web; matches browser rendering |
| **UI Chrome** | Include window frame & top bar | Shows context; users see this in reality |
| **Zoom Level** | 100% browser zoom | No magnification or scaling |
| **Annotations** | Plain capture (no arrows/numbers) | Clarity from UI alone; annotations rarely age well |
| **Sensitive Data** | Use placeholder/demo values only | Hide real emails, API keys, tokens, URLs |
| **Consistency** | Same OS, browser theme, system font size | All screenshots should look "from the same session" |

---

## Dimensions & Resolution {#dimensions--resolution}

### Recommended Capture Size

**Primary recommendation: 1440×900 pixels**

**Why this size?**
- Wide enough to show full UI context (nav, content, sidebar)
- Tall enough for multi-section screenshots (forms, lists, dialogs)
- 16:9 aspect ratio matches modern browsers and displays
- Scales beautifully down to ~1000px width in markdown (still readable)
- Performance-friendly when compressed to WebP

**Alternative sizes** (context-dependent):

| Size | Use Case | Notes |
|---|---|---|
| 1280×800 | Compact dialogs, mobile mockups | Slightly less width; good for modal-only captures |
| 1600×1000 | Full-page flows, complex dashboards | More horizontal space; larger file size |
| 1920×1200 | High-fidelity detail shots (if needed) | Risk of readers needing to scroll; use sparingly |
| 1024×768 | Legacy reference only | Too narrow for modern UI; avoid |

### DPI Considerations

**For web documentation:**
- **96 DPI is standard** (web resolution)
- **Do NOT use 300 DPI** (print resolution) — wastes file size with no visible benefit
- **Do NOT upscale** — capture at 100% zoom, then let markdown/CSS scale for display

**Why DPI doesn't matter for web:**
- Browsers ignore DPI metadata in images
- Only pixel dimensions matter for web
- 96 DPI is baked into web standards and CSS

### Browser Viewport

**Set your browser to these dimensions before capturing:**

**Windows (Chrome/Edge):**
1. Press `F12` (DevTools)
2. Press `Ctrl+Shift+M` (Device Toolbar)
3. Set dimensions to **1440 × 900**
4. Ensure zoom is **100%** (Ctrl+0 to reset)
5. Toggle Device Toolbar off when ready to capture
6. Use PrintScreen or capture tool

**macOS (Chrome/Safari):**
1. Safari: `Safari → Preferences → Advanced → Show Develop Menu`
2. `Develop → Enter Responsive Design Mode`
3. Set to 1440 × 900
4. Zoom at 100% (Cmd+0)
5. Screenshot (Cmd+Shift+4 for selection, then drag)

**Linux (Chrome/Firefox):**
1. Firefox: `Ctrl+Shift+M` (Responsive Design Mode)
2. Chrome: `Ctrl+Shift+M` (Device Toolbar)
3. Set 1440 × 900
4. Use `gnome-screenshot` or Print Screen

---

## Format & Compression {#format--compression}

### WebP (Recommended)

**Advantages:**
- **~30-40% smaller** than PNG for UI screenshots
- **Lossless or lossy** options (use lossless for UI)
- **Native browser support** (all modern browsers)
- **Modern standard** for web documentation

**Disadvantages:**
- Older browsers may not support (but documented sites assume modern browsers)
- Requires conversion tool (see [Tools](#capture-tools--techniques))

**File Size Target:**
- **Single dialog/small area**: 20-40 KB
- **Full-page screenshot**: 60-100 KB
- **Complex dashboard**: up to 150 KB

**Compression settings** (if using ImageMagick or similar):
```bash
# WebP with good quality
cwebp -lossless -q 95 screenshot.png -o screenshot.webp

# Or with lossy (slightly smaller, imperceptible quality loss)
cwebp -q 85 screenshot.png -o screenshot.webp
```

### PNG (Fallback)

**Use PNG only if:**
- WebP produces visible text blurriness (rare for UI)
- Capturing complex gradients or anti-aliased elements that WebP struggles with
- You need maximum compatibility (very old browsers)

**File Size Target:**
- Typically 2-3× larger than WebP
- 150-300 KB for full page

**Compression:**
```bash
# Lossless compression
pngquant --force --ext .png --quality=85-95 screenshot.png

# Or with ImageMagick
convert screenshot.png -strip screenshot.png  # Remove metadata
```

### Avoid JPEG

- **Not recommended** for UI screenshots
- Compression artifacts around text and buttons
- No transparency support
- Larger file than WebP for equivalent quality

### File Size Optimization

Before uploading to repo:

```bash
# WebP: already optimized via cwebp above

# PNG: use pngquant or optipng
optipng -o7 screenshot.png

# Check file sizes
ls -lh screenshot.webp screenshot.png
```

**Target**: Keep repo files under **500 KB total** for all screenshots initially.

---

## Capture Tools & Techniques {#capture-tools--techniques}

### Recommended Tools

#### **Snagit** (Best all-around)
- **Cost**: $49.99 (one-time)
- **Platforms**: Windows, macOS
- **Features**: 
  - One-click capture, region select, full window
  - Built-in editor (no post-processing needed)
  - Automatic format selection
  - Auto-scroll for long pages
- **For SkyCMS**: Excellent for setup wizard steps, form fills

#### **Screenshot Path** (macOS native, free)
- **Platforms**: macOS only
- **How**: Press `Cmd+Shift+5`, select region, then crop/edit
- **Features**: Native macOS integration, can save to clipboard
- **Limitation**: No auto-scroll; manual for tall pages

#### **Windows Snipping Tool** (Windows native, free)
- **Platforms**: Windows 10+
- **How**: Windows key + Shift + S, select region
- **Features**: Rectangular, freeform, window capture modes
- **Limitation**: No auto-scroll for tall pages

#### **GIMP** (Free, powerful)
- **Cost**: Free (open-source)
- **Platforms**: Windows, macOS, Linux
- **Features**:
  - Capture → Screenshot (can capture windows/regions)
  - Export as WebP with compression controls
  - Edit and annotate if needed
- **Workflow**: 
  1. File → Create → Screenshot
  2. Select region
  3. File → Export As → filename.webp

#### **ImageMagick** (CLI tool, free)
- **Platforms**: Windows, macOS, Linux
- **Cost**: Free
- **For automation**:
  ```bash
  # Capture region after 3-second delay
  import -pause 3 screenshot.png
  
  # Convert to WebP
  cwebp -lossless screenshot.png -o screenshot.webp
  ```

### Browser-Based Capture

**Built-in DevTools (all browsers):**
- **Chrome/Edge**: Press `Ctrl+Shift+P`, type "Capture screenshot", select "Capture full page size"
- **Firefox**: Right-click → Take Screenshot → Full Page
- **Safari**: Develop → Take Page Screenshot
- **Limitation**: Loses some UI chrome; good for content-only shots

### Step-by-Step Capture Workflow

1. **Prepare the browser**
   - Set window to 1440×900 px (see [Browser Viewport](#browser-viewport))
   - Zoom to 100% (`Ctrl+0`)
   - Navigate to the target page/section
   - Clear any toast notifications, tooltips
   - Ensure no private/sensitive info visible

2. **Take the screenshot**
   - Use your chosen tool (Snagit, Snipping Tool, GIMP, etc.)
   - Capture the entire window including frame (unless specified otherwise)
   - If capturing dialog: include some background context
   - Check for quality issues before saving

3. **Compress & convert**
   - Export as WebP (or PNG fallback)
   - Use compression settings from [Format & Compression](#format--compression)
   - Verify file size is reasonable (~40-100 KB)

4. **Save with proper naming**
   - See [File Naming & Organization](#file-naming--organization)
   - Place in `Docs/images/screenshots/` folder

5. **Update documentation**
   - Verify markdown link matches filename exactly (case-sensitive)
   - Test that link works and image displays correctly

---

## Style & Consistency Guidelines {#style--consistency-guidelines}

### Visual Consistency

**All screenshots should:**

1. **Use the same browser** across all captures
   - Recommend: Google Chrome (latest stable)
   - Rationale: Most consistent rendering; users recognize the UI

2. **Use the same OS theme**
   - All Windows, all macOS, or all Linux
   - Pick one; don't mix
   - Recommendation: **Windows 11 Light theme** (most universal)

3. **Same system font size**
   - Set OS to 100% scaling (default)
   - Do not use "Large text" or magnified settings
   - Ensures consistency across all captures

4. **Same app theme** (if SkyCMS has theme options)
   - Pick Light or Dark theme; apply consistently
   - If app defaults to system theme, stick with Light for clarity

5. **Hide chrome where it doesn't matter**
   - For dialogs in modal overlays: OK to crop out background
   - For full-page flows: Include window frame for context
   - For mobile views: Include phone bezel/frame

### What to Capture

**Optimal scope:**
- **Small feature** (button, field): Just the element + surrounding context
- **Form/dialog**: Entire modal window
- **Page section**: Full width, one logical section
- **Full page workflow**: Capture in steps (1-3 screenshots per step)

**Example scopes:**

| Example | Width | Include |
|---|---|---|
| Setup wizard step | 1440 px | Full form with buttons |
| Dashboard panel | 1440 px | Nav + content area |
| File upload dialog | 900 px | Modal + title + buttons |
| Button detail | 600 px | Just the button + label |

### What to Hide/Blur

**Always hide:**
- ❌ Real email addresses
- ❌ API keys, tokens, secrets
- ❌ Real user names / personal data
- ❌ Real URLs (use example.com)
- ❌ Real company names (use "Acme Corp")
- ❌ Real phone numbers, addresses

**Replace with:**
- Email: `admin@example.com`, `user@yoursite.com`
- URLs: `https://www.example.com`, `https://mysite.azurewebsites.net`
- Names: "John Smith", "Sample User", "Test Account"
- API Keys: `••••••••` or `[hidden]`

---

## Annotation Standards

### Recommendation: Keep Screenshots Plain

**Why minimal annotations?**
- UI clarity should be self-evident (good design)
- Annotations clutter images and distract from content
- Numbered callouts rarely age well (docs evolve, annotations become outdated)
- Alt text and caption text in markdown are better for accessibility

### When Annotations ARE Helpful

**Use annotations sparingly only for:**
1. **Highlighting a single button/field** that users might miss
2. **Showing interaction flow** (arrow from A → B)
3. **Pointing out critical warnings** (red boxes around errors)

**If you must annotate:**
- Use subtle, semi-transparent overlays
- Keep text minimal: "Click here", "Sample output", etc.
- Use contrasting colors (white text on dark, dark on light)
- Font: Clean sans-serif (Arial, Helvetica, or system font)
- Font size: 12-14pt (readable but not oversized)

### Alt Text & Captions

**Better approach**: Use markdown alt text and captions

```markdown
![Setup wizard storage step showing input fields](../images/screenshots/setup-wizard-storage.webp)

**Fig 1**: The storage configuration step allows you to enter your S3 bucket details.
```

**Benefits:**
- Accessible to screen readers
- Explains context without cluttering image
- Easy to update if image changes

---

## File Naming & Organization {#file-naming--organization}

### Folder Structure

```
Docs/
└── images/
    └── screenshots/
        ├── INDEX.md                           (Updated mapping)
        ├── SCREENSHOT_GUIDE.md                (This file)
        ├── setup-wizard-welcome.webp
        ├── setup-wizard-storage.webp
        ├── page-scheduling-review-dialog.webp
        └── ...
```

### Naming Convention

**Format**: `{feature}-{section}-{action}.webp`

**Rules:**
- Lowercase letters only
- Hyphens to separate words (no underscores, spaces)
- Descriptive but concise (max ~40 chars)
- Number sequences rarely; prefer descriptive names

**Examples:**

| Good | Poor | Why |
|---|---|---|
| `setup-wizard-storage.webp` | `setup_storage_screen.png` | Matches doc references; WebP format |
| `live-editor-editing-toolbar.webp` | `editor-toolbar-1.webp` | Descriptive; indicates purpose |
| `page-scheduling-calendar.webp` | `scheduler_calendar_datetime_picker.webp` | Specific; not redundant |
| `publishing-modes-overview.webp` | `publishing.webp` | Clear what section of publishing |

**Reused across docs?** Keep the **same filename** (don't create variants like `setup-wizard-storage-v2.webp`).

---

## Workflow Checklist

### Before Capturing

- [ ] Browser set to 1440×900 px
- [ ] Zoom at 100% (press Ctrl+0)
- [ ] Page fully loaded (no spinners, progress bars)
- [ ] Clear any notifications/toasts from screen
- [ ] No modal dialogs or tooltips visible (unless intentional)
- [ ] Hide sensitive data (use placeholder values)
- [ ] Consistent with previous screenshots (same theme, OS, browser)

### During Capture

- [ ] Entire window included (title bar + content)
- [ ] Focus on the relevant feature/section
- [ ] No accidental overlaps (other windows, taskbar)
- [ ] Region properly framed (not cut off at edges)

### After Capture

- [ ] Review image for clarity (text readable, colors correct)
- [ ] Compress to WebP (target 40-100 KB)
- [ ] Verify file size is reasonable
- [ ] Name file according to [Naming Convention](#naming-convention)
- [ ] Place in `Docs/images/screenshots/` folder
- [ ] Update markdown link in corresponding doc (match filename exactly)
- [ ] Test link in markdown preview (image displays correctly)

### Before Submitting / Uploading

- [ ] All filenames follow naming convention (lowercase, hyphens)
- [ ] All files are `.webp` (or `.png` fallback with justification)
- [ ] No sensitive data in any screenshot
- [ ] Image dimensions are ~1440 px wide
- [ ] File sizes all under 150 KB (except complex dashboards up to 200 KB)
- [ ] Markdown links are correct (case-sensitive, match filenames exactly)
- [ ] Alt text describes the screenshot
- [ ] Screenshots are consistent in style/theme

---

## Troubleshooting

### Text Appears Blurry in WebP

**Cause**: WebP lossy compression too aggressive on text

**Solution:**
1. Recapture at higher resolution (1600 px instead of 1440)
2. Try lossless WebP: `cwebp -lossless -q 95 screenshot.png -o screenshot.webp`
3. If still blurry, use PNG instead (fallback)

**Prevent:** Use lossless compression for UI screenshots (see [Compression settings](#webp-recommended))

### File Size Too Large (>200 KB)

**Cause**: Over-compression quality or image too large

**Solutions:**
1. Reduce image dimensions (crop to relevant area)
2. Increase compression: `cwebp -q 75 screenshot.png -o screenshot.webp` (lossy)
3. Remove metadata: `convert screenshot.png -strip screenshot.png`
4. For PNG: `pngquant --quality=65-80 screenshot.png`

### Image Won't Convert to WebP

**Cause**: Tool not installed or format issue

**Solutions:**
1. Install libwebp:
   - **macOS**: `brew install webp`
   - **Windows**: [Download installer](https://developers.google.com/speed/webp/download)
   - **Linux**: `sudo apt-get install webp`
2. Verify file format: `file screenshot.png`
3. Try converting via GIMP (File → Export As → .webp)

### Link in Markdown Not Working

**Cause**: Filename mismatch or path incorrect

**Solutions:**
1. Verify filename is exact (case-sensitive: `setup-wizard-storage.webp` ≠ `Setup-Wizard-Storage.webp`)
2. Verify path matches directory structure: `../images/screenshots/filename.webp`
3. Test by opening file directly in browser

### Screenshot Colors Look Different Than Browser

**Cause**: Color space mismatch (sRGB vs other)

**Solution:**
1. Export as sRGB (GIMP: Image → Mode → sRGB)
2. Avoid ICC profiles: `convert screenshot.png -strip screenshot.png`
3. Recapture with sRGB-aware tool

### Zoom/Scale Looks Different on Windows vs macOS

**Cause**: System DPI scaling differs between OS

**Solution:**
1. Capture on single OS only (don't mix Windows and macOS)
2. Set OS scaling to 100% (standard)
3. Use same browser for all captures

---

## Questions & Support

**Have questions about these guidelines?**

Reference the **[Screenshot Index](./INDEX.md)** for which docs need what screenshots, or update this guide if you discover new best practices.

**Estimated time per screenshot:** 3-5 minutes (capture + compress + verify)  
**Total effort estimate for 16 screenshots:** 1-1.5 hours

---

## Summary

| Aspect | Specification |
|---|---|
| **Resolution** | 1440×900 px (16:9) |
| **Format** | WebP (primary), PNG (fallback) |
| **DPI** | 96 (web standard) |
| **Zoom** | 100% |
| **Compression** | Lossless WebP or high-quality PNG |
| **File Size** | 40-150 KB per image |
| **Naming** | kebab-case (lowercase, hyphens) |
| **Browser** | Chrome (consistent across all captures) |
| **Theme** | Light mode (Windows 11 or equivalent) |
| **Annotations** | Minimal; prefer markdown alt text |
| **Sensitive Data** | Hidden/replaced with placeholders |

**Ready to start?** Pick 2-3 screenshots from the [Screenshot Index](./INDEX.md), follow this guide, and submit. You'll have a feel for the workflow after the first few.
