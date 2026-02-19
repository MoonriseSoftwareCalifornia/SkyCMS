---
title: SkyCMS Quick Reference - One-Page Guide
description: One-page visual summary of SkyCMS features, workflow, comparison with alternatives, and getting started.
keywords: quick reference, one-pager, SkyCMS overview, cheat sheet
audience: all
updated: 2025-12-17
---

# SkyCMS Quick Reference Card

One-page visual summary of SkyCMS features, workflow, and comparisons.

---

## SkyCMS at a Glance

**What:** Edge-Native CMS combining WordPress ease-of-use + static site performance  
**When:** Small to medium sites (1-1000 pages) with mixed developer/non-tech teams  
**Why:** Fast launch, low cost, instant publishing, multi-cloud flexibility  
**Cost:** $0-300/year (hosting only; software is free/GPL)  
**Time to Launch:** 3-4 weeks (typical 10-page site)

---

## Core Workflow {#quick-reference-workflow-diagram}

![SkyCMS workflow: Developer creates layouts/templates, Content Creator uses Live Editor, Publish goes live in <5 seconds](images/screenshots/quick-reference-workflow-diagram.webp)

*Visual workflow showing collaboration between developers and content creators through SkyCMS*

```
Developer                          Content Creator
     ↓                                    ↓
Create Layouts ──────→ Publish to SkyCMS ←──── Use Live Editor
Create Templates ────→ (WYSIWYG + Visual) ←──── Create Pages
                            ↓
                      [Preview Draft]
                            ↓
                      [Review → Approve]
                            ↓
                      [Publish → Live] (< 5 sec)
```

---

## Feature Snapshot

| Feature | Status | Notes |
|---------|--------|-------|
| WYSIWYG Editor | ✅ | CKEditor 5 |
| Visual Builder | ✅ | GrapesJS |
| Team Workflows | ✅ | Draft → Review → Publish |
| Multi-User | ✅ | Role-based access |
| Version Control | ✅ | Built-in |
| Media Management | ✅ | File Manager |
| Scheduling | ✅ | Calendar widget |
| SEO Tools | ✅ | Meta, sitemap, links |
| Multi-Site | ✅ | Multi-tenant capable |
| API | ✅ | Optional headless mode |
| Git Integration | ⚠️ | Optional for developers |
| Plugins | 🟡 | Growing ecosystem |

---

## Performance Comparison

```
Publishing Speed:
  SkyCMS:          < 5 sec   ████
  WordPress:       Instant*  ███████ (*but slow delivery)
  Jekyll/Hugo:     5-30 min  ███████████████
  Gatsby:          2-10 min  ██████████
  Contentful:      Instant   ███████ (API only)

Page Load Time (TTFB):
  SkyCMS (Static): 10-50ms  ██
  Hugo/Jekyll:     10-50ms  ██
  Gatsby:          10-50ms  ██
  WordPress:       200-500ms ███████
  Contentful API:  50-200ms  ████
```

---

## Cost Comparison (Annual, Small Site)

| Platform | Setup | Hosting | Licensing | **Total** |
|----------|-------|---------|-----------|----------|
| **SkyCMS** | Free | $0-300 | Free | **$0-300** |
| **WordPress** | Free | $120-1200 | Free/plugins | **$120-1400** |
| **Jekyll/Hugo** | Free | $0-240 | Free | **$0-240** |
| **Gatsby** | Free | Free (Vercel) | Free | **Free** |
| **Contentful** | Free | $348-10,548 | Paid | **$348-10,548** |

---

## Team Complexity

```
Simple (1-2 people):
  Best: SkyCMS, Hugo, Gatsby

Medium (3-10):
  Best: SkyCMS, WordPress, Netlify CMS
  
Large (10-100+):
  Best: SkyCMS multi-tenant, WordPress, Contentful, Sanity

Enterprise (100+):
  Best: Contentful, Sanity, Custom Architecture
```

---

## Quick Decision Tree

```
Are you technical (developer)?
├─ YES → Do you want to write code?
│  ├─ YES → Gatsby, Custom Next.js, Hugo, Jekyll
│  └─ NO  → SkyCMS (visual workflows)
│
└─ NO (Content Creator) → Need a CMS UI?
   ├─ YES → SkyCMS, WordPress, Contentful
   └─ NO  → Jekyll, Hugo (with developer help)

Need non-technical users to publish?
├─ YES → SkyCMS, WordPress
└─ NO  → Any platform

Performance critical?
├─ YES → SkyCMS, Hugo, Jekyll, Gatsby (static)
└─ NO  → WordPress, Contentful, Sanity

Budget-conscious?
├─ YES → SkyCMS ($0-300/yr), Jekyll/Hugo, Gatsby
└─ NO  → Any platform
```

