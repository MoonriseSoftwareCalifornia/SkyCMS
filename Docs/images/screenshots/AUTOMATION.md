# Phase 2 Automation & Build System

**Build script, GitHub Actions, and tooling for automated screenshot validation and index maintenance.**

---

## Overview

Phase 2 (screenshot capture) is now fully automated. Developers submit screenshots, and:
- ✅ Status emoji updates automatically in INDEX.md
- ✅ File naming and size validated
- ✅ Missing screenshots tracked by phase
- ✅ GitHub Actions runs on every push/PR
- ✅ PR checklist prevents common mistakes

---

## Files Created

### 1. `validate-screenshots.ps1` (PowerShell Script)

**Location:** `Scripts/validate-screenshots.ps1`

**Purpose:** Automates status tracking and validation

**Features:**
- Scans `Docs/images/screenshots/` for .webp files
- Updates INDEX.md status emoji (⬜ → ✅) automatically
- Validates kebab-case filenames
- Reports file sizes (warns if > 200 KB)
- Groups missing screenshots by phase (Setup Wizard, Editor, Remaining)
- Timestamps INDEX.md ("Last Validated")
- Generates summary report

**Usage:**
```powershell
# Run locally before committing
.\Scripts\validate-screenshots.ps1

# Run with verbose output
.\Scripts\validate-screenshots.ps1 -Verbose
```

**Output Example:**
```
=== SkyCMS Screenshot Validation ===

Step 1: Scanning for existing screenshots...
  Found 0 screenshot file(s)
Step 2: Checking for missing screenshots...
  Present: 0 / 20
  Missing: 20 / 20
Step 3: Updating INDEX.md...
  ✅ INDEX.md updated
Step 4: Phase breakdown...
  [Setup Wizard] 0/7 (0%)
  [Editor & Publishing] 0/6 (0%)
  [Remaining] 0/7 (0%)
```

**Exit Codes:**
- `0` — Validation passed (all screenshots or awaiting captures)
- `1` — Validation failed (file naming/size issues)

---

### 2. `validate-screenshots.yml` (GitHub Actions Workflow)

**Location:** `.github/workflows/validate-screenshots.yml`

**Purpose:** Automated validation on every push and PR

**Triggers:**
- Push to any branch (if `Docs/images/screenshots/` changes)
- Pull request (if `Docs/images/screenshots/` or `INDEX.md` changes)

**What It Does:**
1. Checks out repository
2. Runs `validate-screenshots.ps1`
3. Verifies INDEX.md is updated when screenshots are added
4. Reports summary on workflow completion

**View Results:**
- Open PR → See "Checks" tab
- See red ❌ if validation fails
- See green ✅ if validation passes

---

### 3. `INDEX.md` (Updated Screenshot Index)

**Location:** `Docs/images/screenshots/INDEX.md`

**Key Changes:**
- **Streamlined table:** Filename | Status | Used on (with clickable links)
- **Emoji status:** ⬜ (missing) or ✅ (present)
- **Auto-updated:** Status emoji changes when files are added
- **Timestamp:** "Last Validated" shows when script last ran
- **Clickable links:** Jump directly from index to doc placement locations
- **Phase breakdown:** Shows Setup Wizard, Editor, Remaining progress
- **Reuse summary:** Shows which images are used 3x (most reused)

**Layout:**
```markdown
| Filename | Status | Used on |
| setup-wizard-welcome.webp | ⬜ | [QuickStart.md](#), [SetupWizard.md](#), [README.md](#) |
```

---

### 4. `QUICK_REFERENCE.md` (Developer Cheat Sheet)

**Location:** `Docs/images/screenshots/QUICK_REFERENCE.md`

**Purpose:** One-page guide developers print and keep open

**Contents:**
- Specs table (1440×900 px, 96 DPI, WebP, file sizes)
- Pre-capture checklist
- Naming template (kebab-case)
- Compression commands (cwebp, pngquant)
- Sensitive data map (what to hide)
- Location map (where each image goes)
- Browser viewport setup (Windows, macOS, Linux)
- Quality checklist (post-capture)
- Tool comparison table
- Troubleshooting section

---

### 5. `PR_CHECKLIST.md` (Submission Checklist)

**Location:** `Docs/images/screenshots/PR_CHECKLIST.md`

**Purpose:** Prevents common mistakes in pull requests

**Sections:**
- Pre-capture checklist (zoom, browser state)
- File submission checklist (format, size, naming)
- Before committing (run validation)
- Commit message template
- PR description template
- Common issues & fixes
- Questions/help section

---

## Workflow: How Developers Use This

### 1. **Start capturing screenshots**
```
Developer reads QUICK_REFERENCE.md
↓
Captures screenshot per specifications
↓
Saves as Docs/images/screenshots/{filename}.webp
```

