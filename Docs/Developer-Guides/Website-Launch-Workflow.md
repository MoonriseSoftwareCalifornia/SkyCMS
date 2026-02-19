---
title: Website Launch Workflow Guide
description: Complete 6-phase roadmap for launching a SkyCMS website from initial design to content creator handoff
keywords: website-launch, workflow, developer-guide, project-management, deployment
audience: [developers]
---

# Website Launch Workflow Guide

**Take your SkyCMS installation from zero to ready for content creators in 6 structured phases.**

This guide provides a complete roadmap for developers launching a new SkyCMS website. It covers the journey from initial design through handing off to non-technical content creators, with phase-specific deep dives, checklists, and templates included.

## Table of Contents
- [Overview](#overview)
- [Who This Is For](#who-this-is-for)
- [Prerequisites](#prerequisites)
- [The 6-Phase Workflow](#the-6-phase-workflow)
- [Learning Objectives](#learning-objectives)
- [Architecture Foundation](#architecture-foundation)
- [Workflow Timeline](#workflow-timeline)
- [Decision Trees](#decision-trees)
- [Common Pitfalls](#common-pitfalls)

## Overview

Launching a new website with SkyCMS involves more than just installing the software. You need to:

1. **Understand the architecture** - How Layouts, Templates, and Pages work together
2. **Plan the site structure** - Determine page types and content organization
3. **Build reusable foundations** - Create layouts and templates for consistency
4. **Populate initial content** - Create your first pages and set up navigation
5. **Prepare for handoff** - Document workflows and train content creators
6. **Support creators** - Ensure smooth transition to non-technical team

This workflow is **iterative** - you may loop back to earlier phases as you discover new requirements or content types.

## Who This Is For

**Primary Audience:**
- Full-stack developers building custom SkyCMS sites
- Frontend developers responsible for site design
- Development leads overseeing SkyCMS projects

**Supporting Audience:**
- Project managers coordinating the launch
- Content strategists planning page types
- Technical leads making architecture decisions

**NOT for:**
- Content creators (see [Content Creator Training Guide](../Templates/Training-Document-Template.md) instead)
- End-users managing the site post-launch
- System administrators managing SkyCMS infrastructure

## Prerequisites

Before starting this workflow, you should have:

- ✅ SkyCMS running and initialized (follow [Setup Wizard](../Installation/SetupWizard.md) first)
- ✅ Access to the editor interface (`/Editor`)
- ✅ Administrator account created
- ✅ Database and storage configured
- ✅ Publisher verified and working (see [Post-Installation](../Installation/Post-Installation.md))

**If you haven't completed these, start here:**
→ [Installation Overview](../Installation/README.md)

## The 6-Phase Workflow

### **Phase 1: Design & Planning**
**Duration:** 3-5 hours | **Effort:** Planning & Analysis

Understand the SkyCMS architecture and plan your site structure before building anything.

**Deliverables:**
- Site structure diagram (pages, sections, content types)
- Page type inventory (home, about, services, blog, etc.)
- Content hierarchy and organization
- Navigation structure

**Key Questions:**
- What are the main sections of the site?
- How many distinct page types do you need?
- What content will be updated frequently vs. rarely?
- Is this content-heavy, image-heavy, or both?

**Next Steps:**
→ [Phase 1: Design & Planning Guide](./01-Design-and-Planning.md)

---

### **Phase 2: Creating Layouts**
**Duration:** 4-8 hours | **Effort:** Low-code Implementation

Build the site-wide structural layouts (header, footer, navigation, sidebar positions).

**Deliverables:**
- Main layout (header, footer, nav structure)
- Optional: Secondary layouts (sidebar layout, full-width layout)
- Responsive design confirmed
- Layout documentation for content creators

**What You'll Do:**
- Create layouts in SkyCMS editor
- Add placeholders for theme elements
- Set up responsive breakpoints
- Test in different browsers/devices

**Key Concepts:**
- Layouts define the overall site structure
- All pages use a layout
- Layouts are shared across templates and pages
- Modify layouts once, updates apply everywhere

**Next Steps:**
→ [Phase 2: Creating Layouts Guide](./02-Creating-Layouts.md)

---

### **Phase 3: Creating Templates**
**Duration:** 8-16 hours | **Effort:** Medium Implementation

Build reusable page templates based on your identified page types.

**Deliverables:**
- Home page template (with hero section, featured content)
- Blog post template (title, date, author, content)
- Service/product page template (title, description, CTA)
- Documentation page template (breadcrumbs, table of contents, sidebar nav)
- Additional templates as needed for your content types

**What You'll Do:**
- Create template variations using layouts
- Add reusable sections and widgets
- Implement common design patterns
- Test template flexibility with different content

**Key Concepts:**
- Templates extend layouts with specific page type structure
- Templates are reusable blueprints
- Consistent design across all page types
- Each template can have its own styling

**Next Steps:**
→ [Phase 3: Creating Templates Guide](./03-Creating-Templates.md)

---

### **Phase 4: Building Home Page**
**Duration:** 4-6 hours | **Effort:** Implementation + Content

Create your first live page - the home page - from your layout and template.

**Deliverables:**
- Published home page
- Navigation working and visible
- Hero section with compelling messaging
- Featured content/sections
- Contact information or CTA visible
- Meta tags for SEO

**What You'll Do:**
- Create home page from home template
- Add hero section content
- Feature key content areas
- Setup calls-to-action
- Verify publishing works
- Test visitor experience

**Key Concepts:**
- This is your first real page
- It's the entry point for your site
- It needs to work on mobile and desktop
- SEO setup starts here

**Next Steps:**
→ [Phase 4: Building Home Page Guide](./04-Building-Home-Page.md)

---

### **Phase 5: Building Initial Pages**
**Duration:** 8-12 hours | **Effort:** Content + Implementation

Create the initial set of pages to launch with (about, services, key content pages).

**Deliverables:**
- About/Company page
- Contact page
- 3-5 key content pages
- Navigation fully populated
- Breadcrumb navigation working
- Internal linking established
- Site hierarchy established

**What You'll Do:**
- Create pages using appropriate templates
- Add real content (or placeholder content)
- Setup navigation and menus
- Test page-to-page navigation
- Verify all links work
- Check responsive design

**Key Concepts:**
- These pages establish site credibility
- Navigation should be intuitive
- Internal linking helps SEO and UX
- This is when you verify templates work with real content

**Next Steps:**
→ [Phase 5: Building Initial Pages Guide](./05-Building-Initial-Pages.md)

---

### **Phase 6: Preparing for Handoff**
**Duration:** 4-8 hours | **Effort:** Documentation + Training

Prepare your site and team for content creators to take over.

**Deliverables:**
- User roles configured (Authors, Editors, Reviewers)
- Content creator accounts created
- Style guide for content creators
- Training documentation completed
- Content workflow documented
- Support plan established
- Pre-launch checklist completed

**What You'll Do:**
- Create user accounts for team members
- Assign appropriate roles
- Write/customize training guide
- Create style guide
- Document common workflows
- Establish support process
- Final pre-launch QA

**Key Concepts:**
- Clear roles prevent access confusion
- Documentation prevents support calls
- Training reduces onboarding time
- Workflows create consistency

**Next Steps:**
→ [Phase 6: Preparing for Handoff Guide](./06-Preparing-for-Handoff.md)

---

## Learning Objectives

By completing this workflow, you will be able to:

### **Architecture Understanding**
- [ ] Explain the relationship between Layouts → Templates → Pages
- [ ] Determine when to use layout changes vs. template changes vs. page customizations
- [ ] Design scalable site structures that support future content types

### **Hands-On Skills**
- [ ] Create and modify layouts in SkyCMS
- [ ] Build reusable page templates
- [ ] Implement common design patterns (hero sections, CTAs, navigation)
- [ ] Create pages and manage the publishing workflow
- [ ] Setup navigation and content hierarchy

### **Best Practices**
- [ ] Apply naming conventions for organization
- [ ] Design for content creator ease-of-use
- [ ] Plan for maintenance and future scaling
- [ ] Document decisions for team clarity

### **Launch Readiness**
- [ ] Prepare comprehensive checklists for launch
- [ ] Train non-technical team members to update content
- [ ] Establish sustainable workflows for ongoing content updates
- [ ] Plan post-launch support and monitoring

## Architecture Foundation

Before you start building, understand how SkyCMS pieces fit together:

```
┌─────────────────────────────────────────────────────┐
│                    LAYOUT                           │
│  (Site-wide structure: header, nav, footer)        │
│                                                     │
│  ┌──────────────────────────────────────────────┐  │
│  │                                              │  │
│  │              TEMPLATE CONTENT AREA           │  │
│  │  (Defines page structure: hero, sidebar...) │  │
│  │                                              │  │
│  │  ┌──────────────────────────────────────┐   │  │
│  │  │                                      │   │  │
│  │  │     PAGE CONTENT                    │   │  │
│  │  │  (Actual content: text, images...)  │   │  │
│  │  │                                      │   │  │
│  │  └──────────────────────────────────────┘   │  │
│  │                                              │  │
│  └──────────────────────────────────────────────┘  │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### **Key Relationships**

| Component | Purpose | Scope | Who Changes It |
|-----------|---------|-------|-----------------|
| **Layout** | Site-wide structure (header, footer, nav) | Entire site | Developer (rarely) |
| **Template** | Page type structure (e.g., blog post layout) | All pages using this template | Developer (occasionally) |
| **Page** | Actual content instance | Single page | Content creator (frequently) |

### **Real-World Example**

```
LAYOUT: "Main Layout"
├── Header (site logo, main nav) - shared by all pages
├── Footer (contact info, links) - shared by all pages
└── Main Content Area
    └── TEMPLATE: "Blog Post Template"
        ├── Title area
        ├── Author/date area
        ├── Featured image area
        └── Content area
            └── PAGE: "Why We Love SkyCMS"
                ├── Title: "Why We Love SkyCMS"
                ├── Author: "Jane Smith"
                ├── Date: "Dec 17, 2025"
                ├── Image: (blog-hero.jpg)
                └── Content: (the full blog post text)

            └── PAGE: "Getting Started with SkyCMS"
                ├── Title: "Getting Started with SkyCMS"
                ├── Author: "Bob Johnson"
                └── ... (different content, same template)
```

**Notice:** Both blog posts use the same Template and Layout, but have different content. Change the template → affects both blogs. Change a page → only affects that page.

## Workflow Timeline

### **Typical Project Timeline**

```
┌─────────────────────────────────────────────────────────────┐
│ TOTAL PROJECT TIME: ~35-55 hours (varies by site complexity) │
└─────────────────────────────────────────────────────────────┘

Week 1:
  Phase 1: Design & Planning .......... 4-5 hours (Mon-Tue)
  Phase 2: Creating Layouts ........... 6-8 hours (Tue-Wed)
  Phase 3: Creating Templates ......... 10-16 hours (Wed-Fri)

Week 2:
  Phase 4: Building Home Page ......... 4-6 hours (Mon-Tue)
  Phase 5: Building Initial Pages .... 8-12 hours (Tue-Fri)

Week 3:
  Phase 6: Preparing for Handoff ....... 4-8 hours (Mon-Wed)
  Pre-Launch Checklist & QA ........... 4-8 hours (Wed-Fri)
  Launch & Handoff ................... (Friday)
```

**Actual timeline depends on:**
- Site complexity (number of page types)
- Team availability
- Design complexity
- Content readiness
- Number of pages in launch set

## Decision Trees

### **Decision: Should This Be a New Template or Page Variation?**

```
"I need a different layout for this page type"
         |
         v
   Is this layout used by:
   • More than one page? → Create new TEMPLATE
   • Only this one page? → Create on this PAGE as custom
```

### **Decision: Should This Be a Layout Change or Template Change?**

```
"The header needs updating"
         |
         v
   Does it affect:
   • All pages on the site? → Change the LAYOUT
   • Just pages of one type? → Change the TEMPLATE
   • Just this one page? → Customize this PAGE
```

### **Decision: When to Add a New Template?**

```
"Do we need a template for this content type?"
         |
         v
   Will we create:
   • Multiple pages of this type? → YES, create template
   • Just one page? → NO, customize a page
         |
         v
   Is it significantly different from existing templates?
   • Very different structure? → Create NEW template
   • Similar to existing? → Use/modify existing template
```

## Common Pitfalls

### ❌ **Pitfall 1: Mixing Content and Structure**

**Problem:** Creating a "Contact Page" layout instead of a reusable template
```
Bad: Layout named "Contact Layout"
     └─ Specific to contact page, not reusable

Good: Template named "Contact Form Template"
      └─ Can be used for multiple contact-type pages
```

**Solution:** Separate concerns. Layouts are for site structure. Templates are for page types.

### ❌ **Pitfall 2: Over-Engineering from the Start**

**Problem:** Building 20 templates before creating any pages
```
Waste of time on templates you might not need
```

**Solution:** Build the minimum templates for launch, add more as needed.

### ❌ **Pitfall 3: Not Testing with Content Creators**

**Problem:** Building templates that are confusing or hard to use
```
"Why is this field hidden?"
"How do I add a section?"
"What's a 'featured image'?"
```

**Solution:** Have content creators test templates during Phase 5-6 and adjust based on feedback.

### ❌ **Pitfall 4: Forgetting Navigation Setup**

**Problem:** Building pages but no navigation structure
```
Readers can't find pages
Internal linking missing
SEO suffers
```

**Solution:** Plan navigation structure in Phase 1, implement in Phase 5.

### ❌ **Pitfall 5: Launching with Incomplete Pages**

**Problem:** Publishing pages with placeholder content or broken links
```
Unprofessional appearance
Broken links hurt SEO
Confuses visitors
```

**Solution:** Use pre-launch checklist to verify all pages before publishing.

### ❌ **Pitfall 6: No Documentation for Content Creators**

**Problem:** Handing off to content creators with no guides
```
"How do I create a blog post?"
"What's a template?"
"Where's my publish button?"
Constant support requests
```

**Solution:** Create training guide and style guide in Phase 6 before handoff.

## Phase-Specific Guides

Each phase has a detailed guide with step-by-step instructions:

1. **[Phase 1: Design & Planning](./01-Design-and-Planning.md)**
   - Architecture overview
   - Site structure planning
   - Wireframing approach
   - Content type inventory

2. **[Phase 2: Creating Layouts](./02-Creating-Layouts.md)**
   - Layout creation walkthrough
   - Real examples (header, footer, nav)
   - Best practices
   - Responsive design

3. **[Phase 3: Creating Templates](./03-Creating-Templates.md)**
   - Template creation workflow
   - Common patterns
   - Widget usage
   - Organization strategies

4. **[Phase 4: Building Home Page](./04-Building-Home-Page.md)**
   - Step-by-step home page creation
   - Hero section setup
   - Publishing workflow
   - SEO basics

5. **[Phase 5: Building Initial Pages](./05-Building-Initial-Pages.md)**
   - Content page creation
   - Navigation setup
   - Internal linking
   - Page hierarchy

6. **[Phase 6: Preparing for Handoff](./06-Preparing-for-Handoff.md)**
   - Role setup and permissions
   - Content workflows
   - Training strategy
   - Launch readiness

## Supporting Resources

### **Checklists**
- [Pre-Launch Checklist](./Checklists/Pre-Launch-Checklist.md) - Final verification before launch
- [Content Creator Onboarding](./Checklists/Content-Creator-Onboarding.md) - Handoff verification
- [Monthly Maintenance Tasks](../Checklists/Monthly-Maintenance.md) - Ongoing upkeep

### **Templates**
- [Style Guide Template](../Templates/Style-Guide-Template.md) - For documenting brand guidelines
- [Training Document Template](../Templates/Training-Document-Template.md) - For training content creators
- [Content Workflow Template](../Templates/Content-Workflow-Template.md) - For mapping approval processes

## Next Steps

**Ready to begin?**

Start with [Phase 1: Design & Planning](./01-Design-and-Planning.md)

**Already know your site structure?**

Jump to [Phase 2: Creating Layouts](./02-Creating-Layouts.md)

**Need to understand SkyCMS first?**

Read [SkyCMS Architecture Overview](../Architecture/Startup-Lifecycle.md)

## Related Documentation

- [Setup Wizard](../Installation/SetupWizard.md) - Initial installation
- [Post-Installation](../Installation/Post-Installation.md) - After wizard completes
- [Layouts Guide](../Layouts/README.md) - In-depth layout documentation
- [Templates Guide](../Templates/README.md) - In-depth template documentation
- [Role-Based Access Control](../Administration/Roles-and-Permissions.md) - Setting up user roles
- [Content Creator Training](../Templates/Training-Document-Template.md) - Training your team

## FAQ

**Q: How long does this take?**
A: Typical project takes 35-55 hours of developer time over 2-3 weeks. Depends on site complexity and team availability.

**Q: Can I skip any phases?**
A: Not really. Each phase builds on the previous. However, you can combine phases if your site is simple.

**Q: What if I don't have content ready?**
A: Use placeholder content in phases 4-5, replace with real content later. Focus on structure first.

**Q: Can I do this alone or do I need a team?**
A: You can do it solo as a developer, but involving content strategists and designers makes phases 1-3 faster and better.

**Q: What happens after launch?**
A: Use [Monthly Maintenance Tasks](../Checklists/Monthly-Maintenance.md) to keep the site healthy. Content creators add new pages using templates you created.

**Q: How do I add new page types after launch?**
A: Create new templates following [Phase 3 guide](./03-Creating-Templates.md), then train content creators on the new template.

---

**Ready to launch your SkyCMS website?** Begin with Phase 1 → [Design & Planning](./01-Design-and-Planning.md)
