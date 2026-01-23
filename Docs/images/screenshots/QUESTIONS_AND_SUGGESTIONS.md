# Screenshot Project: Questions & Suggestions

> Notes and recommendations for the screenshot capture and documentation process.

---

## Questions for You

### 1. **Who Will Capture the Screenshots?**

**Current assumption**: A designer, content creator, or technical writer will use the guide to capture screenshots.

**Questions:**
- Will one person be capturing all screenshots, or multiple people?
- If multiple, do they need training or can they reference the guide independently?
- Should we create a video tutorial showing the capture process?

---

### 2. **Timing & Rollout**

**Current plan**: Capture all 20 screenshots before publishing updated docs.

**Alternative approaches:**
- **Phased rollout**: Capture setup wizard (7 images) first, then other sections. This gives early feedback before full investment.
- **On-demand**: Only capture screenshots for the most-viewed docs (probably Setup Wizard, Live Editor, Publishing).
- **Hybrid**: Capture setup wizard now, defer others to next documentation cycle.

**Questions:**
- What's your timeline? When do you need these images?
- Should we prioritize setup wizard screenshots first (most user-facing)?
- Is there a budget for paid tools like Snagit, or stick with free options?

---

### 3. **Style & Theme Consistency**

**Current guidance**: Use Windows 11 Light theme consistently across all captures.

**Considerations:**
- **macOS vs Windows vs Linux**: Should we show OS-specific screenshots if SkyCMS runs on multiple platforms?
- **Dark mode**: Some users prefer dark mode. Should we include dark mode variants?
- **Branding**: Any SkyCMS logo/branding to include in corner of screenshots?

**Suggestions:**
- Stick with Light mode for now (best readability in docs)
- Consider dark mode variants in future release cycle

---

### 4. **Annotations & Callouts**

**Current guidance**: Keep screenshots plain; use markdown alt text for context instead of annotating images.

**Rationale:**
- Plain images age better as UI evolves
- Annotations clutter; alt text is cleaner
- Harder to maintain annotated versions

**Alternative**: Should we use subtle numbered callouts (1, 2, 3) on screenshots with a legend in the markdown? This helps readers understand complex UI.

**My suggestion**: Keep initial set plain; add annotations only if user feedback shows they're needed.

---

### 5. **Screen Reader Accessibility**

**Current practice**: Include descriptive alt text in markdown.

```markdown
![Setup wizard welcome screen with storage, admin, and publisher steps](../images/screenshots/setup-wizard-welcome.webp)
```

**Questions:**
- Should we provide extended descriptions for complex screenshots (linked via caption)?
- Do we need transcripts or textual descriptions of workflow diagrams?

**Suggestion**: Include brief alt text (40-50 chars) + optional caption with more detail if screenshot is critical to understanding.

---

### 6. **Sensitive Data & Example Values**

**Current guidance**: Hide all real data; use placeholders.

**Questions:**
- Should we provide a consistent set of example values across all screenshots?
  - Example email: `admin@example.com`, `user@yoursite.com`
  - Example domain: `https://www.example.com`, `https://mysite.azurewebsites.net`
  - Example API key: `••••••••` or show first/last 4 chars?

**Suggestion**: Yes. This creates visual consistency and helps users understand what real values would go there.

---

### 7. **Screenshot Dimensions Flexibility**

**Current recommendation**: 1440×900 px (16:9 widescreen).

**Questions:**
- For full-page scrolling screenshots, is it OK to exceed 1440 px height?
  - Setup wizard with 5+ steps filled might be 1440×2000 px
  - Hangfire dashboard might be 1440×1200 px
- Should we use different dimensions for mobile mockups (e.g., 375×812 for iPhone)?

**Suggestion**: 
- Allow height flexibility; width should stay consistent at 1440 px
- Don't force letterboxing for tall content
- Skip mobile mockups for now (focus on desktop UI)

---

### 8. **Version Control & Updates**

**Current plan**: Screenshots in `Docs/images/screenshots/` with simple filename scheme (no versioning).

**Questions:**
- When SkyCMS UI changes, how do we handle screenshot updates?
  - Overwrite existing file (simple, but may cause display issues if image is cached)
  - Version files (`setup-wizard-v2.webp`) and update links (more complex)
  - Keep old screenshots in `_archive/` subfolder for reference

**Suggestion**: Overwrite files and add a note in docs changelog when UI changes. Keep INDEX.md in sync with actual files.

---

### 9. **File Size & Storage**

**Current targets:**
- Single dialog/small area: 20-40 KB
- Full-page: 60-100 KB
- Complex dashboard: up to 150 KB

**Questions:**
- Is repository storage a concern? How large can the screenshots folder grow?
- Should we exclude WebP originals from repo, only keep final compressed versions?

**Suggestion**: All 20 screenshots at target sizes = ~1.5–2 MB total. This is reasonable for docs. No need to compress further or archive originals.

---

### 10. **Index Maintenance**

**Current approach**: [INDEX.md](./INDEX.md) lists all screenshots with clickable links, placement context, and status.

