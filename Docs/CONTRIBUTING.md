---
title: Documentation Contribution Guide
description: Guidelines for organizing, maintaining, and improving SkyCMS documentation
keywords: contributing, documentation, guidelines, maintenance, ownership
audience: [developers, technical-writers]
---

# Documentation Contribution Guide

Welcome to the SkyCMS documentation. This guide outlines how documentation is organized, maintained, and improved.

---

## Table of Contents

- [Before You Start](#before-you-start)
- [Documentation Ownership Model](#documentation-ownership-model)
- [File Naming Conventions](#file-naming-conventions)
- [File Header Template](#file-header-template)
- [Update Triggers](#update-triggers)
- [Review Schedule](#review-schedule)
- [Best Practices](#best-practices)
- [Getting Help](#getting-help)

---

## Before You Start

All documentation is in the `/Docs/` folder. The main entry points are:

- **[README.md](./index.md)** - Primary documentation hub (start here)
- **[LEARNING_PATHS.md](./LEARNING_PATHS.md)** - Role-based documentation journeys
- **[About.md](./About.md)** - SkyCMS overview and positioning

### Overview vs Reference Pattern

- **Overviews** answer what/why/when and point to related guides.
- **Configuration references** hold the authoritative settings, defaults, and examples (one per area, e.g., Database-Configuration-Reference.md).
- **Provider/setup guides** live alongside overviews and should link to the reference and overview.
- Start new docs from the templates in [_templates](./_templates/) to keep structure consistent.

---

## Documentation Ownership Model

Each documentation file should have clear ownership and maintenance responsibility.

### File Header Template

Add this header to the top of major documentation files:

```markdown
---
Owner: @github-username
Last Reviewed: YYYY-MM-DD
Status: Current | Needs Update | Deprecated
Related Docs: [Link to related docs]
---
```

### Example

```markdown
---
Owner: @toiyabe
Last Reviewed: 2025-12-15
Status: Current
Related Docs: [Database-CosmosDB.md](./Configuration/Database-CosmosDB.md), [Database Config Reference](./Configuration/Database-Configuration-Reference.md)
---

# Azure Cosmos DB Configuration
```

### Status Values

- **Current** - Documentation is accurate and complete for the latest release
- **Needs Update** - Documentation exists but may be incomplete or outdated
- **Deprecated** - Documentation covers deprecated features; marked for removal
- **In Progress** - Documentation is being written/updated

### Owner Responsibilities

The owner is responsible for:

1. **Accuracy** - Keeping information correct and up-to-date
2. **Completeness** - Ensuring all relevant information is documented
3. **Currency** - Reviewing and updating as features change
4. **Clarity** - Maintaining consistent quality and readability
5. **Links** - Keeping cross-references accurate and useful

---

## File Naming Conventions

Maintain consistency in naming for easy discovery and organization.

### Standard Pattern

Use **PascalCase with hyphens** for multi-word concepts:

```
✅ CORRECT:
Database-Overview.md
Database-Configuration-Reference.md
Live-Editor-Quick-Start.md
Azure-Installation-Guide.md
Cost-Comparison.md

❌ INCORRECT:
DatabaseOverview.md
database_overview.md
databaseoverview.md
Database Overview.md
QuickStart.md (use hyphens for clarity)
```

### File Organization

```
Docs/
├── README.md                           [Main entry point]
├── LEARNING_PATHS.md                   [Role-based journeys]
├── CONTRIBUTING.md                     [This file]
├── About.md                            [Overview]
├── QuickStart.md                       [5-minute start]
├── Troubleshooting.md                  [Issues & solutions]
│
├── Configuration/                      [Config guides grouped]
│   ├── Database-Overview.md
│   ├── Database-Configuration-Reference.md
│   ├── Storage-Overview.md
│   ├── Storage-Configuration-Reference.md
│   └── CDN-Overview.md
│
├── Editors/                            [Editor documentation]
│   ├── LiveEditor/
│   │   ├── README.md
│   │   ├── Quick-Start.md
│   │   └── [other files]
│   ├── Designer/
│   ├── CodeEditor/
│   └── ImageEditing/
│
├── _Marketing/                         [Marketing materials]
│   ├── README.md
│   ├── Competitor-Analysis.md
│   └── Cost-Comparison.md
│
└── _archive/                           [Archived/deprecated docs]
    └── [old files]
```

---

## Update Triggers

Documentation MUST be updated when:

### 1. Feature Changes (Required)

- **New Feature Added** - Create new documentation or update existing guides
  - Add to overview documents
  - Create provider-specific guides if applicable
  - Update LEARNING_PATHS.md if it affects user journeys
  - Link from main README.md

- **Feature Modified** - Update all related documentation
  - Update configuration formats
  - Update examples and screenshots
  - Update troubleshooting if error messages change
  - Mark as "Needs Update" if you can't update immediately

- **Feature Removed** - Archive or deprecate documentation
  - Mark status as "Deprecated"
  - Add note about removal and alternatives
  - Move to _archive/ folder
  - Update all cross-references

### 2. Configuration Changes (Required)

- Connection string formats change
- Environment variable names change
- API endpoints change
- Authentication methods change
- Permission models change

**Action:** Update configuration reference documents and provider-specific guides

### 3. External Dependencies (Required)

- Azure APIs change
- AWS services are updated
- Cloudflare features change
- Database provider updates
- Browser compatibility changes

**Action:** Update relevant sections and mark as "Needs Review"

### 4. User-Reported Issues (Recommended)

- Documentation is unclear or incorrect
- Examples don't work
- Steps are outdated
- Links are broken
- Information is missing

**Action:** Create GitHub issue, fix documentation, reference issue in commit

### 5. Code Changes (Recommended)

- Code samples in documentation
- UI changes in screenshots
- API examples
- Configuration defaults

**Action:** Update code samples and examples

---

## Review Schedule

### Quarterly Reviews (Every 3 Months)

**Documentation to review:**
- Quick Start guides
- Getting Started materials
- LEARNING_PATHS.md
- README.md

**Checklist:**
- [ ] Are instructions still accurate?
- [ ] Do links still work?
- [ ] Are screenshots current?
- [ ] Is information complete?
- [ ] Update "Last Reviewed" date

### Release Reviews (With Each Release)

**Documentation to review:**
- Configuration guides (Database, Storage, CDN)
- Feature documentation for changed features
- API documentation
- Breaking changes documentation
- CHANGELOG.md

**Checklist:**
- [ ] Do examples work with new version?
- [ ] Are new features documented?
- [ ] Are deprecated features marked?
- [ ] Is troubleshooting updated?
- [ ] Are links still valid?

### Annual Reviews (Yearly)

**Documentation to review:**
- Architecture documentation
- Component documentation
- Developer guides
- Testing guides
- CONTRIBUTING.md (this file)

**Checklist:**
- [ ] Structure still makes sense?
- [ ] Outdated information?
- [ ] Missing topics?
- [ ] Quality consistent throughout?

---

## Best Practices

### 1. Clarity and Simplicity

- Use short sentences
- Define technical terms
- Provide examples for complex concepts
- Explain the "why" not just the "how"
- Use active voice

### 2. Structure

- Use clear headings (H1, H2, H3)
- Start with overview or context
- Provide step-by-step instructions when applicable
- End with "Next Steps" or "Related Topics"
- Keep sections focused on one topic

### 3. Examples

- Provide working code examples
- Show both correct and incorrect usage
- Include expected output
- Update examples with each release
- Test examples before publishing

### 4. Links and References

- Link to related documentation
- Use descriptive link text (avoid "click here")
- Keep links relative where possible
- Check links are not broken
- Update links when files move

### 5. Formatting

- Use **bold** for UI elements and important terms
- Use `code blocks` for commands and file names
- Use > quotes for tips and warnings
- Use tables for comparisons
- Keep line length readable (< 100 chars)

### 6. Consistency

- Use consistent terminology throughout
- Maintain consistent formatting
- Follow naming conventions
- Use consistent structure across similar docs
- Maintain professional tone

### 7. Completeness

- Cover all configuration options
- Document all parameters
- Explain error messages
- Include troubleshooting
- Provide prerequisites

---

## Before Submitting Documentation Changes

### Checklist

- [ ] Used correct file naming convention (PascalCase-with-hyphens.md)
- [ ] Added/updated file header with ownership
- [ ] Checked for broken links
- [ ] Verified code examples work
- [ ] Tested command examples
- [ ] Updated cross-references
- [ ] Updated README.md table of contents if adding new docs
- [ ] Updated LEARNING_PATHS.md if it affects user journeys
- [ ] Ran spelling check
- [ ] Verified screenshots are current
- [ ] Tested on different browsers/devices if applicable
- [ ] Followed style guide and formatting
- [ ] Added comments to explain complex sections
- [ ] Removed redundant information
- [ ] Updated "Last Reviewed" date in file header

### Testing Documentation

For configuration documentation:
```bash
# Test that connection strings work with actual services
# Test configuration with setup wizard
# Verify examples with current version
# Check that all variables are substituted correctly
```

For feature documentation:
```bash
# Follow steps as a new user would
# Test on different browsers
# Verify UI matches current version
# Check that all features mentioned are available
```

---

## Common Documentation Patterns

### Configuration Guide Structure

```markdown
---
Owner: @username
Last Reviewed: YYYY-MM-DD
Status: Current
---

# [Service] Configuration

## Overview
- What is this service?
- Why use it?
- When to use alternatives

## Prerequisites
- Required account/service
- Required permissions
- Required information to gather

## Connection String Format
```
Format: ...
Example: ...
```

## Step-by-Step Setup
1. In [Service] console...
2. Navigate to...
3. Create/configure...

## Verification
How to test the configuration

## Troubleshooting
- Common issues
- Solutions
- Where to get help
```

### Feature Guide Structure

```markdown
---
Owner: @username
Last Reviewed: YYYY-MM-DD
Status: Current
Related Docs: [Related docs]
---

# [Feature Name]

## Overview
What is this feature and what does it do?

## Getting Started (Quick Start)
- Prerequisites
- 5 steps to get started
- Expected result

## How It Works
Explanation of the feature

## Using [Feature]
Step-by-step instructions

## Examples
Real-world examples

## Advanced Usage
Optional advanced features

## Troubleshooting
Common issues and solutions

## See Also
Links to related documentation
```

---

## Documentation Issues

### Found a Documentation Bug?

1. **Check if it's your understanding** - Read the docs again carefully
2. **Search for related issues** - Might already be reported
3. **Create a GitHub issue** with:
   - What the documentation says
   - What actually happens
   - Expected behavior
   - Steps to reproduce (if applicable)
   - Your environment (OS, browser, SkyCMS version)

### Want to Improve Documentation?

1. **Create an issue describing improvement** (optional but helpful)
2. **Create a fork and branch** (`docs/improve-xyz`)
3. **Make your changes** following this guide
4. **Test your changes** (especially code examples)
5. **Create a pull request** with clear description
6. **Respond to feedback** and iterate

---

## Documentation Tools

### Writing

- **Markdown editor** - VS Code with Markdown Preview
- **Spell check** - VS Code extension or https://www.grammarly.com/
- **Link checker** - https://www.deadlinkchecker.com/

### Organization

- **Outline view** - VS Code Outline panel (Ctrl+Shift+O)
- **Table of contents** - Use heading structure
- **Search** - GitHub code search or VS Code search

### Version Control

```bash
# Create feature branch for docs
git checkout -b docs/feature-name

# Commit documentation changes
git commit -m "docs: Update [topic] documentation"

# Push and create pull request
git push origin docs/feature-name
```

---

## Style Guide

### Tone

- **Professional but friendly**
- Not too formal, not too casual
- Clear and direct
- Encouraging and helpful
- Assume reader wants to succeed

### Voice

- **Active voice** (preferred)
  - ✅ "Click the Save button"
  - ❌ "The Save button should be clicked"

- **Second person** (when giving instructions)
  - ✅ "You can configure..."
  - ✅ "To configure..."
  - ❌ "Users can configure..." (usually)

### Capitalization

- Capitalize UI element names: "Click the Save Button"
- Don't capitalize common words: "the page template"
- Capitalize proper nouns: "Azure Cosmos DB"

### Code

```markdown
- Inline code: `variable-name`, `appsettings.json`
- Code blocks: Use markdown fences with language
- Comments: Explain non-obvious code
```

### Links

```markdown
- Descriptive text: [Database Configuration Guide](./Configuration/Database-Overview.md)
- Relative paths: Use `./` for same folder, `../` for parent
- Avoid: [Click here](link), [Link](link)
```

---

## Getting Help

### Questions About Documentation?

- **Check this guide** - Might answer your question
- **Check CONTRIBUTING.md in other repos** - Good examples
- **Ask on GitHub Discussions** - Community can help
- **Contact the project maintainer** - @toiyabe

### Want to Become a Documentation Owner?

1. Identify documentation you can maintain
2. Create an issue expressing interest
3. Make improvements to that documentation
4. Get approval from project maintainer
5. Add your name to file header as owner

### Improvement Ideas?

1. Create a GitHub issue with your suggestion
2. Include examples of what you'd like to see
3. Explain why the change would help
4. Reference similar documentation if applicable

---

## Tools and Resources

### Markdown Resources
- [Markdown Syntax Guide](https://guides.github.com/features/mastering-markdown/)
- [Markdown Cheat Sheet](https://www.markdownguide.org/cheat-sheet/)

### Writing Resources
- [Google Developer Documentation Style Guide](https://developers.google.com/style)
- [Microsoft Style Guide](https://docs.microsoft.com/en-us/style-guide/welcome/)

### Version Control
- [GitHub Help](https://help.github.com/)
- [Git Cheat Sheet](https://github.github.com/training-kit/)

### Tools
- [VS Code](https://code.visualstudio.com/) - Editor
- [Markdown Preview Enhanced](https://marketplace.visualstudio.com/items?itemName=shd101wyy.markdown-preview-enhanced)
- [Spell Right](https://marketplace.visualstudio.com/items?itemName=ban.spellright)

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-12-15 | Initial documentation standards |

---

## Questions?

- **GitHub Issues:** [Create an issue](https://github.com/CWALabs/SkyCMS/issues/new)
- **Discussions:** [Ask a question](https://github.com/CWALabs/SkyCMS/discussions)
- **Email:** For urgent matters, contact the maintainer

Thank you for helping improve SkyCMS documentation! 🙏
