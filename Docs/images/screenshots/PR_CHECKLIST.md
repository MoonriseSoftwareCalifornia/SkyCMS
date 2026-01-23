# Screenshot Submission Checklist

**Use this checklist before submitting a Pull Request with screenshots.**

---

## Pre-Capture Checklist

- [ ] I've read [QUICK_REFERENCE.md](./QUICK_REFERENCE.md)
- [ ] Browser window is 1440×900 px (or height as needed)
- [ ] Zoom level is 100% (Ctrl+0 or Cmd+0)
- [ ] Using light mode (not dark mode)
- [ ] Page fully loaded (no spinners or progress bars)
- [ ] All popups and notifications cleared
- [ ] No sensitive data visible (emails, keys, real URLs replaced with example.com)
- [ ] Screenshot matches the feature described in [INDEX.md](./INDEX.md)

---

## File Submission Checklist

- [ ] Screenshot is saved as `.webp` format (not PNG or JPG)
- [ ] Filename matches exactly (kebab-case, case-sensitive): `Docs/images/screenshots/{filename}.webp`
- [ ] File size is between 40-150 KB
- [ ] Image is sharp and readable (no blurriness, especially text)
- [ ] Consistent with previous screenshots (same browser, theme, zoom)

---

## Before Committing

- [ ] Run validation script locally:
  ```powershell
  .\Scripts\validate-screenshots.ps1
  ```
- [ ] Script output shows ✅ for all submitted screenshots
- [ ] INDEX.md was automatically updated with ✅ status
- [ ] Git shows both the .webp file and INDEX.md as modified

---

## Commit Message Template

```
docs: add screenshots for [feature]

- Added: setup-wizard-welcome.webp
- Used in: QuickStart.md, SetupWizard.md
- Size: 85 KB
- Validation: PASSED

See Docs/images/screenshots/INDEX.md for details.
```

---

## Pull Request Description Template

```markdown
## Screenshots Added

**Phase:** [Setup Wizard / Editor & Publishing / Remaining]

**Files:**
- [ ] setup-wizard-welcome.webp (3 docs)
- [ ] setup-wizard-storage.webp (1 doc)
- [ ] ...

**Verification:**
- [x] Validation script passes
- [x] INDEX.md updated automatically
- [x] All files follow naming convention
- [x] File sizes within limits (40-150 KB)

**Quality:**
- [x] Light mode, 100% zoom
- [x] 1440×900 px resolution
- [x] No sensitive data
- [x] Text sharp and readable
```

---

## Common Issues & Fixes

| Issue | Fix |
|-------|-----|
| Validation script fails | Run `.\Scripts\validate-screenshots.ps1` again; check error messages |
| Filename doesn't match | Screenshot filename must be exactly (case-sensitive) as in INDEX.md |
| File too large (>200 KB) | Reduce WebP quality or crop image to relevant area |
| Text blurry in WebP | Use lossless compression: `cwebp -lossless -q 95 screenshot.png -o screenshot.webp` |
| INDEX.md wasn't updated | Run validation script; it updates INDEX.md automatically |
| Image won't display in markdown | Verify path is `../images/screenshots/filename.webp` (case-sensitive) |

---

## Questions?

- **Capture tools?** → See [SCREENSHOT_GUIDE.md](./SCREENSHOT_GUIDE.md)
- **Specs (dimensions, DPI)?** → See [QUICK_REFERENCE.md](./QUICK_REFERENCE.md)
- **Where does this go?** → Find filename in [INDEX.md](./INDEX.md); click "Used on" links
- **Validation failed?** → Run script with `-Verbose` flag: `.\Scripts\validate-screenshots.ps1 -Verbose`

---

## After PR Merge

- ✅ Screenshot now appears in published documentation
- ✅ STATUS emoji in INDEX.md is ✅
- ✅ Users can see the screenshot when they read that doc section

Thank you for contributing to SkyCMS documentation! 📸
