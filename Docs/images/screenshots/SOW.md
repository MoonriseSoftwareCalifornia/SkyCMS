# Scope of Work: SkyCMS Documentation Screenshots (Phase 2)

## 1) Objectives
- Capture and deliver 20 documentation screenshots (16 unique images) for end-user docs.
- Follow established specs and automation so `INDEX.md` updates automatically.

## 2) Environment & Setup
1. **Clone repository**
   - `git clone <repo-url>`
   - `cd SkyCMS`
2. **Create a feature branch**
   - `git checkout -b docs/screenshots-phase2`
3. **Open in VS Code**
   - `code .`
   - Ensure PowerShell is available (repo scripts use PowerShell).
4. **Install recommended extensions (optional but helpful)**
   - Markdown support (built-in), GitHub Actions, PowerShell extension.
5. **Run a quick sanity check**
   - `.	a` or `git status` to confirm clean working tree.
   - `.
ode` not required; no build needed for screenshots.
6. **Locate guides** (in VS Code Explorer):
   - `Docs/images/screenshots/00_START_HERE.md`
   - `Docs/images/screenshots/QUICK_REFERENCE.md`
   - `Docs/images/screenshots/INDEX.md`
   - `Docs/images/screenshots/PR_CHECKLIST.md`
7. **After work is complete**
   - Run validation: `.\\Scripts\\validate-screenshots.ps1`
   - Commit `.webp` files + updated `INDEX.md`.
   - Push branch: `git push -u origin docs/screenshots-phase2`
   - Open a Pull Request targeting the main branch.

## 3) Inputs (must read before starting)
- `Docs/images/screenshots/00_START_HERE.md`
- `Docs/images/screenshots/QUICK_REFERENCE.md`
- `Docs/images/screenshots/INDEX.md` (see "Used on" links)
- `Docs/images/screenshots/PR_CHECKLIST.md`
- Optional: `Docs/images/screenshots/SCREENSHOT_GUIDE.md`, `Docs/images/screenshots/AUTOMATION.md`

## 4) Deliverables
- 20 WebP files placed in `Docs/images/screenshots/` with exact filenames per `INDEX.md`.
- Updated `Docs/images/screenshots/INDEX.md` (auto-updated by script showing ✅ where files exist).
- A passing run of the validation script and a PR with these changes.

## 5) Technical Requirements
- Format: WebP (lossless preferred), target 40–150 KB; warn if >200 KB.
- Dimensions: 1440×900 px (height may grow for tall content), 96 DPI, 100% zoom, **light mode**.
- Naming: Must match filenames in `INDEX.md` (kebab-case, case-sensitive).
- Path: `Docs/images/screenshots/{filename}.webp`.
- No sensitive data: use `example.com`, placeholder creds, masked keys.

## 6) Process
1. Read 00_START_HERE and QUICK_REFERENCE.
2. For each filename, open INDEX.md and click "Used on" to see placement context.
3. Capture per specs; hide sensitive data.
4. Save WebP with the exact filename.
5. Run validation: `.\\Scripts\\validate-screenshots.ps1` (auto-updates INDEX.md and status emoji).
6. Follow PR_CHECKLIST.
7. Commit `.webp` + updated `INDEX.md`; open PR.
8. Ensure GitHub Actions “Validate Screenshots” check is green.

## 7) Acceptance Criteria
- All 20 expected filenames present and show ✅ in `INDEX.md`.
- Validation script succeeds locally and in GitHub Actions.
- Filenames match `INDEX.md` exactly (kebab-case).
- File sizes within guidance; no warnings left unresolved.
- Visuals match placement intent (per "Used on" links) and use light mode.
- No sensitive data visible.

## 8) Timeline (per plan)
- Start: next week.
- Duration: ~1 week total.
  - Phase 2a: Setup Wizard (7 shots)
  - Phase 2b: Editor & Publishing (6 shots)
  - Phase 2c: Remaining (7 shots)

## 9) Tools
- Capture: Snagit, or Chrome/Edge DevTools device toolbar + OS screenshot.
- Compression: `cwebp` (lossless or -q), `pngquant` if needed.
- Validation: `.\\Scripts\\validate-screenshots.ps1`.

## 10) Roles
- **Developer(s):** Capture, compress, validate, submit PR.
- **Reviewer/PM:** Verify `INDEX.md` shows ✅, CI is green, and visuals match intent.

## 11) Risks & Mitigations
- **Oversized files:** Compress (cwebp -q) or crop; rerun validation.
- **Naming mismatches:** Rerun validation; check PR checklist.
- **Missed placements:** Use INDEX "Used on" links to confirm context.

## 12) Completion Definition
- PR merged with all 20 screenshots present.
- `INDEX.md` shows ✅ for all entries.
- GitHub Actions validation green.
- No open warnings from the validation script.