### 2. **Validate before committing**
```
Developer runs: .\Scripts\validate-screenshots.ps1
↓
Script finds .webp files
↓
Script updates INDEX.md (⬜ → ✅)
↓
Developer commits both .webp and INDEX.md changes
```

### 3. **Submit PR**
```
Developer uses PR_CHECKLIST.md
↓
Opens PR with screenshots
↓
GitHub Actions automatically validates
↓
PR shows ✅ if all checks pass
```

### 4. **After merge**
```
Screenshots live in Docs
↓
INDEX.md shows ✅ status
↓
Documentation readers see the images
```

---

## Status Tracking Example

### Before Capture
```
| setup-wizard-welcome.webp | ⬜ | [QuickStart.md](#), [SetupWizard.md](#) |
| setup-wizard-storage.webp | ⬜ | [Step1-Storage.md](#) |
```

### After Developer Adds screenshot & Runs Script
```
| setup-wizard-welcome.webp | ✅ | [QuickStart.md](#), [SetupWizard.md](#) |
| setup-wizard-storage.webp | ⬜ | [Step1-Storage.md](#) |
```

---

## Timeline

- **Next week:** Developer starts capturing screenshots
- **Week 1-2:** Phase 2a (Setup Wizard: 7 images)
  - Validation script confirms captures
  - INDEX.md auto-updates to ✅
- **Week 2-3:** Phase 2b (Editor & Publishing: 6 images)
  - Reuse existing dashboard screenshot (3 docs)
  - Validation prevents duplicates
- **Week 3-4:** Phase 2c (Remaining: 7 images)
  - Final verification batch
  - All 20 ✅ complete

---

## FAQ

**Q: Does the script require manual updates?**  
A: No. Script auto-updates INDEX.md status emoji. Developer only needs to save .webp files.

**Q: What if a screenshot is too large?**  
A: Script warns if > 200 KB. Developer crops or adjusts WebP compression, reruns script.

**Q: Can I see validation output on GitHub?**  
A: Yes. Open PR → "Checks" tab → "Validate Screenshots" workflow → see full output.

**Q: What if validation fails on GitHub?**  
A: PR shows ❌. Developer fixes issue locally, runs script, commits again. Workflow reruns automatically.

**Q: Can I validate without submitting a PR?**  
A: Yes. Run `.\Scripts\validate-screenshots.ps1` locally before committing.

**Q: Does the script handle multiple captures per person?**  
A: Yes. Developer can capture multiple screenshots, save to folder, run script once (updates all at once).

**Q: What happens to old INDEX.md format?**  
A: Completely replaced. New format is simpler (3 columns vs 5), with emoji status instead of text.

---

## Setup Instructions (Already Done)

✅ Created `Scripts/validate-screenshots.ps1`
✅ Created `.github/workflows/validate-screenshots.yml`
✅ Updated `Docs/images/screenshots/INDEX.md`
✅ Created `QUICK_REFERENCE.md`
✅ Created `PR_CHECKLIST.md`

**Nothing else needed.** Developers can start capturing immediately.

---

## Support

- **How to capture?** → [QUICK_REFERENCE.md](./QUICK_REFERENCE.md)
- **Before submitting PR?** → [PR_CHECKLIST.md](./PR_CHECKLIST.md)
- **Questions about the script?** → See [validate-screenshots.ps1](../../Scripts/validate-screenshots.ps1) comments
- **GitHub Actions issues?** → See [validate-screenshots.yml](../../.github/workflows/validate-screenshots.yml)

---

## Files Reference

| File | Purpose | Developer Facing? |
|------|---------|-------------------|
| [INDEX.md](./INDEX.md) | Status tracker | ✅ Yes (shows progress) |
| [QUICK_REFERENCE.md](./QUICK_REFERENCE.md) | Capture specs | ✅ Yes (use while capturing) |
| [PR_CHECKLIST.md](./PR_CHECKLIST.md) | Submission checklist | ✅ Yes (use before PR) |
| [validate-screenshots.ps1](../../Scripts/validate-screenshots.ps1) | Validation logic | ✅ Yes (run locally) |
| [validate-screenshots.yml](../../.github/workflows/validate-screenshots.yml) | GitHub automation | ⚠️ Indirect (see results in PR) |
| [SCREENSHOT_GUIDE.md](./SCREENSHOT_GUIDE.md) | Deep dive (reference) | ✅ Yes (detailed info) |
| [QUESTIONS_AND_SUGGESTIONS.md](./QUESTIONS_AND_SUGGESTIONS.md) | Strategic Q&A | ⚠️ Archive (decisions made) |
| [README.md](./README.md) | Project overview | ⚠️ Archive (Phase 1 summary) |

---

**Ready to go!** 🚀 Developers can start Phase 2 next week with full automation in place.
