# LICENSES DISTRIBUTION

## Purpose

This document explains how to distribute SkyCMS in ways that respect the licenses of included components, especially CKEditor 5.

This is operational guidance for engineering and release workflows, not legal advice.

## License Components At A Glance

- SkyCMS original source code: MIT (`LICENSE-MIT`)
- CKEditor 5: dual-licensed by CKSource
  - GPL-2.0-or-later, or
  - Commercial license from CKSource
- Other third-party libraries: see `NOTICE.md` and their original license files

## Decision Matrix

| Distribution scenario | Can use MIT-only path for SkyCMS code? | CKEditor 5 path | Key obligations |
|---|---|---|---|
| Internal use only (no distribution outside your org) | Yes | GPL or commercial | Keep notices internally; track provenance |
| Hosted/SaaS only (users access service, software not distributed) | Yes | GPL or commercial | Keep notices and third-party compliance; confirm no software distribution occurs |
| Distribute container/image/binary/package including GPL CKEditor 5 | No (for the distributed combined GPL-covered work) | GPL-2.0-or-later | Provide GPL notices and corresponding source obligations for distributed GPL-covered work |
| Distribute proprietary/commercial package including CKEditor 5 | Yes, if CKEditor rights are commercial | Commercial CKEditor license required | Maintain MIT + third-party notices; keep proof of CKEditor commercial entitlement |
| Distribute SkyCMS code without CKEditor 5 included | Yes | N/A | MIT + third-party compliance for included dependencies |

## Release Policy

1. Every release must declare which CKEditor 5 licensing path is used:
   - `GPL`
   - `COMMERCIAL`
2. A release that includes CKEditor 5 must include:
   - `NOTICE.md`
   - appropriate license files for included third-party software
3. A `GPL` release must include:
   - GPL license text (`LICENSE-GPL`)
   - clear statement that CKEditor 5 is used under GPL-2.0-or-later
   - source availability process for distributed GPL-covered work
4. A `COMMERCIAL` release must include:
   - record of CKEditor commercial entitlement (order/license reference in private compliance records)
   - packaging checks to ensure only commercially authorized CKEditor artifacts are shipped

## Packaging Profiles

## `oss-gpl` profile

Use when distributing open-source builds that include CKEditor 5 under GPL.

Required:
- include `LICENSE-GPL`
- include `LICENSE-CKEDITOR-GPL`
- include `NOTICE.md`
- publish corresponding source for the distributed GPL-covered work and build/install scripts needed to reproduce that work
- do not add downstream restrictions that conflict with GPL rights

## `commercial` profile

Use when distributing proprietary/commercial builds that include CKEditor 5 under CKSource commercial terms.

Required:
- maintain internal record of CKEditor commercial license entitlement
- include `NOTICE.md`
- include `LICENSE-MIT` for SkyCMS code
- ensure release artifacts do not claim CKEditor is under GPL in this profile

## `source-only` profile

Use when shipping SkyCMS source/components without CKEditor 5 bundled.

Required:
- include `LICENSE-MIT`
- include `NOTICE.md`
- verify CKEditor 5 artifacts are excluded from package

## Engineering Checklist (Per Release)

1. Select profile: `oss-gpl`, `commercial`, or `source-only`.
2. Generate SBOM/dependency inventory for shipped artifacts.
3. Verify license files included in release bundle.
4. Verify `NOTICE.md` matches shipped dependencies.
5. Verify CKEditor 5 path:
   - GPL path: GPL files and source offer/process present.
   - Commercial path: entitlement recorded; no GPL-specific claim in release notes.
6. Archive compliance bundle with release tag:
   - notices
   - license files
   - artifact manifest
   - profile used
   - approval record

## Suggested Repository Conventions

- `LICENSE-MIT`: SkyCMS original code
- `LICENSE-GPL`: GPL text
- `LICENSE-CKEDITOR-GPL`: CKEditor 5 GPL-path notice
- `NOTICE.md`: third-party attributions and links
- `LICENSES-DISTRIBUTION.md`: this operational distribution policy

## Source References

- CKEditor 5 license file:
  - https://github.com/ckeditor/ckeditor5/blob/master/packages/ckeditor5/LICENSE.md
- CKEditor licensing options:
  - https://ckeditor.com/legal/ckeditor-licensing-options/
- CKEditor pricing/commercial:
  - https://ckeditor.com/pricing/

## Notes

If you are uncertain whether a deployment model counts as distribution in your jurisdiction, escalate to legal counsel before release.