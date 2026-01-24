# Screenshot Index

**Status:** Phase 3 expansion — 26 total screenshots planned  
**Last Validated:** Jan 24, 2026 (Phase 3 added)

This index tracks all 26 screenshot placeholders across 19 documentation files. The status emoji updates automatically when files are added.

---

## How to Use This Index

1. **Find a screenshot:** Search by filename or feature
2. **See where it's used:** Click links under "Used on" to jump to exact locations in docs
3. **Track progress:** Status emoji shows ⬜ (missing) or ✅ (present)
4. **For developers:** Follow [QUICK_REFERENCE.md](./QUICK_REFERENCE.md), capture screenshot, save to this folder, and run validation script
5. **For managers:** Scan this index to track Phase 2 progress at a glance

---

## Screenshot Placeholders (26 Total)

### Phase 2: Core User Workflows (20 screenshots)

| Filename | Status | Used on |
|----------|--------|---------|
| `setup-wizard-welcome.webp` | ⬜ | [QuickStart.md](../../QuickStart.md#setup-wizard-welcome), [SetupWizard.md](../../Installation/SetupWizard.md#setup-wizard-welcome), [Installation/README.md](../../Installation/README.md#setup-wizard-welcome) |
| `setup-wizard-storage.webp` | ⬜ | [SetupWizard-Step1-Storage.md](../../Installation/SetupWizard-Step1-Storage.md#setup-wizard-storage) |
| `setup-wizard-admin.webp` | ⬜ | [SetupWizard-Step2-Admin.md](../../Installation/SetupWizard-Step2-Admin.md#setup-wizard-admin) |
| `setup-wizard-publisher.webp` | ⬜ | [SetupWizard-Step3-Publisher.md](../../Installation/SetupWizard-Step3-Publisher.md#setup-wizard-publisher) |
| `setup-wizard-email.webp` | ⬜ | [SetupWizard-Step4-Email.md](../../Installation/SetupWizard-Step4-Email.md#setup-wizard-email) |
| `setup-wizard-cdn.webp` | ⬜ | [SetupWizard-Step5-CDN.md](../../Installation/SetupWizard-Step5-CDN.md#setup-wizard-cdn) |
| `setup-wizard-review.webp` | ⬜ | [SetupWizard.md](../../Installation/SetupWizard.md#setup-wizard-review), [QuickStart.md](../../QuickStart.md#setup-wizard-review), [Installation/README.md](../../Installation/README.md#setup-wizard-review) |
| `live-editor-dashboard.webp` | ⬜ | [LiveEditor/QuickStart.md](../../Editors/LiveEditor/QuickStart.md#live-editor-dashboard), [LiveEditor/VisualGuide.md](../../Editors/LiveEditor/VisualGuide.md#live-editor-dashboard), [Post-Installation.md](../../Installation/Post-Installation.md#live-editor-dashboard) |
| `live-editor-editing-toolbar.webp` | ⬜ | [LiveEditor/QuickStart.md](../../Editors/LiveEditor/QuickStart.md#live-editor-editing-toolbar), [LiveEditor/VisualGuide.md](../../Editors/LiveEditor/VisualGuide.md#live-editor-editing-toolbar) |
| `live-editor-insert-image.webp` | ⬜ | [LiveEditor/QuickStart.md](../../Editors/LiveEditor/QuickStart.md#live-editor-insert-image), [LiveEditor/VisualGuide.md](../../Editors/LiveEditor/VisualGuide.md#live-editor-insert-image) |
| `publishing-modes-overview.webp` | ⬜ | [Publishing-Overview.md](../../Publishing-Overview.md#publishing-modes-overview) |
| `publishing-mode-selector.webp` | ⬜ | [Publishing-Overview.md](../../Publishing-Overview.md#publishing-mode-selector) |
| `publishing-staged-approval.webp` | ⬜ | [Publishing-Overview.md](../../Publishing-Overview.md#publishing-staged-approval) |
| `page-scheduling-review-dialog.webp` | ⬜ | [PageScheduling.md](../../Editors/PageScheduling.md#page-scheduling-review-dialog) |
| `page-scheduling-calendar.webp` | ⬜ | [PageScheduling.md](../../Editors/PageScheduling.md#page-scheduling-calendar) |
| `page-scheduler-dashboard.webp` | ⬜ | [PageScheduling.md](../../Editors/PageScheduling.md#page-scheduler-dashboard) |
| `storage-upload-test.webp` | ⬜ | [Post-Installation.md](../../Installation/Post-Installation.md#storage-upload-test) |
| `settings-email-test.webp` | ⬜ | [Post-Installation.md](../../Installation/Post-Installation.md#settings-email-test) |
| `settings-cdn-test.webp` | ⬜ | [Post-Installation.md](../../Installation/Post-Installation.md#settings-cdn-test) |
| `multi-tenant-architecture.webp` | ⬜ | [MinimumRequiredSettings.md](../../Installation/MinimumRequiredSettings.md#multi-tenant-architecture) |

### Phase 3: Decision-Making & Architecture (6 screenshots)

| Filename | Status | Used on |
|----------|--------|---------|
| `comparison-feature-matrix.webp` | ⬜ | [Comparisons.md](../../Comparisons.md#comparison-feature-matrix) |
| `edge-native-architecture-diagram.webp` | ⬜ | [When-to-Use-SkyCMS.md](../../When-to-Use-SkyCMS.md#edge-native-architecture-diagram) |
| `publishing-workflow-comparison.webp` | ⬜ | [Comparisons.md](../../Comparisons.md#publishing-workflow-comparison) |
| `quick-reference-workflow-diagram.webp` | ⬜ | [Quick-Reference.md](../../Quick-Reference.md#quick-reference-workflow-diagram) |
| `developer-experience-comparison-setup.webp` | ⬜ | [Developer-Experience-Comparison.md](../../Developer-Experience-Comparison.md#developer-experience-comparison-setup) |
| `cosmosvsheadless-architecture-modes.webp` | ⬜ | [CosmosVsHeadless.md](../../CosmosVsHeadless.md#cosmosvsheadless-architecture-modes) |

---

## Reuse Summary

| Filename | Used On (Count) |
|----------|-----------------|
| `setup-wizard-welcome.webp` | 3 docs |
| `setup-wizard-review.webp` | 3 docs |
| `live-editor-dashboard.webp` | 3 docs |
| All others | 1 doc each |

**Efficiency:** 26 placements across 26 unique images (Phase 2: 20%, Phase 3: 0% reuse)

---

## Status Legend

- ⬜ **Missing** — Placeholder in place; screenshot file not yet added
- ✅ **Present** — Screenshot file detected; rendering in docs

---

## Quick Start for Developers

1. **Choose a screenshot** from the table above
2. **Follow [QUICK_REFERENCE.md](./QUICK_REFERENCE.md)** for capture specs (1440×900 px, 96 DPI, WebP, light mode)
3. **Capture and compress** the screenshot using tools from the guide
4. **Save file** to: `Docs/images/screenshots/{filename}.webp` (must match filename exactly)
5. **Run validation:** `.\Scripts\validate-screenshots.ps1`
6. **Commit and submit PR** — script auto-updates status to ✅ when file is detected

---

## Automated Validation

The `validate-screenshots.ps1` script (in `Scripts/` folder):
- ✅ Scans this folder for .webp files
- ✅ Updates status emoji automatically (⬜ → ✅)
- ✅ Validates filenames follow kebab-case
- ✅ Reports file sizes and compression efficiency
- ✅ Lists missing images by Phase

**Run before committing:**
```powershell
.\Scripts\validate-screenshots.ps1
```

**Runs automatically on:**
- GitHub Actions (every push)
- Local pre-commit hook (optional setup)



