---
title: Phase 4 - Building Home Page
description: First live page creation, hero sections, featured content, SEO setup, testing, and publishing
keywords: home-page, hero, content, SEO, testing, publishing, live-editor, phase-4
audience: [developers, content-creators]
---

# Phase 4: Building Your Home Page

## Overview

Your home page is the most important page on your website—it's often the first impression visitors have of your brand, and it serves as the gateway to the rest of your site. In this phase, you'll transform your Site Design (layout) and templates into a fully functional, content-rich home page.

**What You'll Accomplish:**
- Create a new home page using a template
- Understand the page creation workflow in SkyCMS
- Learn how to select and apply the right template for your home page

**Prerequisites:**
Before starting this phase, ensure you have:
- ✅ Completed Phase 1: [Design & Planning](01-Design-and-Planning.md)
- ✅ Completed Phase 2: [Creating Layouts (Site Designs)](02-Creating-Layouts.md)
- ✅ Completed Phase 3: [Creating Templates](03-Creating-Templates.md)
- ✅ At least one template with editable regions ready to use
- ✅ Content plan or wireframe for your home page
- ✅ Administrator or Editor role access in SkyCMS

**Estimated Time:** 3-6 hours (depending on content complexity)

---

## Table of Contents

- [Understanding the Home Page Role](#understanding-the-home-page-role)
- [Creating a New Page from a Template](#creating-a-new-page-from-a-template)
- [Understanding the Page Creation Workflow](#understanding-the-page-creation-workflow)
- [Selecting the Right Template](#selecting-the-right-template)
- [Next Steps](#next-steps)

---

## Understanding the Home Page Role

### Why the Home Page Matters

Your home page serves multiple critical functions:

1. **First Impressions**: Visitors form an opinion about your brand within seconds
2. **Navigation Hub**: Guides visitors to key areas of your site
3. **Value Proposition**: Clearly communicates what you offer and why it matters
4. **Conversion Driver**: Encourages visitors to take desired actions (contact, purchase, sign up)
5. **SEO Foundation**: Often the most linked-to and highest-ranking page on your site

### Home Page Typical Structure

While home pages vary by industry and purpose, most successful home pages include:

```
┌─────────────────────────────────────┐
│  Hero Section                       │
│  (Headline, subtext, primary CTA)   │
├─────────────────────────────────────┤
│  Value Proposition                  │
│  (What you do, why it matters)      │
├─────────────────────────────────────┤
│  Featured Content                   │
│  (Services, products, features)     │
├─────────────────────────────────────┤
│  Social Proof                       │
│  (Testimonials, stats, logos)       │
├─────────────────────────────────────┤
│  Call to Action                     │
│  (Contact form, newsletter, demo)   │
└─────────────────────────────────────┘
```

**Key Insight**: Your template from Phase 3 should already define this structure with editable regions. In this phase, you'll fill those regions with actual content.

---

## Creating a New Page from a Template

### Step 1: Access the Page Creation Interface

1. Log into your SkyCMS admin panel
2. Navigate to **"Pages"** in the main menu
3. Click **"Create a Page"** button

**Alternative Path:**
- From the dashboard, click **"New Page"** in the quick actions area

### Step 2: Define the Page URL

The first decision when creating a page is choosing its URL path.

**For the Home Page:**

The home page typically uses one of these URL patterns:

| URL Pattern | Usage | Notes |
|-------------|-------|-------|
| `/` | Root domain home page | Most common for primary site home page |
| `/index` | Traditional index page | Some servers treat this as default |
| `/home` | Explicit home page path | Clear but less common |

**Recommendation:** Use `/` for your primary home page.

**Example:**
```
Your domain: www.example.com
Home page URL: www.example.com/
```

**To Create the Root Home Page:**

1. In the "Page URL" field, enter: `/`
2. The system will show: `www.example.com/`
3. Verify this is the correct URL for your home page

**Important Notes:**
- URLs in SkyCMS are case-sensitive
- Use lowercase for consistency and SEO best practices
- Avoid spaces (use hyphens instead)
- Keep URLs short and descriptive
- The URL cannot be changed after creation (you would need to create a new page)

### Step 3: Choose Your Template

After entering the URL, you'll see the **"Quick Start Templates"** section.

**What You'll See:**
- A list of all available templates
- Template names and descriptions
- Preview thumbnails (if available)
- Indicator if "Live Editor" is enabled (meaning it has editable regions)

**For Your Home Page, Look For:**
- Templates labeled "Home Page," "Landing Page," or "Hero + Content"
- Templates with multiple editable regions
- Templates marked with "Live editor enabled"

**Template Decision Matrix:**

| Template Type | Best For | Typical Structure |
|---------------|----------|-------------------|
| **Hero + Featured Content** | Most home pages | Large hero, 3-4 content sections, CTA |
| **Full-Width Landing** | Product launches, campaigns | Single focus, multiple CTAs |
| **Two-Column with Sidebar** | Blogs, news sites | Content + sidebar widgets |
| **Minimal Single Column** | Portfolios, agencies | Clean, image-focused, minimal text |

**Example Selection Process:**

```
Available Templates:
1. ✓ "Home Page - Hero + Services" (Live editor enabled)
   - Large hero section
   - 3 service highlight areas
   - Testimonial section
   - Contact CTA

2. "Product Landing Page" (Live editor enabled)
   - Product showcase
   - Feature list
   - Pricing section

3. "Blog Post Template" (Live editor enabled)
   - Article content
   - Sidebar
   - Related posts

For a business home page → Choose Template #1
```

### Step 4: Create the Page

1. Select your chosen template by clicking on it (it will highlight)
2. Click **"Create"** button
3. Wait for confirmation message

**What Happens:**
- SkyCMS creates a new page at your specified URL
- The page is populated with the template's structure
- All editable regions are ready for content
- The page is created in **Draft** status (not published)
- You're redirected to the editing interface

**Initial Page State:**
```
✓ Page created at specified URL
✓ Template structure applied
✓ Editable regions identified
✓ Status: Draft (not visible to public)
✗ Content: Empty (needs to be added)
✗ Published: No (needs manual publishing)
```

---

## Understanding the Page Creation Workflow

### The SkyCMS Page Lifecycle

Pages in SkyCMS follow a predictable lifecycle:

```
1. Create → 2. Edit → 3. Preview → 4. Publish → 5. Maintain
   ↓         ↓         ↓         ↓         ↓
 Draft    Content   Review   Live     Updates
           Entry              Public
```

**Detailed Workflow:**

#### 1. **Create (Current Step)**
- Define URL
- Select template
- Initialize page structure
- **Status:** Draft

#### 2. **Edit (Next Steps in This Guide)**
- Add content to editable regions
- Upload images
- Configure widgets
- Style elements
- **Status:** Draft (can save multiple versions)

#### 3. **Preview**
- View page as visitors would see it
- Test responsive design
- Check links and functionality
- **Status:** Still Draft

#### 4. **Publish**
- Make page live to the public
- Page becomes accessible via its URL
- **Status:** Published

#### 5. **Maintain**
- Update content over time
- Each edit creates a new version
- Can unpublish if needed
- **Status:** Published (or back to Draft)

### Version Control in SkyCMS

Every time you save changes to a page, SkyCMS creates a new version:

**Version Tracking:**
```
Version 1 (Draft) → Initial creation
Version 2 (Draft) → Added hero content
Version 3 (Draft) → Added featured services
Version 4 (Published) → First publish
Version 5 (Published) → Updated testimonials
```

**Key Features:**
- **Auto-Save**: Changes save automatically (configurable interval)
- **Manual Save**: Ctrl+S (Cmd+S on Mac) saves immediately
- **Version History**: Access all previous versions
- **Rollback**: Restore any previous version if needed

**To Access Version History:**
1. Go to Pages list
2. Click on your page article number or title
3. View all versions with timestamps
4. Click any version to preview or restore

### Draft vs. Published Status

Understanding the difference is critical:

| Aspect | Draft | Published |
|--------|-------|-----------|
| **Visibility** | Admin/Editors only | Public (all visitors) |
| **URL Access** | Preview URL only | Live site URL |
| **SEO** | Not indexed | Indexed by search engines |
| **Editing** | Freely editable | Creates new draft version |
| **Status Indicator** | Yellow "Draft" badge | Green "Published" badge |

**Important:** A page can have both a published version AND a draft version simultaneously. This allows you to work on updates without affecting the live page.

**Example Scenario:**
```
Your Home Page Status:
- Version 4 (Published) → Live on site at www.example.com/
- Version 5 (Draft) → Being edited, not public

Visitors see: Version 4
You're editing: Version 5
When you publish Version 5 → It becomes the live version
```

---

## Selecting the Right Template

### Matching Template to Purpose

The right template depends on your home page goals. Use this decision framework:

#### Business/Corporate Website
**Primary Goals:**
- Establish credibility
- Showcase services
- Generate leads

**Recommended Template Features:**
- Large hero section for brand message
- Service/product highlights (3-4 items)
- Testimonial or client logo section
- Contact form or CTA section

**Template Name Examples:**
- "Business Home Page"
- "Corporate Landing"
- "Professional Services Template"

#### E-commerce Website
**Primary Goals:**
- Showcase products
- Drive sales
- Highlight promotions

**Recommended Template Features:**
- Eye-catching hero with promotion
- Featured product grid
- Category navigation
- Trust indicators (shipping, returns, security)

**Template Name Examples:**
- "E-commerce Home Page"
- "Shop Landing Template"
- "Product Showcase Template"

#### Portfolio/Creative Website
**Primary Goals:**
- Showcase work
- Tell your story
- Attract clients

**Recommended Template Features:**
- Large visual hero
- Project gallery or grid
- Brief about section
- Contact information

**Template Name Examples:**
- "Portfolio Home Page"
- "Creative Landing"
- "Agency Showcase Template"

#### Nonprofit/Community Website
**Primary Goals:**
- Share mission
- Encourage involvement
- Drive donations

**Recommended Template Features:**
- Mission-focused hero
- Impact stories or stats
- Ways to help (donate, volunteer, share)
- Latest news or events

**Template Name Examples:**
- "Nonprofit Home Page"
- "Community Landing"
- "Cause Template"

### Evaluating Template Quality

Before committing to a template, evaluate these factors:

#### ✅ Template Quality Checklist

**Structural Quality:**
- [ ] Has clearly defined editable regions
- [ ] Includes "Live editor enabled" indicator
- [ ] Structure matches your content plan
- [ ] Adequate editable regions for your needs
- [ ] Not overly complex or restrictive

**Design Quality:**
- [ ] Responsive layout (works on mobile)
- [ ] Clean, professional appearance
- [ ] Consistent with your Site Design (layout)
- [ ] Supports your brand colors/fonts
- [ ] Visually balanced sections

**Functional Quality:**
- [ ] Includes necessary sections (hero, content, CTA)
- [ ] Supports widgets if needed
- [ ] Has appropriate heading hierarchy (H1, H2, H3)
- [ ] Includes semantic HTML structure
- [ ] SEO-friendly markup

### Template Preview and Testing

**Before Creating Your Page:**

While SkyCMS doesn't always provide full template previews before page creation, you can:

1. **Check Template Descriptions**
   - Read the template's description field
   - Look for documentation or notes
   - Identify key features

2. **Review Template Code** (If Comfortable with HTML)
   - Click "Code" button on template
   - Review the HTML structure
   - Identify editable regions (`data-ccms-ceid`)
   - Check for complexity

3. **Create a Test Page First**
   - Create page at `/test-home` instead of `/`
   - Add sample content
   - Review how it looks
   - Delete and create final page at `/` when satisfied

**Pro Tip:** Always create a test page first, especially if you're uncertain about a template. It's easier to delete and restart than to rebuild.

### What If You Choose the Wrong Template?

**Don't Worry—You Have Options:**

#### Option 1: Create a New Page
- Delete the current page (if it's just a draft)
- Create new page with different template
- This is easiest if you haven't added much content

#### Option 2: Switch Templates
- Some content may be lost (non-matching editable regions)
- Use "Update from Template" feature
- Review and re-add any lost content

#### Option 3: Edit as Blank Page
- Remove template association
- Manually edit HTML/content
- More control but more work

**Recommendation:** If you haven't invested much time in content, Option 1 (start over) is usually fastest.

---

## Next Steps

Congratulations! You've now created your home page and understand the page creation workflow.

**What You've Accomplished:**
- ✅ Created a new home page at your desired URL
- ✅ Selected and applied an appropriate template
- ✅ Understand the page lifecycle (draft → edit → publish)
- ✅ Know how to access version history

**Up Next in Phase 4:**
Continue building your home page content by following these guides in order:

1. **[Hero Section](#)** (Coming Next)
   - Design hero content
   - Add hero images and backgrounds
   - Write compelling headlines and CTAs
   - Make it responsive

2. **[Featured Content](#)**
   - Showcase services or products
   - Add testimonials and social proof
   - Create engaging content sections

3. **[CTAs & Navigation](#)**
   - Place effective call-to-action buttons
   - Integrate navigation elements
   - Add contact forms

4. **[SEO Basics](#)**
   - Optimize page title and meta descriptions
   - Structure headings properly
   - Add alt text to images

5. **[Testing & Publishing](#)**
   - Preview your page
   - Test responsiveness
   - Publish to make it live

---

## Quick Reference

### Page Creation Commands

| Action | Steps |
|--------|-------|
| **Create Page** | Pages → Create a Page → Enter URL → Select Template → Create |
| **Preview Draft** | Open page in editor → Click "Preview" |
| **Save Changes** | Ctrl+S (Cmd+S) or wait for auto-save |
| **View Versions** | Pages → Click article number → View history |
| **Publish Page** | Open page → Click "Publish" |

### Common URLs for Home Pages

| URL | Purpose | Example |
|-----|---------|---------|
| `/` | Main home page | `www.example.com/` |
| `/index` | Alternative home | `www.example.com/index` |
| `/home` | Explicit home URL | `www.example.com/home` |

### Template Selection Quick Guide

| Site Type | Template Features Needed |
|-----------|-------------------------|
| **Business** | Hero + Services + Testimonials + Contact |
| **E-commerce** | Hero + Products + Categories + Trust |
| **Portfolio** | Large Visual + Gallery + About + Contact |
| **Nonprofit** | Mission + Impact + Ways to Help + News |

---

## Troubleshooting

### Issue: Can't Find Template List

**Solution:**
- Ensure you're logged in with Administrator or Editor role
- Verify you're on the "Create a Page" screen (not "Edit")
- Check that templates exist (go to Templates section)
- Confirm templates are associated with your Site Design

### Issue: Template Doesn't Have Live Editor

**Solution:**
- Template may not have editable regions defined
- Review template in Phase 3 guide
- Add `data-ccms-ceid` attributes to template
- Or choose a different template with "Live editor enabled"

### Issue: Page Created at Wrong URL

**Solution:**
- URLs cannot be changed after creation
- Delete the page (if it's still a draft)
- Create new page with correct URL
- Or use redirect rules (advanced)

### Issue: Template Structure Doesn't Match Plan

**Solution:**
- Delete page if minimal work invested
- Create new page with better-matching template
- Or manually edit page content to adapt structure

---

## Additional Resources

- **Previous Phase:** [Creating Templates](03-Creating-Templates.md)
- **Parallel Reading:** [Pages Documentation](../ContentCreation-QuickStart.md)
- **Template Reference:** [Templates Guide](../Templates/README.md)
- **Live Editor Guide:** [Using the Live Editor](../Editors/LiveEditor/README.md)

---

**Ready to Continue?** Proceed to the next section where you'll build out your hero section—the most important visual element of your home page.

---

## Building Your Hero Section

The hero section is the first content visitors see when they land on your home page. It typically occupies the full viewport (above the fold) and serves as the primary attention-grabber that communicates your core message.

**What You'll Learn:**
- Hero section design patterns and best practices
- How to add hero content using editable regions
- Working with hero images and backgrounds
- Creating compelling headlines and CTAs
- Making hero sections responsive

---

### What Makes a Great Hero Section?

#### Essential Elements

A high-converting hero section typically includes:

1. **Compelling Headline** (H1)
   - Clear, concise statement of your value proposition
   - 5-10 words maximum
   - Uses action-oriented language
   - Immediately answers: "What do you do?"

2. **Supporting Subheadline** (H2 or paragraph)
   - Expands on the headline
   - 10-20 words
   - Provides context or benefit
   - Answers: "Why should I care?"

3. **Visual Element**
   - Background image or video
   - Product photo
   - Illustration
   - Should support (not distract from) the message

4. **Primary Call-to-Action**
   - Single, prominent button
   - Action-oriented text ("Get Started," "Learn More," "Shop Now")
   - High contrast with background
   - Above the fold

5. **Optional Secondary CTA**
   - Lower emphasis alternative action
   - Text link or secondary button style
   - Example: "Watch Demo" alongside "Start Free Trial"

#### Hero Section Patterns

**Pattern 1: Center-Aligned Hero**
```
┌───────────────────────────────────┐
│                                   │
│        [Compelling Headline]      │
│     [Supporting Subheadline]      │
│                                   │
│      [Primary CTA Button]         │
│     [Secondary CTA Link]          │
│                                   │
│    [Background Image/Video]       │
└───────────────────────────────────┘
```
**Best For:** Clean, focused messaging; software products; services

**Pattern 2: Left-Aligned with Visual**
```
┌───────────────────────────────────┐
│ [Headline]           [Product]    │
│ [Subheadline]        [Image or]   │
│                      [Screenshot] │
│ [Primary CTA]                     │
│ [Secondary CTA]                   │
└───────────────────────────────────┘
```
**Best For:** Product demonstrations; SaaS; apps; e-commerce

**Pattern 3: Full-Screen Background**
```
┌───────────────────────────────────┐
│ :::::::FULL BACKGROUND IMAGE::::::│
│ :::::::::::::::::::::::::::::::::::│
│        [Headline]                 │
│      [Subheadline]                │
│    [Primary CTA Button]           │
│ :::::::::::::::::::::::::::::::::::│
└───────────────────────────────────┘
```
**Best For:** Lifestyle brands; hospitality; events; photography

**Pattern 4: Video Hero**
```
┌───────────────────────────────────┐
│ [Auto-playing background video]   │
│                                   │
│        [Headline]                 │
│      [Subheadline]                │
│    [Primary CTA Button]           │
└───────────────────────────────────┘
```
**Best For:** High-impact brands; products needing demonstration; storytelling

---

### Accessing the Live Editor

After creating your page (as covered in the previous section), you'll use the **Live Editor** to add content to your hero section.

#### Step 1: Open Your Page for Editing

1. From the **Pages** list, find your home page
2. Click on the article number or title to open version history
3. Click **"Edit"** on the latest draft version
4. The Live Editor will open

**Alternative Path:**
- If you just created the page, click **"Edit this page"** button

#### Step 2: Understand the Live Editor Interface

The Live Editor provides a WYSIWYG (What You See Is What You Get) editing experience:

**Key Interface Elements:**

| Element | Location | Purpose |
|---------|----------|---------|
| **Toolbar** | Top of screen | Text formatting, alignment, links, images |
| **Editable Regions** | Outlined in page | Areas with blue dotted borders when hovering |
| **Save Button** | Top right | Save current version (Ctrl+S / Cmd+S) |
| **Preview Button** | Top right | View page as visitors will see it |
| **Publish Button** | Top right | Make page live |

**Editable Regions:**
- Hover over any area to see if it's editable
- Editable areas show a blue dotted outline
- Click to activate editing
- Use toolbar for formatting

---

### Adding Hero Content

#### Step 1: Identify Your Hero Editable Region

Your template should have defined a hero section with one or more editable regions.

**Common Hero Editable Region Patterns:**

**Single Editable Region (Simple):**
```html
<div class="hero-section" data-ccms-ceid="hero-content">
  <!-- All hero content goes here -->
</div>
```

**Multiple Editable Regions (Advanced):**
```html
<div class="hero-section">
  <div data-ccms-ceid="hero-headline">
    <!-- Headline here -->
  </div>
  <div data-ccms-ceid="hero-subheadline">
    <!-- Subheadline here -->
  </div>
  <div data-ccms-ceid="hero-cta">
    <!-- CTA buttons here -->
  </div>
</div>
```

**How to Identify:**
1. Hover over the hero area
2. Look for blue dotted outline indicating editability
3. Click to activate the region

#### Step 2: Add Your Headline

**Best Practices for Headlines:**

✅ **Do:**
- Keep it under 10 words
- Use active, powerful verbs
- Focus on the benefit, not the feature
- Make it specific and concrete
- Test for clarity (would a 10-year-old understand it?)

❌ **Don't:**
- Use jargon or industry buzzwords
- Make vague or generic claims
- Write overly long sentences
- Use passive voice
- Focus solely on yourself ("We do X")

**Good Headline Examples:**

| Industry | Weak Headline | Strong Headline |
|----------|--------------|-----------------|
| **SaaS** | "Innovative Project Management Solutions" | "Ship Projects 2x Faster" |
| **E-commerce** | "Quality Products for Your Home" | "Furniture That Lasts Generations" |
| **Services** | "We Provide Marketing Services" | "Get More Customers in 30 Days" |
| **Nonprofit** | "Helping Communities Worldwide" | "Clean Water for 1 Million People" |

**How to Add the Headline:**

1. Click into the hero headline editable region
2. Type or paste your headline text
3. Highlight the text
4. Use the toolbar to format as **H1** (Heading 1)
5. Apply any additional styling (bold, color, alignment)

**Example:**
```
Headline: Ship Projects 2x Faster
Format: H1, Bold, Center-aligned
```

#### Step 3: Add Your Subheadline

The subheadline provides context and expands on your headline.

**Best Practices for Subheadlines:**

✅ **Do:**
- Elaborate on the headline's benefit
- Keep it to 1-2 sentences (10-20 words)
- Use plain language
- Address the visitor's pain point
- Include emotional appeal

❌ **Don't:**
- Repeat the headline
- Use technical jargon
- Make it too long
- Include multiple ideas

**Good Subheadline Examples:**

| Headline | Subheadline |
|----------|-------------|
| "Ship Projects 2x Faster" | "Collaborative project management for modern teams. No training required." |
| "Furniture That Lasts Generations" | "Handcrafted, sustainable furniture built to be passed down to your children." |
| "Get More Customers in 30 Days" | "Data-driven marketing strategies that actually work. Guaranteed results or your money back." |
| "Clean Water for 1 Million People" | "Join thousands of donors providing safe drinking water to families in need." |

**How to Add the Subheadline:**

1. Click into the hero subheadline editable region (or below headline if single region)
2. Type or paste your subheadline
3. Highlight the text
4. Format as **H2** (Heading 2) or **paragraph** (depending on template design)
5. Adjust alignment to match headline

#### Step 4: Add Supporting Text (Optional)

Some hero sections include additional supporting text or bullet points.

**When to Include:**
- Complex products needing more explanation
- Multiple value propositions to highlight
- Key differentiators that need emphasis

**Example Supporting Content:**
```
✓ No credit card required
✓ Free 14-day trial
✓ Cancel anytime
```

**How to Add:**
1. Click into the appropriate editable region
2. Type your list items
3. Use the toolbar to create a bulleted list
4. Apply checkmark icons if available (depends on template)

---

### Working with Hero Images and Backgrounds

Hero images dramatically impact first impressions. Choose and implement them carefully.

#### Image Best Practices

**Technical Requirements:**

| Aspect | Recommendation | Why |
|--------|----------------|-----|
| **Resolution** | Minimum 1920×1080px | Ensures sharpness on large screens |
| **File Size** | Under 500KB (optimized) | Fast loading, good performance |
| **Format** | JPG (photos), PNG (graphics), WebP (modern) | Balance quality and size |
| **Aspect Ratio** | 16:9 or wider | Fits most hero layouts |
| **Subject Placement** | Center or left | Avoids cutoff on mobile |

**Visual Quality:**

✅ **Do:**
- Use high-quality, professional images
- Ensure text is readable over the image
- Consider color overlay for better text contrast
- Use images that evoke emotion
- Choose images relevant to your message

❌ **Don't:**
- Use generic stock photos (people shaking hands, etc.)
- Overcrowd the image
- Use images with competing focal points
- Forget mobile responsiveness
- Use low-resolution or pixelated images

#### Adding a Hero Background Image

**Method 1: Template-Based Background (Most Common)**

If your template has a predefined hero section with a background, you may need to upload the image through SkyCMS's file manager.

**Steps:**
1. In the Live Editor, look for an **"Upload Image"** or **"Background"** option in the hero section
2. Click the upload button or image placeholder
3. Select your optimized hero image
4. Upload and apply

**Method 2: Using Image Editable Region**

Some templates have an editable region specifically for images.

**Steps:**
1. Click into the hero image editable region
2. Click **"Insert Image"** icon in toolbar
3. Upload your image or select from media library
4. Adjust size and alignment as needed

**Method 3: Inline Style (Advanced)**

If you're comfortable with HTML/CSS, you can add background images via inline styles.

**Example:**
```html
<div style="background-image: url('/path/to/hero-image.jpg'); 
            background-size: cover; 
            background-position: center;">
  <!-- Hero content -->
</div>
```

**Note:** Check if your template supports this approach, as some templates have specific image handling mechanisms.

#### Image Overlay for Text Readability

To ensure text is readable over busy images, add a color overlay.

**Common Overlay Techniques:**

**Dark Overlay (for light text):**
```css
background: linear-gradient(rgba(0,0,0,0.5), rgba(0,0,0,0.5)), 
            url('/path/to/image.jpg');
```

**Light Overlay (for dark text):**
```css
background: linear-gradient(rgba(255,255,255,0.7), rgba(255,255,255,0.7)), 
            url('/path/to/image.jpg');
```

**Gradient Overlay:**
```css
background: linear-gradient(to right, rgba(0,0,0,0.8), rgba(0,0,0,0.3)), 
            url('/path/to/image.jpg');
```

**How to Apply (if supported by template):**
- Some templates have overlay settings in the design editor
- Otherwise, you may need to modify template CSS (Phase 2 guides cover this)

#### Using Video Backgrounds

Video backgrounds create dynamic, engaging hero sections.

**Video Best Practices:**

| Aspect | Recommendation |
|--------|----------------|
| **Duration** | 10-30 second loop |
| **File Size** | Under 5MB |
| **Format** | MP4 (H.264) for compatibility |
| **Audio** | Muted by default (autoplay requirement) |
| **Fallback** | Provide poster image for mobile |
| **Content** | Subtle, not distracting |

**When to Use Video:**
- ✅ Your brand is visual/experiential (hotels, restaurants, travel)
- ✅ Product in action needs to be shown
- ✅ You have high-quality video assets
- ❌ On mobile-only sites (performance concerns)
- ❌ If it distracts from core message

**How to Add Video Background (Template Dependent):**

Check if your template supports video. If so:
1. Upload video to SkyCMS file manager
2. In hero section settings, select video background option
3. Upload your video file
4. Set poster image (fallback for mobile)
5. Test on multiple devices

**Note:** Video backgrounds are an advanced feature. If your template doesn't support them, this may require template customization (covered in Phase 3 advanced topics).

---

### Creating Compelling CTAs

Your Call-to-Action (CTA) button is the most important interactive element in the hero section.

#### CTA Button Best Practices

**Text Guidelines:**

✅ **Effective CTA Text:**
- "Get Started Free"
- "Start Your Free Trial"
- "Book a Demo"
- "Shop Now"
- "Download the Guide"
- "Join 10,000+ Users"

❌ **Weak CTA Text:**
- "Submit"
- "Click Here"
- "Learn More" (too vague)
- "Enter" (unclear)

**CTA Button Design Principles:**

| Principle | Recommendation | Example |
|-----------|----------------|---------|
| **Contrast** | High contrast with background | Orange button on blue background |
| **Size** | Large enough to be obvious | Minimum 44×44px (touch target) |
| **Placement** | Above the fold, centered or left | Below headline/subheadline |
| **Whitespace** | Plenty of breathing room | 20-30px padding around button |
| **Action Color** | Bright, attention-getting | Red, orange, green (test!) |

#### Adding CTA Buttons

**Step 1: Locate CTA Editable Region**

1. In the Live Editor, find the CTA area of your hero section
2. Click to activate the editable region
3. Some templates have pre-styled buttons; others require manual creation

**Step 2: Create Button (Method Depends on Template)**

**Method A: Pre-Styled Button**
1. Click "Insert Button" from toolbar (if available)
2. Enter button text
3. Enter link URL (e.g., `/contact`, `/pricing`, `https://signup.example.com`)
4. Select button style (primary, secondary)
5. Insert

**Method B: Manual HTML Button**
If your template requires HTML:

```html
<a href="/contact" class="btn btn-primary">Get Started Free</a>
```

Or for Bootstrap-based templates:
```html
<a href="/pricing" class="btn btn-lg btn-primary">Start Free Trial</a>
```

**Method C: Using the Link Tool**
1. Type your CTA text ("Get Started Free")
2. Highlight the text
3. Click the **"Link"** icon in the toolbar
4. Enter your destination URL
5. Style the link as a button using template classes

**Step 3: Add Secondary CTA (Optional)**

If you want a secondary action (e.g., "Watch Demo" alongside "Start Trial"):

1. Add text for secondary CTA next to primary button
2. Link it to the appropriate URL
3. Style it differently (text link or secondary button style)
4. Ensure it's visually less prominent than primary CTA

**Example Dual CTA:**
```html
<a href="/signup" class="btn btn-primary btn-lg">Start Free Trial</a>
<a href="/demo" class="btn btn-outline-secondary btn-lg">Watch Demo</a>
```

#### CTA Link Destinations

**Common CTA Destinations:**

| CTA Text | Typical Destination | Purpose |
|----------|---------------------|---------|
| "Get Started" | `/signup` or `/register` | Account creation |
| "Start Free Trial" | `/trial` or `/signup?plan=trial` | Trial signup |
| "Book a Demo" | `/demo` or `/contact?type=demo` | Sales meeting |
| "Shop Now" | `/shop` or `/products` | E-commerce browsing |
| "Learn More" | `/about` or `/features` | Information |
| "Contact Us" | `/contact` | General inquiries |
| "Download" | `/download` or PDF link | Content download |

**Internal vs. External Links:**

- **Internal Links** (same site): Use relative paths (`/contact`, `/pricing`)
- **External Links** (different site): Use full URLs (`https://example.com`)
- **Downloads**: Link directly to file (`/files/whitepaper.pdf`)

**Link Attributes:**

For external links, consider adding:
```html
<a href="https://external-site.com" target="_blank" rel="noopener noreferrer">
  Visit Partner Site
</a>
```

- `target="_blank"` - Opens in new tab
- `rel="noopener noreferrer"` - Security best practice for external links

---

### Making Hero Sections Responsive

Your hero section must look great on all devices—desktop, tablet, and mobile.

#### Responsive Design Principles

**Key Breakpoints:**

| Device | Width | Typical Changes |
|--------|-------|-----------------|
| **Desktop** | 1200px+ | Full-width, side-by-side layouts |
| **Tablet** | 768px - 1199px | Reduced padding, stacked on smaller tablets |
| **Mobile** | < 768px | Single column, larger text, full-width buttons |

#### Mobile Hero Optimization

**Text Adjustments:**

On mobile devices:
- Headline font size should scale down (but remain readable)
- Subheadline may need to be shortened or removed
- Line height should increase for readability
- Text alignment often switches to left (instead of center)

**Example Responsive Typography:**
```css
/* Desktop */
h1 { font-size: 48px; }

/* Tablet */
@media (max-width: 991px) {
  h1 { font-size: 36px; }
}

/* Mobile */
@media (max-width: 767px) {
  h1 { font-size: 28px; }
}
```

**Note:** Your template should handle this automatically if designed responsively. If not, you may need to adjust template CSS (Phase 2 guide).

**Image Adjustments:**

- **Background Position**: On mobile, ensure subject isn't cut off
- **Aspect Ratio**: May need different crops for mobile vs. desktop
- **File Size**: Consider serving smaller images on mobile (advanced)

**CTA Button Adjustments:**

On mobile:
- Buttons should be full-width or nearly full-width
- Increase touch target size (minimum 44×44px)
- Stack multiple buttons vertically
- Increase spacing between buttons

**Example Mobile CTA:**
```css
/* Mobile */
@media (max-width: 767px) {
  .btn {
    display: block;
    width: 100%;
    margin-bottom: 15px;
  }
}
```

#### Testing Responsiveness

**In the Live Editor:**

1. Click **"Preview"** button
2. Resize your browser window to test different widths
3. Check for:
   - Text readability at all sizes
   - Image scaling and positioning
   - Button accessibility
   - Content not overlapping or breaking

**Using Browser DevTools:**

1. Open preview in browser
2. Press **F12** (or **Cmd+Option+I** on Mac)
3. Click **"Toggle Device Toolbar"** icon (or Ctrl+Shift+M)
4. Test multiple device presets:
   - iPhone SE (375px)
   - iPad (768px)
   - Desktop (1920px)

**Real Device Testing:**

- Test on actual mobile devices when possible
- Check both portrait and landscape orientations
- Test on different browsers (Chrome, Safari, Firefox)

---

### Hero Section Checklist

Before moving on, ensure your hero section has:

**Content:**
- [ ] Clear, compelling headline (H1)
- [ ] Supporting subheadline that adds context
- [ ] Primary CTA button with action-oriented text
- [ ] Optional secondary CTA (if appropriate)
- [ ] All text is free of typos and grammatical errors

**Visual:**
- [ ] High-quality background image or visual element
- [ ] Text is readable over background (good contrast)
- [ ] Image is optimized for web (under 500KB)
- [ ] Image subject is properly positioned

**Technical:**
- [ ] All editable regions are properly filled
- [ ] Links point to correct destinations
- [ ] Internal links use relative paths
- [ ] External links have proper attributes (`target`, `rel`)

**Responsive:**
- [ ] Looks good on desktop (1920px)
- [ ] Looks good on tablet (768px)
- [ ] Looks good on mobile (375px)
- [ ] Text is readable at all sizes
- [ ] Buttons are easily tappable on mobile
- [ ] No content overlapping or breaking

**SEO:**
- [ ] Headline uses H1 tag (only one H1 per page)
- [ ] Subheadline uses H2 or paragraph tag
- [ ] Image has descriptive alt text (covered in SEO section later)

---

### Common Hero Section Mistakes

**Mistake 1: Too Much Text**
- **Problem:** Overwhelming visitors with information
- **Solution:** Keep headline under 10 words, subheadline under 20 words

**Mistake 2: Weak or Generic Headline**
- **Problem:** "Welcome to Our Website" or "Quality Service Provider"
- **Solution:** Focus on specific benefit ("Save 10 Hours Per Week")

**Mistake 3: Poor Image Choice**
- **Problem:** Generic stock photos that don't relate to message
- **Solution:** Use authentic, relevant images that support your value proposition

**Mistake 4: Multiple CTAs Fighting for Attention**
- **Problem:** 3-4 equally prominent buttons confuse visitors
- **Solution:** One primary CTA, optional secondary CTA with less emphasis

**Mistake 5: Unreadable Text Over Image**
- **Problem:** Low contrast makes text hard to read
- **Solution:** Add overlay, adjust image, or reposition text

**Mistake 6: Not Mobile-Optimized**
- **Problem:** Text too small, buttons too small, layout breaks
- **Solution:** Test on mobile devices and adjust accordingly

**Mistake 7: Slow Loading**
- **Problem:** Huge image files cause slow page load
- **Solution:** Optimize images (compress to under 500KB)

---

### Save Your Work

Don't forget to save your hero section progress!

**To Save:**
1. Click **"Save"** button in top right
2. Or press **Ctrl+S** (Cmd+S on Mac)
3. Wait for confirmation message
4. A new version is created

**Auto-Save:**
- SkyCMS may have auto-save enabled (check settings)
- Typically saves after 1.5 seconds of inactivity
- Don't rely solely on auto-save—manually save important changes

---

### What's Next?

Congratulations! You've created a compelling hero section for your home page.

**In the Next Section:**
Continue building your home page by adding featured content, service highlights, testimonials, and more engaging sections below the hero.

---

## Additional Resources

- **Template Patterns:** [Phase 3 - Creating Templates](03-Creating-Templates.md)
- **Responsive Design:** [Phase 2 - Creating Layouts](02-Creating-Layouts.md)
- **Live Editor Details:** [Live Editor Guide](../Editors/LiveEditor/README.md)
- **Image Management:** [File Manager Guide](../FileManagement/index.md)

---

## Adding Featured Content Sections

Now that your hero section is complete, it's time to build out the supporting content sections below it. These sections showcase your value proposition, build credibility, and guide visitors toward conversion.

**What You'll Learn:**
- Featured content strategies for different business types
- Service and product highlights
- Testimonials and social proof
- Stats and metrics sections
- Working with content grids and cards

---

### Planning Your Content Sections

Before adding content, decide which sections your home page needs.

#### Common Home Page Sections

| Section Type | Purpose | Best For |
|-------------|---------|----------|
| **Services/Features** | Showcase what you offer | Service businesses, SaaS |
| **Product Highlights** | Feature key products | E-commerce, retail |
| **Benefits** | Explain value propositions | All business types |
| **How It Works** | Explain your process | Complex services, apps |
| **Testimonials** | Build trust | All business types |
| **Stats/Metrics** | Show credibility | Data-driven businesses |
| **Case Studies** | Demonstrate results | B2B, consulting |
| **Team/About** | Humanize your brand | Small businesses, personal brands |
| **Latest News/Blog** | Show activity | Content-driven sites |
| **Partners/Clients** | Display associations | B2B, enterprise |

**Typical Section Order:**
```
Hero Section
    ↓
Value Proposition / Benefits
    ↓
Services / Products / Features (3-6 items)
    ↓
How It Works / Process (3-4 steps)
    ↓
Social Proof (Testimonials / Stats / Logos)
    ↓
Final CTA
```

---

### Creating Service/Feature Highlights

Service highlights showcase what you offer in an easily scannable format.

#### Best Practices for Service Sections

**Layout Options:**

**3-Column Grid (Most Common):**
```
┌──────────┬──────────┬──────────┐
│  Icon    │  Icon    │  Icon    │
│  Title   │  Title   │  Title   │
│  Text    │  Text    │  Text    │
└──────────┴──────────┴──────────┘
```
**Best For:** 3-6 services or features

**Card Layout:**
```
┌─────────────────┐ ┌─────────────────┐
│  [Image]        │ │  [Image]        │
│  Title          │ │  Title          │
│  Description    │ │  Description    │
│  [Learn More]   │ │  [Learn More]   │
└─────────────────┘ └─────────────────┘
```
**Best For:** Visual services with images

**Content Guidelines:**

✅ **Do:**
- Limit to 3-6 items (odd numbers often look better)
- Keep titles short (2-5 words)
- Keep descriptions brief (15-30 words)
- Use parallel structure (all start with verb, etc.)
- Include relevant icons or images

❌ **Don't:**
- List more than 6 items (overwhelming)
- Write long paragraphs
- Use inconsistent formatting
- Forget mobile responsiveness

#### Adding Service Highlights

**Step 1: Locate the Service Section**

1. In the Live Editor, scroll below your hero section
2. Find the editable region for services/features
3. Click to activate editing

**Step 2: Add Section Heading**

1. Type your section heading
2. Format as **H2**: "Our Services," "What We Offer," "Key Features"
3. Optionally add a subheading (H3 or paragraph)

**Example:**
```
H2: Our Services
Paragraph: Everything you need to grow your business online
```

**Step 3: Add Individual Services**

Your template likely has a grid or card structure. For each service:

1. **Add Icon or Image** (if template supports it)
   - Click image placeholder
   - Upload icon or service image
   - Keep images consistent size

2. **Add Service Title (H3)**
   - Clear, descriptive (2-4 words)
   - Action-oriented when possible
   
3. **Add Description**
   - 15-30 words
   - Focus on benefit, not feature
   - Plain language

4. **Add Link (Optional)**
   - "Learn More" → links to detail page
   - Use relative path (`/services/web-design`)

**Example Service Item:**
```
Icon: 🎨 (or graphic)
Title (H3): Web Design
Description: Beautiful, responsive websites that convert visitors into customers. Mobile-first designs that work everywhere.
Link: Learn More → /services/web-design
```

---

### Adding Testimonials and Social Proof

Testimonials build trust and credibility. They're one of the most effective conversion elements.

#### Testimonial Best Practices

**What Makes a Good Testimonial:**

✅ **Effective Testimonials:**
- Specific results ("Increased sales by 150%")
- Real names and photos
- Company/title for credibility
- 2-4 sentences (50-100 words)
- Focus on transformation or outcome

❌ **Weak Testimonials:**
- Generic praise ("Great company!")
- Anonymous or "John D."
- Too long (full paragraphs)
- Only features, no outcomes

**Testimonial Formats:**

**Format 1: Quote with Attribution**
```
"[Quote text]"
— Name, Title/Company
```

**Format 2: Card with Photo**
```
┌─────────────────────────┐
│  [Photo]  "Quote"       │
│           Name          │
│           Company       │
└─────────────────────────┘
```

**Format 3: Star Rating + Quote**
```
★★★★★
"Quote text"
— Name, Company
```

#### Adding Testimonials

**Step 1: Locate Testimonial Section**

1. Scroll to the testimonial area in Live Editor
2. Click into the editable region

**Step 2: Add Section Heading**

```
H2: What Our Clients Say
or: Customer Success Stories
or: Trusted by Thousands
```

**Step 3: Add Individual Testimonials**

For each testimonial:

1. **Add Quote**
   - 2-4 sentences
   - Include specific result or benefit
   - Use quotation marks

2. **Add Attribution**
   - Full name
   - Title and company
   - Format: "— Name, Title at Company"

3. **Add Photo (if supported)**
   - Professional headshot
   - 100x100px to 200x200px
   - Circular crop often works well

**Example Testimonial:**
```
"SkyCMS helped us launch our website in just 2 weeks. The intuitive editor made content updates a breeze, and our traffic increased by 40% in the first month."

— Sarah Johnson, Marketing Director at TechStart Inc.
```

---

### Adding Stats and Metrics

Numbers build credibility and prove your impact.

#### Effective Stat Sections

**Common Stat Types:**

| Stat Category | Examples |
|--------------|----------|
| **Scale** | "10,000+ Customers," "50+ Countries" |
| **Performance** | "99.9% Uptime," "2x Faster Loading" |
| **Experience** | "15 Years in Business," "500+ Projects" |
| **Satisfaction** | "4.9/5 Stars," "98% Satisfaction Rate" |
| **Results** | "150% ROI," "$5M Saved for Clients" |

**Layout Pattern:**
```
┌────────┬────────┬────────┬────────┐
│ 10,000+│  99.9% │   24/7 │  4.9★  │
│ Users  │ Uptime │Support │ Rating │
└────────┴────────┴────────┴────────┘
```

#### Adding Stats

**Step 1: Choose Your Stats**

Select 3-4 impressive, specific numbers:
- ✅ "10,000+ Happy Customers"
- ✅ "99.9% Uptime Guarantee"
- ✅ "24/7 Customer Support"
- ❌ "Many satisfied clients" (too vague)

**Step 2: Add to Page**

1. Locate stats section in Live Editor
2. For each stat:
   - **Big Number/Visual**: Large, bold text
   - **Label**: Small text explaining the number
   
**Example:**
```
[Large Text] 10,000+
[Small Text] Happy Customers

[Large Text] 99.9%
[Small Text] Uptime Guarantee
```

**Step 3: Format for Impact**

- Make numbers very large (2-3x normal text)
- Use bold formatting
- Keep labels short (2-3 words)
- Ensure mobile responsiveness

---

### Content Section Checklist

Before moving on, ensure your content sections have:

**Structure:**
- [ ] Clear H2 headings for each section
- [ ] Logical flow from hero to final CTA
- [ ] 3-6 items per section (not overwhelming)
- [ ] Consistent formatting throughout

**Content Quality:**
- [ ] Specific, concrete language
- [ ] Benefits over features
- [ ] Real testimonials with names
- [ ] Accurate stats and numbers
- [ ] No typos or errors

**Visual:**
- [ ] Icons or images where appropriate
- [ ] Consistent visual style
- [ ] Good spacing between sections
- [ ] Mobile-responsive layout

**Links:**
- [ ] "Learn More" links work correctly
- [ ] Internal links use relative paths
- [ ] All links point to existing pages

---

### Save Your Progress

Remember to save after adding each section:
- Click **Save** or press **Ctrl+S** (Cmd+S)
- Preview to check appearance
- Test on mobile devices

---

### What's Next?

You've built out the main content sections of your home page. In the next section, you'll learn about CTAs, navigation integration, and final touches before publishing.

---

## Final Call-to-Action Sections

Beyond the hero CTA, your home page needs a final call-to-action section that encourages visitors to convert after learning about your offerings.

### Purpose of Final CTA

The final CTA appears after all your value propositions and social proof:

**Why It's Important:**
- Last chance to convert visitors before they leave
- Visitors are now informed about your offerings
- Reinforces the action you want them to take

**Common Placements:**
- After testimonials section
- Before the footer
- As a distinct colored section (background color for emphasis)

### Final CTA Design Patterns

**Pattern 1: Simple Centered CTA**
```
┌─────────────────────────────┐
│   Ready to Get Started?     │
│   [Large CTA Button]        │
└─────────────────────────────┘
```
**Best For:** Simple, direct conversion

**Pattern 2: CTA with Supporting Text**
```
┌─────────────────────────────┐
│   Ready to Transform Your   │
│      Business?              │
│   Join 10,000+ customers    │
│   [Primary Button]          │
│   [Secondary Link]          │
└─────────────────────────────┘
```
**Best For:** Providing context and reducing hesitation

**Pattern 3: Two-Column CTA**
```
┌──────────────┬──────────────┐
│  Have        │  Ready to    │
│  Questions?  │  Buy?        │
│  [Contact]   │  [Start Now] │
└──────────────┴──────────────┘
```
**Best For:** Offering choice based on readiness

### Adding Your Final CTA

**Step 1: Locate CTA Section**

1. Scroll to bottom of page (before footer)
2. Find final CTA editable region
3. Click to activate

**Step 2: Add Headline**

```
Examples:
- "Ready to Get Started?"
- "Start Your Free Trial Today"
- "Let's Build Something Amazing Together"
- "Join Thousands of Happy Customers"
```

Format as H2, center-aligned

**Step 3: Add Supporting Text (Optional)**

Brief reinforcement (1-2 sentences):
- "No credit card required. Cancel anytime."
- "Free 14-day trial with full features."
- "Get started in less than 5 minutes."

**Step 4: Add CTA Button**

1. Type button text
2. Highlight and create link
3. Point to conversion page (`/signup`, `/contact`, `/demo`)
4. Style as prominent button

---

## Verifying Navigation and Footer

Your Site Design (layout) should already include navigation and footer, but verify they're working correctly.

### Navigation Checklist

Your navigation should already be defined in your Site Design from Phase 2. Verify:

**Header Navigation:**
- [ ] Logo links to home page (`/`)
- [ ] Main menu items are present
- [ ] Links point to correct pages
- [ ] Mobile menu works (if responsive)
- [ ] Active page is highlighted (if supported)

**Common Navigation Issues:**

| Issue | Solution |
|-------|----------|
| Links to pages that don't exist yet | Note which pages to create in Phase 5 |
| Broken internal links | Update links in Site Design (Phase 2) |
| Navigation not visible | Check Site Design is applied to page |
| Mobile menu not working | Review Site Design responsive code |

**Note:** Navigation is managed in the Site Design (layout), not individual pages. If you need to change navigation, return to Phase 2 guides.

### Footer Verification

Your footer is also part of the Site Design. Verify it includes:

**Essential Footer Elements:**
- [ ] Copyright notice with current year
- [ ] Contact information (email, phone, address)
- [ ] Links to key pages (About, Contact, Services)
- [ ] Social media links (if applicable)
- [ ] Privacy Policy and Terms links (if applicable)

**Common Footer Content:**

| Section | Typical Content |
|---------|----------------|
| **Company Info** | Logo, tagline, brief description |
| **Quick Links** | Home, About, Services, Contact |
| **Legal** | Privacy Policy, Terms of Service, Cookie Policy |
| **Contact** | Email, phone, address, hours |
| **Social** | Facebook, Twitter, LinkedIn, Instagram icons |
| **Newsletter** | Email signup form (optional) |

**Footer Best Practices:**
- Keep it organized in columns (3-4 columns on desktop)
- Make links easy to find
- Include copyright with current year
- Test all links work correctly
- Ensure mobile-friendly layout

---

## Internal Linking Strategy

Strategic internal links throughout your home page improve navigation and SEO.

### Where to Add Internal Links

**Within Content Sections:**
- Service descriptions → Link to detailed service pages
- Product highlights → Link to product detail pages
- Testimonials → Link to case studies or full stories
- Stats → Link to "About Us" or methodology page

### Link Best Practices

✅ **Good Internal Links:**
- Descriptive anchor text ("Learn about web design services")
- Natural flow in content
- Links to relevant pages
- Opens in same window/tab

❌ **Poor Internal Links:**
- Generic anchor text ("Click here," "Read more")
- Too many links (overwhelming)
- Links to irrelevant content
- Broken links

**Example Integration:**
```
Instead of:
"We offer web design. Click here to learn more."

Use:
"Our web design services help businesses create stunning, 
mobile-responsive websites that drive conversions."
(where "web design services" links to /services/web-design)
```

---

## CTAs & Navigation Checklist

Before moving to the next section, verify:

**Final CTA:**
- [ ] Compelling headline that reinforces action
- [ ] Clear, action-oriented button text
- [ ] Button links to correct destination
- [ ] Supporting text reduces friction (if applicable)
- [ ] Visually distinct from rest of page

**Navigation:**
- [ ] All header nav links work correctly
- [ ] Logo links to home page
- [ ] Mobile navigation functions properly
- [ ] Active page highlighted (if applicable)

**Footer:**
- [ ] All footer links tested and working
- [ ] Contact information is accurate
- [ ] Copyright year is current
- [ ] Social media links work (if applicable)
- [ ] Legal pages linked (Privacy, Terms)

**Internal Links:**
- [ ] Service/product descriptions link to detail pages
- [ ] Descriptive anchor text (not "click here")
- [ ] Links open in same tab (unless external)
- [ ] All internal links use relative paths

---

### Save and Preview

Save your work and preview the complete page:

1. Click **Save** (or Ctrl+S / Cmd+S)
2. Click **Preview** to view full page
3. Scroll through entire page
4. Click all CTAs and links to verify
5. Test navigation menu
6. Check footer links

---

### What's Next?

Your home page now has compelling content, clear CTAs, and working navigation. In the next section, you'll optimize the page for search engines with proper SEO basics.

---

## SEO Basics for Your Home Page

Search Engine Optimization (SEO) helps your home page rank well in search results and attract organic traffic.

**What You'll Learn:**
- Setting page title and meta description
- Proper heading hierarchy (H1, H2, H3)
- Adding alt text to images
- URL structure best practices
- Basic SEO checklist

**Why SEO Matters:**
- 53% of website traffic comes from organic search
- Home pages are often the highest-ranking pages
- Proper SEO improves visibility and credibility
- Good SEO practices improve user experience

---

### Page Title and Meta Description

The page title and meta description are what appear in search results.

#### Page Title (Title Tag)

**What It Is:**
The text that appears in browser tabs and search results as the blue clickable link.

**Best Practices:**

| Guideline | Recommendation |
|-----------|----------------|
| **Length** | 50-60 characters (longer gets truncated) |
| **Format** | Primary Keyword - Brand Name |
| **Keywords** | Include main keyword near the beginning |
| **Uniqueness** | Different from all other pages |
| **Accuracy** | Match page content |

**Examples:**

| Industry | Poor Title | Good Title |
|----------|-----------|------------|
| **SaaS** | "Home" | "Project Management Software - TeamFlow" |
| **E-commerce** | "Welcome" | "Handcrafted Furniture - Sustainable Wood Designs" |
| **Services** | "Main Page" | "Web Design Services - Portland, OR \| DesignCo" |
| **Nonprofit** | "Homepage" | "Clean Water for Africa - WaterAid Foundation" |

#### Meta Description

**What It Is:**
The brief summary that appears below the title in search results.

**Best Practices:**

| Guideline | Recommendation |
|-----------|----------------|
| **Length** | 150-160 characters |
| **Content** | Compelling summary with benefit |
| **Keywords** | Include naturally (not stuffing) |
| **Call-to-Action** | Encourage clicks ("Learn more," "Get started") |
| **Uniqueness** | Different from other pages |

**Examples:**

| Industry | Meta Description |
|----------|------------------|
| **SaaS** | "Ship projects 2x faster with TeamFlow. Collaborative project management designed for modern teams. Start your free 14-day trial today—no credit card required." |
| **E-commerce** | "Discover handcrafted furniture built to last generations. Sustainable hardwood designs for your home. Free shipping on orders over $500. Shop our collection." |
| **Services** | "Award-winning web design services in Portland, OR. We create beautiful, responsive websites that convert visitors into customers. Get your free quote today." |
| **Nonprofit** | "Help provide clean water to 1 million people in Africa. Join thousands of donors making a difference. 100% of donations go directly to water projects. Donate now." |

#### Setting Title and Description in SkyCMS

**Step 1: Access Page Settings**

1. In the Live Editor, look for page settings or properties
2. Or go to Pages list → Click article number → Edit page properties

**Step 2: Enter Page Title**

1. Find "Page Title" or "Title" field
2. Enter your optimized title (50-60 characters)
3. Avoid using the site name if it's automatically appended

**Step 3: Enter Meta Description**

1. Find "Meta Description" or "Description" field
2. Enter your compelling description (150-160 characters)
3. Include benefit and call-to-action

**Step 4: Save Changes**

Click **Save** to apply changes

---

### Proper Heading Hierarchy

Headings structure your content for both users and search engines.

#### The Heading Structure

**Heading Levels:**

| Tag | Purpose | Usage on Home Page |
|-----|---------|-------------------|
| **H1** | Main page heading | One only - your primary headline |
| **H2** | Major sections | Service sections, About, Testimonials |
| **H3** | Subsections | Individual services, features |
| **H4-H6** | Minor headings | Rarely needed on home pages |

**Hierarchy Example:**
```
H1: Ship Projects 2x Faster
  H2: Our Services
    H3: Project Management
    H3: Team Collaboration
    H3: Time Tracking
  H2: What Our Clients Say
    H3: "Increased productivity by 150%"
  H2: Ready to Get Started?
```

**Critical Rules:**

✅ **Do:**
- One H1 per page (your main headline)
- Use H2 for main sections
- Use H3 for subsections within H2
- Follow sequential order (don't skip levels)
- Make headings descriptive

❌ **Don't:**
- Multiple H1 tags
- Skip levels (H1 → H3 without H2)
- Use headings just for styling
- Make headings too generic ("Section 1")
- Stuff headings with keywords unnaturally

---

### Alt Text for Images

Alt text describes images for screen readers and search engines.

#### Why Alt Text Matters

**Benefits:**
- **Accessibility**: Screen readers describe images to visually impaired users
- **SEO**: Search engines use alt text to understand images
- **Fallback**: Displays if image fails to load
- **Image Search**: Helps images appear in Google Image Search

#### Writing Good Alt Text

**Best Practices:**

✅ **Effective Alt Text:**
- Describe what's in the image specifically
- Include context if relevant
- 5-15 words typically
- Include keywords naturally (don't stuff)
- Describe function if it's a button/link

❌ **Poor Alt Text:**
- "Image" or "Picture"
- "IMG_12345.jpg"
- Keyword stuffing
- Overly long descriptions
- Starting with "Image of..." (implied)

**Examples:**

| Image Type | Poor Alt Text | Good Alt Text |
|------------|---------------|---------------|
| **Hero Image** | "hero-bg.jpg" | "Modern office team collaborating on project" |
| **Product** | "product" | "Handcrafted oak dining table with six chairs" |
| **Team Photo** | "image" | "Sarah Johnson, CEO, speaking at conference" |
| **Icon** | "icon" | "24/7 customer support available" |
| **Logo** | "logo" | "TeamFlow project management software" |

#### Adding Alt Text in SkyCMS

**Step 1: Enter Edit Mode**

Click **Edit** in the upper-right corner.

**Step 2: Click on Image**

Click the image you want to add alt text to.

**Step 3: Access Image Properties**

Depending on your template:
- Look for **Properties** or **Settings** icon
- Or right-click → **Image Properties**

**Step 4: Add Alt Text**

In the image properties dialog:
1. Find the **Alternative Text** or **Alt Text** field
2. Type your descriptive alt text (5-15 words)
3. Click **OK** or **Apply**

**Step 5: Repeat for All Images**

Repeat for every image on your home page (hero, features, testimonials, etc.).

**Step 6: Save Changes**

Click **Save** in the upper-right corner.

---

### URL Structure

Your home page URL should be clean and meaningful.

#### Best Practices for URLs

✅ **Good URL Practices:**
- Use root path `/` for home page (already done!)
- Lowercase letters only
- Use hyphens for spaces (not underscores)
- Keep URLs short and descriptive
- Avoid special characters (?&%#)
- No date stamps unless blog
- Include primary keyword when possible

❌ **Poor URL Practices:**
- /index.html or /home.html
- /Page_1.aspx
- Uppercase letters
- Special characters
- Query strings (?id=123)

**Examples:**

| Page Type | Poor URL | Good URL |
|-----------|----------|----------|
| **Home** | `/index.html` | `/` |
| **About** | `/about_us.html` | `/about` |
| **Services** | `/Services/Page1.aspx` | `/services/web-design` |
| **Contact** | `/contact_us.php` | `/contact` |

#### Verifying Your Home Page URL

**Check in SkyCMS:**

1. Navigate to **Pages** menu
2. Find your home page in the list
3. Verify the **URL** column shows `/`
4. If not, you may need to update it (see note below)

> **Note:** The home page URL should always be `/` (root). If you created your home page with a different URL, you may need to recreate it or ask your administrator to update the routing configuration.

---

### SEO Checklist

Before moving to testing and publishing, verify all SEO elements.

#### Complete SEO Checklist

**Page Title:**
- [ ] Title is 50-60 characters
- [ ] Includes primary keyword naturally
- [ ] Includes brand name
- [ ] Format: `Primary Keyword | Brand Name` or similar
- [ ] No keyword stuffing

**Meta Description:**
- [ ] Description is 150-160 characters
- [ ] Summarizes page value proposition
- [ ] Includes call-to-action
- [ ] Compelling and click-worthy
- [ ] Natural keyword usage

**Heading Structure:**
- [ ] Exactly one H1 tag (page title)
- [ ] H1 includes primary keyword
- [ ] H2s organize main sections
- [ ] H3s used for subsections
- [ ] No skipped levels (H1→H3 without H2)
- [ ] Headings describe content (not generic)

**Images:**
- [ ] All images have alt text
- [ ] Alt text is descriptive (5-15 words)
- [ ] Alt text includes context/keywords naturally
- [ ] No "image of..." prefix
- [ ] Functional images describe purpose

**URL:**
- [ ] Home page uses `/` path
- [ ] URL is clean (no ?id= or special characters)
- [ ] Lowercase only
- [ ] Matches site structure

**Content Quality:**
- [ ] Keyword appears naturally in first paragraph
- [ ] Content answers user intent
- [ ] Links open in appropriate windows
- [ ] All links work (test before publishing)
- [ ] Content is unique (not copied)

**Final Verification:**
- [ ] Save all changes in Edit mode
- [ ] Preview page to check appearance
- [ ] All elements above completed

> **Tip:** Print this checklist or keep it handy when building pages. Good SEO is a habit, not an afterthought!

---

## 8. Testing and Publishing Your Home Page

Before making your home page live, thorough testing ensures everything works correctly and looks professional.

### What You'll Learn

- How to use the Preview functionality
- Responsive design testing techniques
- Browser compatibility verification
- Link validation methods
- Publishing workflow (Draft → Published)
- Troubleshooting common issues

**Estimated Time:** 30-45 minutes

---

### Using Preview Functionality

The Preview button lets you see how your page looks before publishing.

#### How to Preview

**Step 1: Save Your Changes**

Always save before previewing:
1. Click **Save** in the upper-right corner
2. Wait for confirmation that changes are saved

**Step 2: Click Preview**

1. Click the **Preview** button (next to Edit/Save)
2. Page opens in preview mode (non-editable view)
3. This shows exactly how visitors will see the page

**Step 3: What to Check**

While in Preview mode, verify:

**Visual Elements:**
- [ ] Hero section displays correctly
- [ ] Images load properly (no broken images)
- [ ] Text is readable (contrast, size)
- [ ] Spacing looks balanced (not too cramped/sparse)
- [ ] Colors match your brand
- [ ] All sections visible without scrolling horizontally

**Content:**
- [ ] No typos or grammatical errors
- [ ] Headings use correct hierarchy
- [ ] CTAs are clear and compelling
- [ ] Contact information is accurate
- [ ] All text is in the correct editable regions

**Navigation:**
- [ ] Header navigation visible and styled
- [ ] Footer links present and organized
- [ ] Logo clickable (returns to home)
- [ ] Menu items in correct order

---

### Responsive Design Testing

Your home page must look good on all devices (desktop, tablet, mobile).

#### Using Browser DevTools

**Chrome DevTools (F12 or Ctrl+Shift+I):**

**Step 1: Open DevTools**

1. While viewing your page, press **F12** (or **Ctrl+Shift+I**)
2. Click the **Toggle Device Toolbar** icon (phone/tablet icon)
3. Or press **Ctrl+Shift+M** (Windows) or **Cmd+Shift+M** (Mac)

**Step 2: Test Device Presets**

Select different devices from the dropdown:

| Device Type | Screen Width | What to Check |
|-------------|--------------|---------------|
| **Mobile** (iPhone SE) | 375px | Text readable, buttons tappable, no horizontal scroll |
| **Mobile** (iPhone 12 Pro) | 390px | Same as above |
| **Tablet** (iPad) | 768px | Layout uses space well, images scale properly |
| **Tablet** (iPad Pro) | 1024px | Transition to desktop layout smooth |
| **Desktop** | 1920px | Full design visible, content not stretched |

**Step 3: Test Orientation**

Click the **Rotate** icon to test landscape/portrait:
- Mobile landscape: Navigation still works, content visible
- Tablet portrait: Layout adapts appropriately

#### What to Verify on Each Device

**Mobile (375px - 428px):**
- [ ] Hero text readable (not too small)
- [ ] Buttons large enough to tap (min 44×44px)
- [ ] No horizontal scrolling
- [ ] Images scale correctly (don't overflow)
- [ ] Navigation accessible (hamburger menu works)
- [ ] Forms usable (inputs not too small)
- [ ] Spacing adequate (not cramped)

**Tablet (768px - 1024px):**
- [ ] 2-column layouts work well
- [ ] Images appropriate size
- [ ] Text not stretched too wide
- [ ] Touch targets still adequate
- [ ] Whitespace balanced

**Desktop (1200px+):**
- [ ] Full design visible
- [ ] Content centered or appropriately aligned
- [ ] Large images look crisp
- [ ] Hover states work (on CTAs, links)
- [ ] Multi-column layouts effective

---

### Browser Compatibility Testing

Test in multiple browsers to ensure consistent experience.

#### Minimum Browser Testing

| Browser | Priority | What to Check |
|---------|----------|---------------|
| **Chrome** | High | Primary browser for most users (60%+ market share) |
| **Safari** | High | iOS/Mac users (15-20% market share) |
| **Firefox** | Medium | Alternative browser users (5-10% market share) |
| **Edge** | Medium | Windows default browser (5-10% market share) |

#### Quick Testing Checklist

**For Each Browser:**

- [ ] Page loads without errors
- [ ] All images display correctly
- [ ] Fonts render as expected
- [ ] Colors accurate (no color shifts)
- [ ] Layout maintains structure
- [ ] Buttons and links work
- [ ] Animations smooth (if used)
- [ ] No console errors (F12 → Console tab)

#### Common Browser Issues

| Issue | Likely Cause | Quick Fix |
|-------|-------------|-----------|
| Font looks different | Font not loading | Check font CDN links in layout |
| Images broken | Path issues | Verify File Manager URLs |
| Layout broken | CSS compatibility | Check browser DevTools for errors |
| Buttons not working | JavaScript error | Check console for errors |

> **Tip:** Chrome DevTools can emulate other browsers' rendering engines, but always test in real browsers for final verification.

---

### Link Validation

Verify all links work correctly before publishing.

#### Links to Test

**Call-to-Action Buttons:**
- [ ] Hero CTA button works
- [ ] Feature/service "Learn More" links work
- [ ] Final CTA button works
- [ ] All buttons go to correct destinations

**Navigation Links:**
- [ ] Header navigation items
- [ ] Logo link (should go to `/`)
- [ ] Footer navigation links
- [ ] Social media links (if present)

**Internal Links:**
- [ ] Links to other pages (About, Services, Contact)
- [ ] Anchor links (if used for same-page navigation)
- [ ] Breadcrumb links (if present)

#### Testing Methods

**Manual Click Testing:**

1. Click every link on the page
2. Verify it goes to the correct destination
3. Check that it opens in appropriate window:
   - Internal links: Same window/tab
   - External links: New tab (if desired)
   - Downloads: Download dialog opens

**What to Look For:**

❌ **Broken Links (404 errors):**
- Page not found errors
- Misspelled URLs
- Links to pages that don't exist yet

❌ **Incorrect Destinations:**
- Link goes to wrong page
- Anchor link doesn't scroll to correct section
- External link goes to wrong site

✅ **Working Links:**
- Page loads successfully
- Correct page/section displayed
- No error messages

> **Tip:** If you have links to pages not yet created (e.g., "/about"), either create placeholder pages or remove those links until pages are ready.

---

### Publishing Your Home Page

Once testing is complete, publish your home page to make it live.

#### Understanding Page States

| State | Description | Who Can See It |
|-------|-------------|----------------|
| **Draft** | Work in progress, not published | Only Admins and Editors |
| **Published** | Live on the website | Everyone (public) |

**Key Points:**
- New pages start as **Draft**
- Publishing makes page visible to public
- You can unpublish to take page offline
- Draft changes don't affect published version

#### How to Publish

**Step 1: Final Save**

1. Make sure all changes are saved
2. Click **Save** if needed

**Step 2: Exit Edit Mode**

1. If in Edit mode, exit to view mode
2. You should see the page in preview/read-only mode

**Step 3: Publish the Page**

1. Look for the **Publish** button (upper-right area)
2. Click **Publish**
3. Confirm if prompted

**Step 4: Verify Publication**

After publishing:
- [ ] Page status shows "Published"
- [ ] Published date/time displayed
- [ ] Page accessible at your URL (e.g., `yoursite.com/`)
- [ ] Test in incognito/private window (ensures public can see it)

#### Draft vs. Published: What You Need to Know

**Making Changes After Publishing:**

When you edit a published page:
1. Changes are saved to **Draft** version
2. Published version remains unchanged
3. You must **Publish** again to make changes live

**This means:**
- You can safely work on improvements without affecting live page
- Preview shows Draft version
- Public sees Published version
- "Publish" button updates live page with Draft changes

**Version History:**

SkyCMS maintains version history:
- Each publish creates a new version
- You can revert to previous versions if needed
- Access via the version history icon (if available)

---

### Troubleshooting Common Issues

#### Display and Layout Problems

| Problem | Possible Cause | Solution |
|---------|---------------|----------|
| **Images not showing** | Incorrect File Manager path | Re-upload image, copy correct URL from File Manager |
| **Broken layout on mobile** | CSS not responsive | Check template's responsive CSS, test at various widths |
| **Text overflowing container** | Content too long for fixed-width element | Adjust template CSS or shorten content |
| **Sections overlapping** | CSS positioning issues | Check template code, adjust margins/padding |
| **Colors wrong** | Browser cache | Clear cache (Ctrl+Shift+R), check template CSS |

#### Functionality Problems

| Problem | Possible Cause | Solution |
|---------|---------------|----------|
| **Links not working** | Incorrect URL format | Check URL starts with `/` for internal, `http://` or `https://` for external |
| **CTA button not clickable** | Missing link/href | Add href attribute to button/link in template |
| **Navigation missing** | Not part of template | Check layout file, ensure navigation included |
| **Page not found (404)** | Wrong URL or not published | Verify URL in Pages list, ensure page is Published |

#### Content and SEO Problems

| Problem | Possible Cause | Solution |
|---------|---------------|----------|
| **Page title not showing** | Not set in page properties | Go to page properties, add title (50-60 chars) |
| **Content not editable** | Not in editable region | Template needs `data-ccms-ceid` attributes on sections |
| **Changes not appearing** | Viewing cached version | Clear browser cache, publish changes again |
| **Images look blurry** | Image too small | Upload higher resolution image (min 1920px wide for hero) |

#### When to Get Help

Contact your administrator or check documentation if:
- Template structure needs modification
- Database/server errors occur
- Permissions issues prevent publishing
- Systematic issues across multiple pages
- Need to revert to previous version

---

### Testing and Publishing Checklist

Before marking your home page complete, verify everything:

**Preview and Appearance:**
- [ ] Saved all changes in Edit mode
- [ ] Previewed page (looks professional)
- [ ] All sections visible and styled correctly
- [ ] No broken images or missing content
- [ ] Text readable and error-free

**Responsive Design:**
- [ ] Tested on mobile (375px width)
- [ ] Tested on tablet (768px width)
- [ ] Tested on desktop (1200px+ width)
- [ ] No horizontal scrolling on any size
- [ ] All content accessible on small screens

**Browser Compatibility:**
- [ ] Tested in Chrome
- [ ] Tested in Safari (if Mac/iOS users expected)
- [ ] Tested in Firefox
- [ ] Tested in Edge
- [ ] No console errors in any browser

**Link Validation:**
- [ ] All CTA buttons work
- [ ] Header navigation links work
- [ ] Footer links work
- [ ] External links open in new tab (if intended)
- [ ] No 404 errors

**SEO Verification:**
- [ ] Page title set (50-60 characters)
- [ ] Meta description set (150-160 characters)
- [ ] Exactly one H1 tag
- [ ] Proper heading hierarchy (H1 → H2 → H3)
- [ ] All images have alt text
- [ ] URL is `/` (root path)

**Publishing:**
- [ ] Page published (not Draft)
- [ ] Verified live URL works
- [ ] Tested in incognito/private window
- [ ] Published date/time shows correctly

---

### What's Next?

✅ **Congratulations!** Your home page is live and looks great.

**Next Steps:**

1. **Build Additional Pages** (Phase 5)
   - Create About page
   - Build service/product pages
   - Add contact page
   
2. **Refine and Optimize**
   - Monitor analytics (if integrated)
   - Gather feedback
   - Make iterative improvements
   
3. **Prepare for Handoff** (Phase 6)
   - Set up content creator roles
   - Create documentation
   - Provide training

**See Also:**
- [Phase 5: Building Initial Pages](05-Building-Initial-Pages.md) *(coming soon)*
- [Phase 6: Preparing for Handoff](06-Preparing-for-Handoff.md) *(coming soon)*
- [Pre-Launch Checklist](./Checklists/Pre-Launch-Checklist.md) *(coming soon)*

---

*Need help? Check the [main Website Launch Workflow](Website-Launch-Workflow.md) or visit [SkyCMS Documentation](https://www.moonrise.net/cosmos/documentation/).*
