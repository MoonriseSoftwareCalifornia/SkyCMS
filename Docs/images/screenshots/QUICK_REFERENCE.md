# Screenshot Quick Reference Card

**Print this or keep it open while capturing screenshots.**

---

## One-Page Specs

| Aspect | Value | Why |
|--------|-------|-----|
| **Width** | 1440 px | Readable, web-standard, scales well |
| **Height** | Flexible (900+ px) | Allow tall content; don't letterbox |
| **Aspect Ratio** | 16:9 | Natural browser window size |
| **DPI** | 96 | Web standard; ignore DPI in dialog |
| **Format** | WebP | 30-40% smaller than PNG, modern standard |
| **Fallback** | PNG | Only if WebP shows blurriness |
| **Compression** | Lossless | Sharp text, good for UI (60-100 KB target) |
| **Zoom** | 100% | No magnification; Ctrl+0 to reset |
| **Browser** | Chrome | Consistent, recognizable rendering |
| **OS Theme** | Light mode | Best readability in docs |
| **File Size** | 40-150 KB | Keep repo size reasonable |

---

## Pre-Capture Checklist

Before hitting "capture":

- [ ] Browser window set to 1440×900 px
- [ ] Zoom at 100% (Ctrl+0)
- [ ] Page fully loaded (no spinners)
- [ ] Clear any notifications/popups
- [ ] No sensitive data visible
- [ ] Use example values (admin@example.com, https://www.example.com)
- [ ] Consistent with previous screenshots (same theme, browser)

---

## Naming Template

```
{feature}-{section}-{action}.webp
```

**Examples:**
- ✅ `setup-wizard-storage.webp`
- ✅ `live-editor-toolbar.webp`
- ✅ `page-scheduling-calendar.webp`
- ❌ `setup_storage_step1.png` (underscores, PNG)
- ❌ `screenshot.webp` (too generic)

---

## File Size Targets

| Screenshot Type | Target Size |
|---|---|
| Small (dialog, button detail) | 20-40 KB |
| Medium (full form, small page) | 60-100 KB |
| Large (full page, dashboard) | 100-150 KB |

**Over limit?** Crop to relevant area or increase WebP compression.

---

## Compression Commands

### Convert PNG to WebP (Lossless)
```bash
cwebp -lossless -q 95 screenshot.png -o screenshot.webp
```

### Convert PNG to WebP (Lossy)
```bash
cwebp -q 85 screenshot.png -o screenshot.webp
```

### Compress PNG
```bash
pngquant --quality=85-95 screenshot.png
optipng -o7 screenshot.png
```

---

## Sensitive Data to Hide

| Replace | With |
|---------|------|
| Real email | `admin@example.com`, `user@yoursite.com` |
| Real domain | `https://www.example.com`, `mysite.azurewebsites.net` |
| API key | `••••••••`, `[hidden]` |
| Real name | "John Smith", "Test User" |
| Real phone | `555-1234` |
| Real address | "123 Main St, City, State" |

---

## Screenshot Location Map

### Setup Wizard Series (7 images)
1. Welcome → [Docs/QuickStart.md](../../QuickStart.md)
2. Storage → [Docs/Installation/SetupWizard-Step1-Storage.md](../../Installation/SetupWizard-Step1-Storage.md)
3. Admin → [Docs/Installation/SetupWizard-Step2-Admin.md](../../Installation/SetupWizard-Step2-Admin.md)
4. Publisher → [Docs/Installation/SetupWizard-Step3-Publisher.md](../../Installation/SetupWizard-Step3-Publisher.md)
5. Email → [Docs/Installation/SetupWizard-Step4-Email.md](../../Installation/SetupWizard-Step4-Email.md)
6. CDN → [Docs/Installation/SetupWizard-Step5-CDN.md](../../Installation/SetupWizard-Step5-CDN.md)
7. Review → [Docs/Installation/SetupWizard-Step6-Review.md](../../Installation/SetupWizard-Step6-Review.md)

### Live Editor Series (3 images)
1. Dashboard → [Docs/Editors/LiveEditor/QuickStart.md](../../Editors/LiveEditor/QuickStart.md)
2. Toolbar → [Docs/Editors/LiveEditor/VisualGuide.md](../../Editors/LiveEditor/VisualGuide.md)
3. Insert Image → [Docs/Editors/LiveEditor/QuickStart.md](../../Editors/LiveEditor/QuickStart.md)

**Full list:** See [INDEX.md](./INDEX.md)

---

## Workflow (Per Screenshot)

1. **Navigate** to the feature in SkyCMS
2. **Prepare** (full page load, clear popups, ensure 1440×900 px viewport)
3. **Capture** (screenshot tool or DevTools → Copy to clipboard)
4. **Compress** (convert to WebP if PNG, run compression)
5. **Verify** (check file size, open in image viewer, confirm quality)
6. **Save** (to `Docs/images/screenshots/{filename}.webp`)
7. **Test** (render markdown locally, verify image displays)
8. **Track** (update INDEX.md status to ✅)

**Time per image:** 3-5 minutes  
**Total for 16 unique images:** ~1 hour

---

## Browser Viewport Setup

### Windows (Chrome/Edge)
1. Press `F12` (DevTools)
2. Press `Ctrl+Shift+M` (Device Toolbar)
3. Set to 1440 × 900
4. Zoom: Ctrl+0
5. Toggle Device Toolbar off (`Ctrl+Shift+M`) to see browser UI
6. Screenshot

### macOS (Safari)
1. `Develop → Enter Responsive Design Mode`
2. Set to 1440 × 900
3. Zoom: Cmd+0
4. Exit Responsive Mode
5. Cmd+Shift+4 (screenshot tool)

### Linux (Chrome/Firefox)
1. Ctrl+Shift+M (Responsive Design Mode)
2. Set to 1440 × 900
3. Ctrl+0 (reset zoom)
4. Exit responsive mode
5. Print Screen or `gnome-screenshot`

---

## Quality Checklist (Post-Capture)

After you've taken and compressed the screenshot:

- [ ] Image is 1440 px wide
- [ ] File size is under 150 KB
- [ ] Text is sharp and readable (no blurriness)
- [ ] Colors are consistent with previous screenshots
- [ ] No sensitive data visible
- [ ] Filename follows kebab-case naming
- [ ] Markdown link matches filename exactly (case-sensitive)
- [ ] Image renders correctly in markdown preview
- [ ] Alt text describes the screenshot

---

## Common Issues & Fixes

| Problem | Fix |
|---------|-----|
| Text blurry in WebP | Use lossless: `cwebp -lossless -q 95` |
| File too large | Crop to relevant area; increase compression `-q 75` |
| Zoom looks off | Reset to 100%: Ctrl+0 or Cmd+0 |
| Colors different | Ensure sRGB color space; export without ICC profile |
| Link not working | Check filename matches exactly (case-sensitive) |
| Image won't display | Verify path: `../images/screenshots/filename.webp` |

---

## Tools Comparison

| Tool | Cost | Platforms | Best For | Rating |
|---|---|---|---|---|
| Snagit | $49.99 | Win, Mac | Quality, speed, built-in editor | ⭐⭐⭐⭐⭐ |
| GIMP | Free | Win, Mac, Linux | Full control, format conversion | ⭐⭐⭐⭐ |
| Chrome DevTools | Free | All | Browser UI shots, auto-scroll | ⭐⭐⭐ |
| Windows Snipping Tool | Free (OS) | Windows | Quick region capture | ⭐⭐⭐ |
| macOS Screenshot | Free (OS) | macOS | Native, simple | ⭐⭐⭐ |
| ImageMagick | Free (CLI) | All | Batch automation, compression | ⭐⭐⭐ |

---

## Links

- **[INDEX.md](./INDEX.md)** — Full index with clickable placement links
- **[SCREENSHOT_GUIDE.md](./SCREENSHOT_GUIDE.md)** — Complete technical guide
- **[QUESTIONS_AND_SUGGESTIONS.md](./QUESTIONS_AND_SUGGESTIONS.md)** — Strategic Q&A
- **[README.md](./README.md)** — Project overview

---

## Need Help?

- **Blurry text?** → See Troubleshooting in SCREENSHOT_GUIDE.md
- **Where does this image go?** → Open INDEX.md and search for the filename
- **Should I use PNG or WebP?** → Use WebP (unless text is blurry, then PNG)
- **File too large?** → Crop the image or increase WebP compression
- **How do I compress?** → See Compression Commands above

---

**Good luck! 📸** Follow the guide, and you'll have professional, consistent screenshots that improve docs readability.
