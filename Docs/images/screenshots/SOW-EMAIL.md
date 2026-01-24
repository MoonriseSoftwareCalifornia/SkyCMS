# Scope of Work: SkyCMS Documentation Screenshots (Phase 2)

**Objective:** Capture and deliver 20 documentation screenshots (16 unique images) for end-user docs, using existing specs and automation so the index updates automatically.

## 1) Environment & Setup
- Clone repo: `git clone <repo-url>` → `cd SkyCMS`
- Create branch: `git checkout -b docs/screenshots-phase2`
- Open in VS Code: `code .` (PowerShell available for scripts)
- Locate guides in VS Code:
  - Docs/images/screenshots/00_START_HERE.md
  - Docs/images/screenshots/QUICK_REFERENCE.md
  - Docs/images/screenshots/INDEX.md
  - Docs/images/screenshots/PR_CHECKLIST.md
- When done:
  - Run validation: `.\\Scripts\\validate-screenshots.ps1`
  - Commit `.webp` + updated `INDEX.md`
  - Push branch: `git push -u origin docs/screenshots-phase2`
  - Open PR to main (GitHub Actions will validate)

## 2) Inputs (must read before starting)
- 00_START_HERE.md, QUICK_REFERENCE.md, INDEX.md ("Used on" links), PR_CHECKLIST.md
- Optional references: SCREENSHOT_GUIDE.md, AUTOMATION.md

## 3) Deliverables
- 20 WebP files in `Docs/images/screenshots/` with exact filenames from INDEX.md.
- Updated INDEX.md (auto-updated by validation script to show ✅ where files exist).
- A passing validation run (local + GitHub Actions) and a PR with changes.

## 4) Technical Requirements
- Format: WebP (lossless preferred), target 40–150 KB (warn if >200 KB).
- Dimensions: 1440×900 px (height may grow for tall content), 96 DPI, 100% zoom, light mode.
- Naming: Exact filenames from INDEX.md, kebab-case, case-sensitive.
- Path: `Docs/images/screenshots/{filename}.webp`.
- No sensitive data: use example.com, placeholder creds, mask keys.

## 5) Process
1. Read 00_START_HERE and QUICK_REFERENCE.
2. For each filename, open INDEX.md and click "Used on" to see placement context.
3. Capture per specs; hide sensitive data.
4. Save WebP with the exact filename.
5. Run validation: `.\\Scripts\\validate-screenshots.ps1` (auto-updates INDEX.md and status emoji).
6. Follow PR_CHECKLIST.
7. Commit `.webp` + INDEX.md; open PR.
8. Ensure GitHub Actions "Validate Screenshots" is green.

## 6) Acceptance Criteria
- All 20 expected filenames present and show ✅ in INDEX.md.
- Validation script succeeds locally and in GitHub Actions.
- Filenames match INDEX.md exactly; file sizes within guidance.
- Visuals match placement intent (per "Used on" links) and use light mode.
- No sensitive data visible.

## 7) Timeline
- Start: next week.
- Duration: ~1 week total:
  - Phase 2a: Setup Wizard (7 shots)
  - Phase 2b: Editor & Publishing (6 shots)
  - Phase 2c: Remaining (7 shots)

## 8) Tools
- Capture: Snagit or Chrome/Edge DevTools + OS screenshot.
- Compression: `cwebp` (lossless or -q), `pngquant` if needed.
- Validation: `.\\Scripts\\validate-screenshots.ps1`.

## 9) Roles
- Developer(s): Capture, compress, validate, submit PR.
- Reviewer/PM: Verify INDEX shows ✅, CI is green, visuals match intent.

## 10) Risks & Mitigations
- Oversized files → Compress (cwebp -q) or crop; rerun validation.
- Naming mismatches → Rerun validation; check PR checklist.
- Missed placements → Use INDEX "Used on" links to confirm context.

## 11) Communication & Checkpoints
- Daily/periodic update: progress vs. 20 items; blockers.
- Mid-week check: Phase completion status (2a/2b/2c).
- Final handoff: PR link + validation output + confirmation that INDEX is fully ✅.

## 12) Scope Boundaries (Out of Scope)
- No new docs/pages; only screenshots for existing placeholders.
- No style/theme changes beyond specified light mode.
- No code changes outside screenshot assets and INDEX.md.

## 13) Access Prerequisites
- Repo access with push rights for a feature branch.
- Ability to run PowerShell locally (for the validation script).
- Browser access to a running SkyCMS instance with safe demo/test data.

## 14) Completion Definition
- PR merged with all 20 screenshots present.
- INDEX.md shows ✅ for all entries and latest validation timestamp.
- GitHub Actions validation green.
- No outstanding warnings from the validation script.

## 15) Optional: Convert to DOCX (via pandoc)
If pandoc is installed locally, from repo root run:
```
pandoc Docs/images/screenshots/SOW-EMAIL.md -o Docs/images/screenshots/SOW-EMAIL.docx
```
Open the generated .docx in Word if you prefer, or open the markdown in Word and "Save As" DOCX.