---

## Getting Started (5 Steps)

```
1. Deploy SkyCMS
   └─ Docker container to cloud or on-premise
   
2. Run Setup Wizard (5 min)
   └─ Database → Storage → Admin account → Publisher
   
3. Create Layouts
   └─ HTML/CSS for site structure (header, footer, nav)
   
4. Create Templates
   └─ Page-type blueprints with editable regions
   
5. Publish Pages
   └─ Content creators use Live Editor to build pages
```

**Time to first page:** 15-30 min (with planning)  
**Time to launch:** 3-4 weeks (typical)

---

## SkyCMS vs. Competitors

### SkyCMS Strengths
✅ Fast launch (15-30 min setup)  
✅ Non-tech friendly (WYSIWYG editor)  
✅ Instant publishing (< 5 sec)  
✅ Multi-cloud (no vendor lock-in)  
✅ Low cost ($0-300/yr possible)  
✅ Built-in workflows (no Git required)  
✅ Origin-less edge hosting (Cloudflare R2)

### SkyCMS Considerations
⚠️ Smaller ecosystem (vs. WordPress)  
⚠️ Newer platform (growing maturity)  
⚠️ Web-first (not mobile SDKs)  
⚠️ No plugin marketplace yet

### When to Choose Alternatives

| When | Choose | Why |
|------|--------|-----|
| Need massive plugin ecosystem | WordPress | 50k+ plugins |
| Team is developer-first | Hugo, Jekyll | Code-centric |
| Multi-channel (web + mobile) | Contentful, Sanity | Headless APIs |
| Maximum customization | Gatsby, Custom | React/Nodejs |
| Zero hosting cost | Gatsby, Jekyll (GitHub Pages) | Free tier |
| Enterprise support needed | Contentful, Sanity | SLA + Support |

---

## Tech Stack

```
Database:        MySQL, MS SQL, SQLite, Cosmos DB
Storage:         S3, Azure Blob, Cloudflare R2, Google Cloud
Editor:          CKEditor 5 (WYSIWYG), GrapesJS (Visual)
Framework:       ASP.NET Core (.NET 8+)
Deployment:      Docker
Hosting:         Any cloud (static, dynamic, or edge mode)
```

---

## Next Steps

1. **Read:** [Website Launch Workflow](./Developer-Guides/Website-Launch-Workflow.md) (15 min)
2. **Compare:** [Comparisons Matrix](./Comparisons.md) (10 min)
3. **Explore:** [Learning Paths](./LEARNING_PATHS.md) (role-based guides)
4. **Deploy:** [Installation Overview](./Installation/README.md) (setup guide)
5. **Learn:** [Developer Guides](./Developer-Guides/README.md) (all phases)

---

## Cheat Sheet: Common Tasks

| Task | Time | Reference |
|------|------|-----------|
| Create a layout | 30-60 min | [Phase 2](./Developer-Guides/02-Creating-Layouts.md) |
| Create a template | 30-90 min | [Phase 3](./Developer-Guides/03-Creating-Templates.md) |
| Build home page | 60-90 min | [Phase 4](./Developer-Guides/04-Building-Home-Page.md) |
| Add content page | 15-30 min | [Phase 5](./Developer-Guides/05-Building-Initial-Pages.md) |
| Set up workflows | 30-60 min | [Content Workflow Template](./Templates/Content-Workflow-Template.md) |
| Train content creator | 1-2 hours | [Training Template](./Templates/Training-Document-Template.md) |
| Pre-launch QA | 2-4 hours | [Pre-Launch Checklist](./Developer-Guides/Checklists/Pre-Launch-Checklist.md) |
| Monthly maintenance | 2-4 hours | [Monthly Maintenance](./Checklists/Monthly-Maintenance.md) |

---

## Resources

- **Docs:** [Complete Documentation](./index.md)
- **FAQ:** [Frequently Asked Questions](./FAQ.md)
- **Comparisons:** [Feature Matrix](./Comparisons.md)
- **GitHub:** https://github.com/CWALabs/SkyCMS
- **Issues:** Report bugs and feature requests on GitHub

---

**Last Updated:** December 17, 2025

**Tip:** Print this page and share with your team for quick reference!