**Questions:**
- Should we create a simple status tracking file (e.g., CSV or checklist) that a tool could parse?
- Would a GitHub issue or pull request template be helpful to track completion?

**Suggestion**: 
- Keep INDEX.md as primary tracking (human-readable, easy to update)
- Optionally: Create a simple HTML dashboard pulling status from INDEX.md
- Use ✅ / ⬜ / 🟨 emoji for status visibility

---

## Suggestions for Improvement

### 1. **Create a Capture Template**

A pre-configured SkyCMS instance "in screenshot mode" with:
- Predictable test data (pages named "Sample 1", "Sample 2", etc.)
- No real user content
- Consistent state (not mid-editing)

**Benefit**: Anyone capturing screenshots sees the same content, leading to consistent images.

---

### 2. **Test Render Outputs**

After capturing a batch of screenshots:
1. Copy `.webp` files to `Docs/images/screenshots/`
2. Render the markdown locally (`jekyll serve` or similar)
3. Verify images display correctly, sizes are reasonable
4. Check for blurriness, artifacts, or rendering issues

**Benefit**: Catch issues before publishing; test WebP browser compatibility.

---

### 3. **Create Video Walkthrough** (Optional)

A short 5-minute video showing:
1. How to navigate SkyCMS to each screenshot location
2. Which parts of the UI to capture
3. How to compress and name files
4. How to verify placement in docs

**Benefit**: Faster onboarding for anyone capturing screenshots.

---

### 4. **Batch Captures by Feature**

Instead of randomizing, capture in logical order:
- **Batch 1** (Day 1): Setup Wizard (7 images) — foundational, widely used
- **Batch 2** (Day 2): Live Editor basics (3 images) — core workflow
- **Batch 3** (Day 3): Publishing + Post-Install (5 images)
- **Batch 4** (Day 4): Page Scheduling + Settings (5 images)

**Benefit**: 
- Faster to set up SkyCMS once for a batch
- Easier to maintain consistency within batch
- Can test and verify each batch before moving on

---

### 5. **Create PR Checklist for Screenshot Submissions**

When someone submits screenshots via PR:

```markdown
## Screenshot Upload Checklist

- [ ] All files are .webp (no PNG unless justified)
- [ ] File sizes are 20-150 KB each
- [ ] Filenames follow kebab-case naming convention
- [ ] No sensitive data visible (emails, APIs, real URLs)
- [ ] Markdown links updated to match filenames exactly
- [ ] INDEX.md status updated to ✅
- [ ] Local markdown preview shows images correctly
- [ ] Images are sharp and readable (no blurriness)
```

**Benefit**: Ensures quality and consistency of submitted screenshots.

---

### 6. **Optional: Automate Naming Validation**

A pre-commit hook or GitHub Action that:
- Checks all files in `Docs/images/screenshots/` follow naming convention
- Verifies all placeholders in markdown have matching files
- Warns if INDEX.md status shows `⬜` but file exists

**Benefit**: Catches errors before merge; keeps INDEX.md in sync.

---

### 7. **Internationalization Consideration**

**Future question**: Should screenshots be language-specific if SkyCMS supports multiple languages?

**Current suggestion**: Start with English (US) only. Add localized screenshots in future if needed.

---

## Summary of Recommendations

| Item | Recommendation | Rationale |
|---|---|---|
| **Capture Tool** | Snagit (paid) or GIMP (free) | Best quality, lossless WebP export |
| **Timeline** | Phased: Setup Wizard first, then other sections | Lower initial effort, early feedback |
| **Who** | One person initially; document process for others | Easier to maintain consistency |
| **Dimensions** | 1440×900 px width (height flexible) | Readable, consistent, web-standard |
| **Format** | WebP, PNG fallback only if needed | Modern, compressed, accessible |
| **Theme** | Windows 11 Light mode consistently | Best readability, universally recognizable |
| **Annotations** | None on images (use markdown alt text) | Ages better, cleaner, more maintainable |
| **Sensitive Data** | Use placeholders (admin@example.com, etc.) | Professional, privacy-safe, consistent |
| **Index** | Maintain as live tracking doc with links | Single source of truth, human-readable |
| **Quality Gate** | Batch testing & verification before merge | Catches issues early |

---

## Next Steps (Your Decision)

1. **Decide capture timeline**
   - When do you want the first batch (Setup Wizard) complete?
   - Can you assign someone to this task?

2. **Review SCREENSHOT_GUIDE.md**
   - Does it cover everything the screenshot creator needs?
   - Any clarifications or additions?

3. **Set up capture environment**
   - Fresh SkyCMS instance for screenshots
   - Ensure test data is consistent/predictable

4. **Start with Setup Wizard batch**
   - 7 screenshots (3 of which are reused across docs)
   - Easier to verify and iterate before committing

5. **Share feedback**
   - What works well? What's confusing?
   - Adjust guide based on real-world capture experience

---

## Contact / Questions

If anything is unclear in the guides, or if you have additional requirements, let me know and I can refine the documentation.
