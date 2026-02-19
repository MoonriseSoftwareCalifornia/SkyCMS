---
title: Developer Guides
description: Complete workflows and best practices for developers building and maintaining SkyCMS sites
keywords: developer-guides, workflow, best-practices, launch, templates, layouts
audience: [developers, technical-leads]
---

# Developer Guides

Complete workflows and best practices for developers building and maintaining SkyCMS sites.

## Applicability Notes

- These guides use SkyCMS examples, but the processes and tips apply broadly to modern website development (planning, componentized layouts/templates, accessibility, SEO, testing, publishing, and maintenance).
- Everything here is intended to be useful: adopt the parts that fit your project and skip or adapt the rest.
- Mapping to other stacks: "Live Editor" → any WYSIWYG/block editor; "Templates" → components/layouts; "File Manager" → static assets/CDN; content workflows → draft/review/publish with approvals.

## SkyCMS & Modern Approach

SkyCMS encourages a modern, sustainable website development workflow:
- **Componentized architecture:** Clear separation between layouts (site-wide), templates (page types), and pages (instances) enables reuse and consistency.
- **Content-first workflows:** Built around draft→review→approve→publish, with roles and handoff guidance that match contemporary content ops.
- **Quality baked in:** The guides emphasize accessibility, performance, SEO, link validation, and ongoing maintenance.
- **Operational readiness:** Publisher verification and environment prerequisites align with disciplined release practices.

Learn more: [SkyCMS & Modern Approach (Deep Dive)](./SkyCMS-Modern-Approach.md)


## Quick Navigation

### **Website Launch Workflow** (Start Here for New Projects)

**[Complete Website Launch Workflow →](./Website-Launch-Workflow.md)**

A comprehensive 6-phase roadmap for taking a fresh SkyCMS installation to a fully functional website ready for content creators.

**Perfect for:**
- Launching a new SkyCMS website
- Understanding the complete development workflow
- Planning your project timeline
- Training new developers on SkyCMS

**Time to read:** 15-20 minutes (overview + planning)
**Time to complete:** 35-55 hours (all phases)

---

## Workflow Phases

Once you start the Website Launch Workflow, use these phase-specific guides:

| Phase | Guide | Duration | Focus |
|-------|-------|----------|-------|
| **1** | [Design & Planning](./01-Design-and-Planning.md) | 3-5 hrs | Architecture understanding, site planning |
| **2** | [Creating Layouts](./02-Creating-Layouts.md) | 4-8 hrs | Site-wide structure, responsive design |
| **3** | [Creating Templates](./03-Creating-Templates.md) | 8-16 hrs | Page types, reusable components |
| **4** | [Building Home Page](./04-Building-Home-Page.md) | 4-6 hrs | First page, publishing, SEO setup |
| **5** | [Building Initial Pages](./05-Building-Initial-Pages.md) | 8-12 hrs | Content pages, navigation, linking |
| **6** | [Preparing for Handoff](./06-Preparing-for-Handoff.md) | 4-8 hrs | Team setup, training, documentation |

---

## Supporting Checklists

- Technical validation (database, storage, CDN)
[Complete checklist →](./Checklists/Pre-Launch-Checklist.md)
- Content completeness
- Performance and security
- SEO and analytics
- Backup verification

### **Content Creator Onboarding Checklist**
[Complete checklist →](./Checklists/Content-Creator-Onboarding.md)

Ensure smooth handoff to content creators:
- Account creation and access
- Training completion

### **Monthly Maintenance Tasks**
[Complete checklist →](../Checklists/Monthly-Maintenance.md)

Ongoing maintenance after launch:

---

## Supporting Templates
- Typography and fonts
[Get template →](../Templates/Style-Guide-Template.md)
- Image guidelines
- Tone of voice
- Content guidelines
- Common design patterns

Build a training guide for content creators:

• Customize the [Training Document Template](../Templates/Training-Document-Template.md)
**Use by:** New content creators (during onboarding)

---

### **Content Workflow Template**
[Get template →](../Templates/Content-Workflow-Template.md)

Document your content approval process:
- Content assignment
- Draft creation
- Review process
- Approval gates
- Publishing workflow
- Roles and responsibilities
- Communication points

**Create:** During Phase 6, customize for your team
**Use by:** Content team (during ongoing operations)

---

## When to Use These Guides

### **Scenario 1: "I'm launching a new SkyCMS website"**
→ Start with [Website Launch Workflow](./Website-Launch-Workflow.md), then follow each phase guide

### **Scenario 2: "I need to add new page types after launch"**
→ Jump to [Phase 3: Creating Templates](./03-Creating-Templates.md)

### **Scenario 3: "I need to train a content creator"**
→ Use [Training Document Template](../Templates/Training-Document-Template.md) with [Phase 6 Guide](./06-Preparing-for-Handoff.md)

### **Scenario 4: "I'm about to launch and want to verify everything"**
→ Use [Pre-Launch Checklist](./Checklists/Pre-Launch-Checklist.md)

### **Scenario 5: "I'm setting up content workflows for my team"**
→ Use [Content Workflow Template](../Templates/Content-Workflow-Template.md)

### **Scenario 6: "I need to maintain the site after launch"**
→ Use [Monthly Maintenance Tasks](../Checklists/Monthly-Maintenance.md)

---

## Key Concepts

### **Layouts vs. Templates vs. Pages**

The foundation of SkyCMS development:

**Layouts** - Site-wide structure
- Header, footer, navigation
- Appears on every page
- Change once, affects entire site
- Created by developers

**Templates** - Page type blueprints
- Blog post layout, service page layout, etc.
- Reusable for multiple pages
- Define content sections
- Created by developers

**Pages** - Actual content instances
- Specific blog post, specific service page
- Contains the actual text/images
- Created/edited by content creators
- Based on a template using a layout

### **Workflow Summary**

```
Developer creates → Layouts + Templates
                ↓
Content creator creates → Pages (using templates)
                ↓
Visitors see → Published pages with layouts applied
```

---

## Prerequisites

Before using these guides, you should have:

- ✅ SkyCMS installed and running (see [Installation](../Installation/README.md))
- ✅ Administrator account created
- ✅ Database and storage configured
- ✅ Publisher verified (see [Post-Installation](../Installation/Post-Installation.md))
- ✅ Access to editor interface (`/Editor`)

---

## Related Documentation

### **Installation & Setup**
- [Installation Overview](../Installation/README.md) - Getting SkyCMS running
- [Setup Wizard](../Installation/SetupWizard.md) - Initial configuration
- [Post-Installation](../Installation/Post-Installation.md) - After wizard

### **SkyCMS Architecture**
- [Startup Lifecycle](../Architecture/Startup-Lifecycle.md) - How SkyCMS starts
- [Middleware Pipeline](../Architecture/Middleware-Pipeline.md) - Request processing

### **Core Documentation**
- [Layouts Guide](../Layouts/Readme.md) - In-depth layout reference
- [Templates Guide](../Templates/Readme.md) - In-depth template reference
- [Role-Based Access Control](../Administration/Roles-and-Permissions.md) - User roles and permissions

### **Advanced Topics**
- [Multi-Tenant Configuration](../Configuration/Multi-Tenant-Configuration.md) - Multiple sites on one instance
- [Troubleshooting](../Troubleshooting.md) - Common issues and solutions

---

## Getting Help

### **I'm stuck on a phase**
- Review the phase-specific guide again
- Check the "Common Pitfalls" section
- See [Troubleshooting](../Troubleshooting.md)

### **I need to train content creators**
- Customize the [Training Document Template](../Templates/Training-Document-Template.md)
- Reference the [Phase 6: Preparing for Handoff](./06-Preparing-for-Handoff.md) guide

### **I need to set up permissions**
- Read [Role-Based Access Control](../Administration/Roles-and-Permissions.md)
- Follow the role assignment steps in [Phase 6](./06-Preparing-for-Handoff.md)

### **Something's broken**
- Check [Troubleshooting](../Troubleshooting.md)
- Review the specific phase guide where the issue occurs

---

## Quick Reference

### **Typical Project Timeline**

```
Week 1:
  - Mon-Tue: Phase 1 (Design & Planning)
  - Tue-Wed: Phase 2 (Layouts)
  - Wed-Fri: Phase 3 (Templates)

Week 2:
  - Mon-Tue: Phase 4 (Home Page)
  - Tue-Fri: Phase 5 (Initial Pages)

Week 3:
  - Mon-Wed: Phase 6 (Handoff)
  - Thu-Fri: Pre-launch QA
  - Friday: Launch & Handoff
```

### **Decision Tree: Layout vs. Template vs. Page?**

```
Is this a site-wide element?
├─ YES → This is a LAYOUT change
│
└─ NO → Is this for one specific page?
   ├─ YES → Customize this PAGE
   │
   └─ NO → This is a TEMPLATE (reusable page type)
```

### **Estimated Hours by Complexity**

| Site Type | Hours | Notes |
|-----------|-------|-------|
| **Simple** | 30-40 | 1-2 templates, 5-10 pages |
| **Medium** | 40-55 | 3-5 templates, 10-20 pages |
| **Complex** | 55-75+ | 5+ templates, 20+ pages, custom workflows |

---

## Summary

This documentation provides everything you need to:

1. ✅ Plan and design a new SkyCMS website
2. ✅ Build layouts and templates
3. ✅ Create initial content pages
4. ✅ Train non-technical content creators
5. ✅ Launch with confidence
6. ✅ Maintain the site after launch

**Start here:** [Website Launch Workflow](./Website-Launch-Workflow.md)

**Questions?** See the [FAQ in the main workflow guide](./Website-Launch-Workflow.md#faq)
