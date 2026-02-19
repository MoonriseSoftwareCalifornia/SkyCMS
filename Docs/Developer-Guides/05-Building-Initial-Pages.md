---
title: Phase 5 - Building Initial Pages
description: Content pages, page hierarchy, internal linking, navigation setup, and page relationships
keywords: pages, hierarchy, linking, navigation, content, phase-5
audience: [developers, content-creators]
---

# Phase 5: Building Initial Pages

## Introduction

With your home page published, it's time to build the essential content pages that complete your website's core structure. This phase focuses on creating the foundational pages visitors expect to find: About, Services/Products, Contact, and any other key pages identified in your sitemap.

### What This Guide Covers

This guide walks you through:
- Planning your initial page structure
- Understanding page hierarchy and URLs
- Creating an About page with compelling content
- Building service or product pages
- Setting up contact pages
- Establishing internal linking
- Configuring navigation menus
- Best practices for content pages

### Prerequisites

Before starting this phase, ensure you have:

- [x] **Completed Phase 4**: Home page published and live
- [x] **Sitemap Ready**: Approved sitemap from Phase 1
- [x] **Templates Available**: Page templates created in Phase 3
- [x] **Content Prepared**: Draft content for each page
- [x] **Brand Assets**: Images, logos, and other media ready
- [x] **Admin/Editor Access**: Permissions to create and publish pages

### Learning Objectives

By the end of this phase, you will:

✅ Understand how to structure pages hierarchically  
✅ Create professional About, Service, and Contact pages  
✅ Implement effective internal linking strategies  
✅ Configure navigation menus correctly  
✅ Apply SEO best practices to all pages  
✅ Maintain consistency across your site  

### Estimated Time

**Total Time:** 4-6 hours (depending on number of pages)

| Task | Time |
|------|------|
| Planning page structure | 30 minutes |
| Creating About page | 45-60 minutes |
| Creating service/product pages | 1-2 hours |
| Creating contact page | 30-45 minutes |
| Setting up navigation | 30 minutes |
| Internal linking | 30 minutes |
| Testing and publishing | 45 minutes |

---

## 1. Planning Your Page Structure

Before creating pages, plan their organization and hierarchy.

### Understanding Page Hierarchy

Page hierarchy determines URL structure and site organization.

#### URL Structure Basics

```
yoursite.com/               ← Home page (root)
yoursite.com/about          ← Top-level page
yoursite.com/services       ← Top-level page
yoursite.com/services/web-design    ← Second-level page (child of /services)
yoursite.com/contact        ← Top-level page
```

**Hierarchy Principles:**

- **Top-level pages**: Directly under root (e.g., `/about`, `/services`)
- **Second-level pages**: Children of top-level (e.g., `/services/web-design`)
- **Third-level pages**: Children of second-level (e.g., `/services/web-design/ecommerce`)

**Best Practices:**

✅ **Keep it shallow**: 2-3 levels maximum (easier navigation, better SEO)  
✅ **Logical grouping**: Related pages under same parent  
✅ **Clear URLs**: Descriptive, lowercase, hyphenated  
✅ **Consistent structure**: Similar page types use similar URLs  

❌ **Avoid:**
- Deep nesting (4+ levels)
- Confusing URLs (`/page1`, `/temp`, `/new-page-copy`)
- Special characters in URLs
- Constantly changing URL structure

#### Common Website Structures

**Simple Business Site:**

```
Home (/)
├── About (/about)
├── Services (/services)
├── Portfolio (/portfolio)
└── Contact (/contact)
```

**Service-Based Business:**

```
Home (/)
├── About (/about)
├── Services (/services)
│   ├── Web Design (/services/web-design)
│   ├── SEO (/services/seo)
│   └── Marketing (/services/marketing)
├── Case Studies (/case-studies)
└── Contact (/contact)
```

**Product/E-commerce:**

```
Home (/)
├── About (/about)
├── Products (/products)
│   ├── Category A (/products/category-a)
│   └── Category B (/products/category-b)
├── Support (/support)
└── Contact (/contact)
```

**Nonprofit:**

```
Home (/)
├── About (/about)
│   ├── Mission (/about/mission)
│   └── Team (/about/team)
├── Programs (/programs)
├── Get Involved (/get-involved)
└── Donate (/donate)
```

### Planning Your Initial Pages

Use your sitemap from Phase 1 to identify which pages to build first.

#### Essential Pages (Build First)

| Page Type | Priority | Purpose |
|-----------|----------|---------|
| **About** | High | Build trust, tell your story |
| **Services/Products** | High | Explain what you offer |
| **Contact** | High | Enable communication |

#### Secondary Pages (Build Next)

| Page Type | Priority | Purpose |
|-----------|----------|---------|
| **FAQ** | Medium | Answer common questions |
| **Testimonials** | Medium | Build credibility |
| **Team** | Medium | Humanize your brand |
| **Portfolio/Case Studies** | Medium | Demonstrate expertise |
| **Blog/News** | Low | Ongoing content (can wait) |

#### Page Planning Checklist

For each page you plan to create:

- [ ] Page purpose clearly defined
- [ ] URL decided (lowercase, hyphenated)
- [ ] Parent page identified (if applicable)
- [ ] Template selected
- [ ] Content drafted (at least outline)
- [ ] Images/media prepared
- [ ] SEO keywords identified

---

## 2. Creating Your About Page

The About page tells your story and builds trust with visitors.

### Why the About Page Matters

**Statistics:**
- About pages are typically the 2nd most visited page (after home)
- 52% of visitors want to see "About Us" information
- Builds credibility and human connection

**What Visitors Want to Know:**
- Who you are (person, team, company)
- Your story and mission
- Why you're qualified/trustworthy
- Your values and approach
- How to work with you

### About Page Structure

#### Common About Page Sections

**1. Introduction/Hero**
- Brief statement of who you are
- What you do
- Compelling headline

**2. Your Story**
- How you started
- Why you do what you do
- Key milestones

**3. Mission/Values**
- What drives you
- Core principles
- Approach or philosophy

**4. Team (if applicable)**
- Key team members
- Photos and bios
- Expertise highlights

**5. Social Proof**
- Achievements
- Certifications
- Client/customer count
- Awards

**6. Call-to-Action**
- Next step for visitors
- Link to services/contact
- Invitation to connect

### Step-by-Step: Creating the About Page

#### Step 1: Create New Page

1. Navigate to **Create a Page** (main menu or dashboard)
2. Enter the URL: **`/about`**
3. Select an appropriate template:
   - **Content Page Template** (if available)
   - **Standard Page Template**
   - Or use the same template as your home page

4. Click **Create**

#### Step 2: Set Page Properties

Before editing content, set SEO properties:

1. Click **Properties** or **Settings** (depending on interface)
2. **Page Title**: `About [Your Company Name] | [Tagline]`
   - Example: `About TechFlow Solutions | Web Design & Development Experts`
   - Keep to 50-60 characters
3. **Meta Description**: Compelling 150-160 character summary
   - Example: `Learn about TechFlow Solutions, a team of passionate web designers and developers helping businesses grow online since 2018. See our mission and meet our team.`
4. Click **Save**

#### Step 3: Enter Edit Mode

1. Click **Edit** button (upper-right corner)
2. Live Editor opens with editable regions

#### Step 4: Add Introduction/Hero Section

**If your template has a hero section:**

1. Click on the hero headline area
2. Write a compelling headline:
   - **Good examples:**
     - "We Build Websites That Drive Results"
     - "Helping Nonprofits Change the World Through Technology"
     - "Your Partner in Digital Marketing Success"
   - **Avoid generic:**
     - "About Us" (too boring)
     - "Welcome" (says nothing)

3. Click on subheadline/supporting text
4. Add 1-2 sentences explaining who you are:
   ```
   TechFlow Solutions is a web design and development agency 
   specializing in custom websites for growing businesses.
   ```

**If no hero section:**
- Skip to main content area
- Start with a compelling opening paragraph

#### Step 5: Add Your Story Section

1. Click in the main content area
2. Add an H2 heading: **"Our Story"** or **"How We Started"**
3. Write 2-3 paragraphs about:
   - **Origin**: How and why you started
   - **Journey**: Key moments or growth
   - **Present**: Where you are today

**Example Structure:**

```markdown
## Our Story

[Paragraph 1: The beginning]
TechFlow Solutions was founded in 2018 when two web developers 
realized small businesses needed affordable, professional websites 
that actually generated leads.

[Paragraph 2: The journey]
What started as a side project quickly grew as word spread about 
our practical approach and client-first mindset. We've now helped 
over 150 businesses establish their online presence.

[Paragraph 3: Today]
Today, we're a team of 8 passionate designers, developers, and 
digital strategists who love helping businesses succeed online.
```

#### Step 6: Add Mission/Values Section

1. Add an H2 heading: **"Our Mission"** or **"What We Believe"**
2. Write about your purpose and approach
3. Consider using a bulleted list for values:

**Example:**

```markdown
## Our Mission

We believe every business deserves a website that works as hard 
as they do. Our mission is to create beautiful, functional websites 
that help our clients grow.

**Our Core Values:**
- **Client Success First**: Your goals drive our decisions
- **Transparency**: Clear communication, no surprises
- **Quality Craftsmanship**: We sweat the details
- **Continuous Learning**: Staying ahead of industry trends
```

#### Step 7: Add Team Section (Optional)

If showcasing team members:

1. Add an H2 heading: **"Meet the Team"**
2. For each team member, include:
   - Photo (professional headshot)
   - Name and title
   - Brief bio (2-3 sentences)
   - Expertise or role

**Layout Options:**
- **Grid**: 2-3 columns with photos and bios
- **List**: Vertical list with photo, name, and bio
- **Highlights**: Feature just key leadership

**Adding Team Photos:**

1. Click **Insert Image** or image placeholder
2. Select from File Manager or upload new photo
3. Add alt text: `[Name], [Title] at [Company]`
   - Example: `Sarah Johnson, Lead Designer at TechFlow Solutions`

#### Step 8: Add Social Proof/Achievements

Build credibility with achievements:

1. Add an H2 heading: **"Our Track Record"** or **"By the Numbers"**
2. Include impressive stats or achievements:

**Example:**

```markdown
## By the Numbers

- **150+ Websites Launched**: Helping businesses since 2018
- **98% Client Satisfaction**: Rated 4.9/5 on Google Reviews
- **12 Industry Awards**: Recognized for design excellence
- **50+ Ongoing Clients**: Long-term partnerships built on results
```

#### Step 9: Add Call-to-Action

End with a clear next step:

1. Add an H2 heading: **"Ready to Work Together?"**
2. Write inviting text:
   ```
   We'd love to hear about your project. Let's discuss how 
   we can help you achieve your goals.
   ```
3. Add a CTA button:
   - Text: "Get in Touch" or "Schedule a Consultation"
   - Link: `/contact` or your contact page URL

#### Step 10: Save and Preview

1. Click **Save** (upper-right corner)
2. Click **Preview** to review the page
3. Check for:
   - No typos or errors
   - Images display correctly
   - Headings in proper hierarchy (H1 → H2)
   - CTA button works
   - Content reads naturally

### About Page Content Tips

**Writing Your Story:**

✅ **Do:**
- Be authentic and genuine
- Share why you're passionate
- Include specific details (dates, numbers, milestones)
- Show personality
- Focus on benefits to visitors

❌ **Don't:**
- Use corporate jargon
- Write in third person (unless large company)
- Make it all about you (connect to customer needs)
- Include irrelevant history
- Be overly promotional

**Tone and Voice:**

| Business Type | Appropriate Tone |
|---------------|------------------|
| **B2B/Professional Services** | Professional, credible, approachable |
| **Creative/Agency** | Energetic, innovative, personality-driven |
| **Nonprofit** | Passionate, mission-focused, human |
| **E-commerce** | Friendly, helpful, trustworthy |

---

## 3. Creating Service or Product Pages

Service/product pages explain what you offer and convert visitors into customers.

### Planning Your Service/Product Pages

#### Single vs. Multiple Service Pages

**Option 1: Single Services Page** (`/services`)
- **Best for**: 3-5 services, similar offerings
- **Structure**: All services on one page with sections
- **Pros**: Simple, less maintenance
- **Cons**: Can get long, harder to rank for specific services

**Option 2: Services Parent + Child Pages**
- **Best for**: Multiple distinct services, SEO focus
- **Structure**: 
  ```
  /services (overview page)
  /services/web-design
  /services/seo
  /services/marketing
  ```
- **Pros**: Better SEO, detailed service info, focused CTAs
- **Cons**: More pages to maintain

**Option 3: No Parent Page, Individual Pages**
- **Best for**: Completely different offerings
- **Structure**:
  ```
  /web-design
  /seo
  /marketing
  ```
- **Pros**: Strongest SEO, clearest focus
- **Cons**: Navigation may be unclear

#### Which Structure to Choose?

| Scenario | Recommended Structure |
|----------|-----------------------|
| **Freelancer** with 3 services | Single page (/services) |
| **Agency** with 5+ distinct services | Parent + child pages |
| **SaaS** with multiple products | Individual pages (no parent) |
| **E-commerce** with product categories | Parent + child pages |
| **Local business** with few services | Single page (/services) |

### Service/Product Page Essential Elements

Every service or product page should include:

**1. Clear Headline**
- What service/product is
- Primary benefit
- Specific and descriptive

**2. Description**
- What it is
- Who it's for
- Problems it solves

**3. Features/Benefits**
- Key features (what it has)
- Benefits (what customer gets)
- Use bulleted lists for scannability

**4. How It Works (Optional)**
- Process or steps
- What customer can expect
- Timeline (if applicable)

**5. Social Proof**
- Testimonials specific to this service
- Case studies or examples
- Results or metrics

**6. Pricing (Optional)**
- Starting prices or packages
- "Contact for quote" if custom
- Value proposition

**7. FAQ (Optional)**
- Common questions
- Address objections
- Clarify details

**8. Strong CTA**
- Clear next action
- Urgent but not pushy
- Easy to find

### Step-by-Step: Creating a Service Page

We'll create an example service page for "Web Design" at `/services/web-design`.

#### Step 1: Create the Page

1. Go to **Create a Page**
2. Enter URL: **`/services/web-design`**
   - Note: Use `/` for parent-child structure
   - Lowercase, hyphenated
3. Select a template:
   - **Service Page Template** (if available)
   - **Content Page Template**
4. Click **Create**

#### Step 2: Set Page Properties (SEO)

1. Click **Properties** or **Settings**
2. **Page Title**: `[Service Name] | [Company Name]`
   - Example: `Web Design Services | TechFlow Solutions`
   - Include primary keyword
   - 50-60 characters

3. **Meta Description**: Include benefit, CTA
   - Example: `Professional web design services that convert visitors into customers. Custom websites built for your business goals. Get a free consultation today.`
   - 150-160 characters
   - Compelling and descriptive

4. Click **Save**

#### Step 3: Add Hero/Introduction

1. Click **Edit** to enter Live Editor
2. Click on hero headline area
3. Write benefit-focused headline:
   - **Good examples:**
     - "Custom Web Design That Drives Results"
     - "Beautiful Websites Built for Your Business"
     - "Professional Web Design Services"
   - **Avoid:**
     - "Web Design" (too generic)
     - "Our Web Design Services" (company-focused)

4. Add supporting text (1-2 sentences):
   ```
   We create custom websites that look stunning and convert 
   visitors into customers. Built for your goals, optimized 
   for performance.
   ```

#### Step 4: Add Service Description

1. Click in main content area
2. Add H2 heading: **"What Is Included"** or **"Our Web Design Services"**
3. Write 2-3 paragraphs describing the service:

**Example:**

```markdown
## Our Web Design Services

Our custom web design service creates websites that represent 
your brand and achieve your business goals. Whether you need 
a simple business site or a complex e-commerce platform, we 
design with your audience in mind.

Every website we design is:
- Custom-built for your brand (no templates)
- Responsive across all devices
- Optimized for search engines
- Built with user experience first
- Fast-loading and secure
```

#### Step 5: Add Features and Benefits

1. Add H2 heading: **"What You Get"** or **"Features"**
2. Use a bulleted or numbered list:

**Example:**

```markdown
## What You Get

**Design & Planning:**
- Custom design mockups
- Brand integration
- User experience planning
- Wireframe development

**Development:**
- Responsive HTML/CSS
- Cross-browser compatibility
- CMS integration (easy editing)
- Fast-loading pages

**Optimization:**
- SEO foundation (meta tags, structure)
- Mobile optimization
- Performance tuning
- Security best practices

**Support:**
- Training for your team
- 30-day post-launch support
- Documentation
- Ongoing maintenance options
```

#### Step 6: Add "How It Works" Section

1. Add H2 heading: **"Our Process"** or **"How It Works"**
2. List steps (numbered list works well):

**Example:**

```markdown
## Our Process

**1. Discovery (Week 1)**
We learn about your business, goals, and target audience. 
You'll fill out a brief and we'll have a kickoff call.

**2. Design (Week 2-3)**
We create custom design mockups based on your brand. You'll 
review and provide feedback until it's perfect.

**3. Development (Week 4-5)**
We build your website with clean code, ensuring it works 
flawlessly on all devices and browsers.

**4. Testing & Launch (Week 6)**
We thoroughly test everything, provide training, and launch 
your new website to the world.

**Typical Timeline:** 6-8 weeks from start to launch
```

#### Step 7: Add Social Proof

1. Add H2 heading: **"What Our Clients Say"** or **"Success Stories"**
2. Include testimonials or case study highlights:

**Example:**

```markdown
## What Our Clients Say

> "TechFlow redesigned our website and we saw a 40% increase 
> in leads within the first month. The process was smooth and 
> they really understood our business."
> 
> **— Sarah Chen, CEO at GreenLeaf Consulting**

> "Professional, creative, and responsive. They delivered 
> exactly what we needed, on time and on budget."
> 
> **— Mike Rodriguez, Owner of Rodriguez Law Firm**
```

**Or link to case studies:**

```markdown
## Success Stories

**GreenLeaf Consulting** - 40% increase in qualified leads  
See how we transformed their outdated website into a lead 
generation machine. [Read Case Study →](/case-studies/greenleaf)
```

#### Step 8: Add Pricing (Optional)

If you show pricing:

1. Add H2 heading: **"Investment"** or **"Pricing"**
2. Include pricing tiers or starting price:

**Example (Packages):**

```markdown
## Investment

| Package | Starting At | Best For |
|---------|-------------|----------|
| **Starter** | $2,500 | Small businesses, 5-page site |
| **Professional** | $5,000 | Growing businesses, custom features |
| **Enterprise** | $10,000+ | Complex sites, e-commerce, custom apps |

All packages include responsive design, SEO setup, and training.

[Get a Custom Quote →](/contact)
```

**Example (Starting Price):**

```markdown
## Investment

Custom web design projects start at **$3,500** depending on 
scope and complexity.

Every project includes discovery, design, development, testing, 
and launch support. Contact us for a detailed quote based on 
your specific needs.

[Schedule a Consultation →](/contact)
```

#### Step 9: Add Call-to-Action

1. Add final H2 heading: **"Ready to Get Started?"**
2. Write inviting text with CTA button:

**Example:**

```markdown
## Ready to Get Started?

Let's discuss your project and create a website that helps 
your business grow.

[Get a Free Consultation →](/contact) [View Our Portfolio →](/portfolio)
```

#### Step 10: Save, Preview, and Publish

1. Click **Save**
2. Click **Preview** to review
3. Check:
   - Headline is benefit-focused
   - Content scannable (headings, bullets)
   - CTA clear and prominent
   - No typos
   - Images (if any) have alt text
4. If satisfied, click **Publish**

### Service Page Writing Tips

**Focus on Benefits, Not Features:**

❌ **Feature-focused:** "Our websites use HTML5 and CSS3"  
✅ **Benefit-focused:** "Your website will look stunning on any device"

❌ **Feature-focused:** "We include SEO meta tags"  
✅ **Benefit-focused:** "Your website will be found on Google"

**Address Customer Concerns:**

Common objections to anticipate:
- **Cost**: Show value, offer packages, emphasize ROI
- **Time**: Provide clear timeline
- **Technical**: Assure ease of use, training provided
- **Trust**: Include testimonials, portfolio, guarantees

**Keep It Scannable:**

- Use headings frequently (H2, H3)
- Short paragraphs (2-4 sentences)
- Bulleted lists for features
- Bold key points
- Ample whitespace

---

## 4. Creating Your Contact Page

The contact page makes it easy for visitors to reach you.

### Contact Page Essential Elements

**Must-Have Elements:**

1. **Clear Headline**: "Get in Touch" or "Contact Us"
2. **Contact Form**: Primary way to reach you
3. **Email Address**: Alternative contact method
4. **Phone Number**: For urgent/phone-preferred contacts
5. **Physical Address**: If you have a location (builds trust)
6. **Business Hours**: When you're available

**Optional Elements:**

- **Social Media Links**: Additional ways to connect
- **Map**: If physical location
- **FAQ**: Common questions before contacting
- **Response Time**: Set expectations
- **Multiple Contact Options**: Sales, support, general

### Step-by-Step: Creating Contact Page

#### Step 1: Create the Page

1. Go to **Create a Page**
2. Enter URL: **`/contact`**
3. Select template:
   - **Contact Page Template** (if available)
   - **Content Page Template**
4. Click **Create**

#### Step 2: Set Page Properties

1. Click **Properties**
2. **Page Title**: `Contact [Company Name] | [Service/Location]`
   - Example: `Contact TechFlow Solutions | Web Design Inquiries`
3. **Meta Description**: Clear, inviting
   - Example: `Contact TechFlow Solutions for web design and development inquiries. Call (555) 123-4567, email hello@techflow.com, or fill out our contact form.`
4. Click **Save**

#### Step 3: Add Headline and Introduction

1. Click **Edit**
2. Add headline: **"Let's Work Together"** or **"Get in Touch"**
3. Add brief welcoming text (1-2 sentences):
   ```
   We'd love to hear about your project. Reach out and we'll 
   respond within 24 hours.
   ```

#### Step 4: Add Contact Form

**If your template includes a form:**

1. The form may already be present
2. Verify form fields are appropriate:
   - Name
   - Email
   - Phone (optional)
   - Message
   - Submit button

**If you need to add a form:**

> **Note:** Contact form implementation depends on your SkyCMS setup. Common methods:
> - Embedded form code from template
> - Third-party form service (Google Forms, Typeform, etc.)
> - Custom form integrated with your CMS

**Basic form structure to add:**

```html
<form action="/submit-contact" method="POST">
  <label for="name">Name *</label>
  <input type="text" id="name" name="name" required>
  
  <label for="email">Email *</label>
  <input type="email" id="email" name="email" required>
  
  <label for="phone">Phone</label>
  <input type="tel" id="phone" name="phone">
  
  <label for="message">Message *</label>
  <textarea id="message" name="message" rows="5" required></textarea>
  
  <button type="submit">Send Message</button>
</form>
```

> **Check with your administrator** about form handling and email delivery setup.

#### Step 5: Add Contact Information

1. Add H2 heading: **"Other Ways to Reach Us"**
2. List contact details:

**Example:**

```markdown
## Other Ways to Reach Us

**Email:** hello@techflow.com  
**Phone:** (555) 123-4567  
**Address:** 123 Main Street, Suite 200, San Francisco, CA 94102

**Business Hours:**  
Monday-Friday: 9:00 AM - 5:00 PM PST  
Saturday-Sunday: Closed

We typically respond to inquiries within 24 business hours.
```

#### Step 6: Add Social Media (Optional)

If appropriate for your business:

```markdown
## Connect With Us

[LinkedIn] [Twitter] [Facebook] [Instagram]
```

Or with icons if your template supports them.

#### Step 7: Add Map (Optional)

If you have a physical location:

**Using Google Maps Embed:**

1. Go to Google Maps
2. Find your location
3. Click **Share** → **Embed a map**
4. Copy the iframe code
5. Paste into your page (in Code view if needed)

**Example:**

```html
<iframe 
  src="https://www.google.com/maps/embed?pb=..." 
  width="600" 
  height="450" 
  style="border:0;" 
  allowfullscreen="" 
  loading="lazy">
</iframe>
```

#### Step 8: Save, Preview, and Publish

1. Click **Save**
2. Click **Preview**
3. **Test the contact form:**
   - Fill out all fields
   - Click Submit
   - Verify you receive the email (or check with admin)
4. If everything works, click **Publish**

### Contact Page Best Practices

**Form Design:**

✅ **Do:**
- Keep forms short (name, email, message minimum)
- Mark required fields clearly
- Use clear labels
- Large, tappable submit button
- Confirmation message after submission

❌ **Don't:**
- Ask for unnecessary information
- Use CAPTCHA unless spam is severe
- Make phone number required (barrier)
- Use unclear field labels

**Building Trust:**

- **Response time**: State how quickly you'll respond
- **Privacy**: "We'll never share your information"
- **Alternative methods**: Offer phone/email for those who prefer
- **Physical address**: Shows legitimacy (if applicable)

**Accessibility:**

- Labels for all form fields
- High contrast for readability
- Large enough tap targets (mobile)
- Keyboard navigable

---

## 5. Setting Up Navigation

Ensure all your new pages are accessible through navigation.

### Understanding SkyCMS Navigation

Navigation in SkyCMS is typically controlled through:

1. **Layout File**: Navigation structure defined in your site design
2. **Menu Management**: Some SkyCMS setups have menu management UI
3. **Manual Links**: Hardcoded links in layout/template

> **Check with your administrator** about how navigation is managed in your specific SkyCMS setup.

### Verifying Navigation Links

Your navigation should include all essential pages.

#### Typical Header Navigation

```
Logo | Home | About | Services | Portfolio | Contact
```

Or with dropdown:

```
Logo | Home | About | Services ▼ | Portfolio | Contact
                      ├─ Web Design
                      ├─ SEO
                      └─ Marketing
```

#### Verification Checklist

- [ ] Home link present (usually the logo)
- [ ] About page linked
- [ ] Services/Products page(s) linked
- [ ] Contact page linked
- [ ] All links use correct URLs (starting with `/`)
- [ ] Active page highlighted (if styling supports)
- [ ] Mobile menu works (hamburger icon on small screens)

### Updating Navigation Links

**If your layout uses static HTML:**

1. Go to **Site Designs** (Layouts) in admin
2. Find your current layout
3. Click **Edit Code**
4. Locate the navigation section (usually in `<nav>` or `<header>`)
5. Update links:

**Example:**

```html
<nav>
  <ul>
    <li><a href="/">Home</a></li>
    <li><a href="/about">About</a></li>
    <li><a href="/services">Services</a></li>
    <li><a href="/portfolio">Portfolio</a></li>
    <li><a href="/contact">Contact</a></li>
  </ul>
</nav>
```

6. Click **Save**
7. Verify changes on your published pages

**If your CMS has Menu Management:**

1. Navigate to menu management area
2. Add new menu items for each page
3. Set proper hierarchy (parent/child relationships)
4. Save menu
5. Verify on live site

### Footer Navigation

Don't forget footer links:

**Typical Footer Structure:**

```
Company               Services            Contact
├─ About             ├─ Web Design       ├─ Get in Touch
├─ Team              ├─ SEO              ├─ (555) 123-4567
└─ Careers           └─ Marketing        └─ hello@company.com

Legal                 Social
├─ Privacy Policy     ├─ Facebook
├─ Terms of Service   ├─ Twitter
└─ Sitemap            └─ LinkedIn
```

**Update footer links** in your layout file to include new pages.

---

## 6. Internal Linking Strategy

Internal links connect your pages and improve SEO.

### Why Internal Linking Matters

**Benefits:**
- **SEO**: Helps search engines discover and understand pages
- **User Experience**: Helps visitors find related content
- **Page Authority**: Distributes ranking power across site
- **Engagement**: Increases time on site, pages per visit

### Where to Add Internal Links

**1. Navigation** (Primary)
- Header menu
- Footer menu
- Breadcrumbs

**2. Content Links** (Contextual)
- In body text where relevant
- "Learn more about [topic]" with link
- Related services/products mentions

**3. Call-to-Action Links**
- End of sections
- Sidebar widgets
- Related content boxes

### Internal Linking Best Practices

**Anchor Text (Link Text):**

✅ **Good anchor text:**
- Descriptive: "our web design services"
- Specific: "schedule a free consultation"
- Natural: "learn more about SEO"

❌ **Poor anchor text:**
- "Click here"
- "Read more"
- "This page"

**Linking Strategy:**

1. **Home page** should link to all top-level pages
2. **About page** should link to services and contact
3. **Service pages** should link to:
   - Other related services
   - Portfolio/case studies
   - Contact page (CTA)
4. **Contact page** needs no outbound internal links (it's the destination)

**Example - Adding Links to About Page:**

```markdown
We specialize in [web design](/services/web-design), 
[SEO](/services/seo), and [digital marketing](/services/marketing). 
Our team has helped over 150 businesses grow their online presence.

Ready to work together? [Get in touch](/contact) to discuss your project.
```

### Adding Internal Links in SkyCMS

1. **In Edit mode:**
   - Select text you want to make a link
   - Click **Link** icon in toolbar
   - Enter URL (starting with `/` for internal)
     - Example: `/about`, `/services/web-design`, `/contact`
   - Click **OK** or **Insert**

2. **Verify the link:**
   - Click the link in preview mode
   - Ensure it goes to correct page

---

## 7. Testing and Publishing Your Pages

Before marking Phase 5 complete, test all pages thoroughly.

### Comprehensive Testing Checklist

**For Each Page:**

**Content Verification:**
- [ ] No typos or grammatical errors
- [ ] All placeholder text replaced with real content
- [ ] Images load correctly (no broken images)
- [ ] Alt text added to all images
- [ ] Content accurate and up-to-date

**SEO Verification:**
- [ ] Page title set (50-60 characters)
- [ ] Meta description set (150-160 characters)
- [ ] One H1 heading per page
- [ ] Proper heading hierarchy (H1 → H2 → H3)
- [ ] URL is clean and descriptive
- [ ] Primary keyword used naturally in content

**Navigation:**
- [ ] Page appears in header navigation (if applicable)
- [ ] Page appears in footer navigation (if applicable)
- [ ] All navigation links work correctly
- [ ] Logo link returns to home page
- [ ] Mobile menu works (hamburger icon functional)

**Internal Links:**
- [ ] All internal links work (no 404 errors)
- [ ] Links open in same window/tab
- [ ] Anchor text is descriptive (not "click here")
- [ ] Related pages are linked contextually

**Call-to-Action:**
- [ ] CTA button present and visible
- [ ] CTA link goes to correct destination
- [ ] CTA text is clear and action-oriented
- [ ] Multiple CTA opportunities (if long page)

**Responsive Design:**
- [ ] Tested on mobile (375px)
- [ ] Tested on tablet (768px)
- [ ] Tested on desktop (1200px+)
- [ ] No horizontal scrolling
- [ ] Text readable at all sizes
- [ ] Buttons tappable on mobile

**Browser Compatibility:**
- [ ] Tested in Chrome
- [ ] Tested in Safari
- [ ] Tested in Firefox
- [ ] Tested in Edge
- [ ] No console errors

**Forms (Contact Page):**
- [ ] All fields work correctly
- [ ] Required fields validated
- [ ] Submit button works
- [ ] Confirmation message displays
- [ ] Email received (or admin notified)

**Publishing:**
- [ ] Page saved
- [ ] Previewed before publishing
- [ ] Published (not still in Draft)
- [ ] Verified live URL works
- [ ] Tested in incognito/private window

### Cross-Page Testing

After all pages are published:

**Navigation Flow:**
- [ ] Can navigate from home to every page
- [ ] Can navigate between related pages
- [ ] Can get back to home from any page
- [ ] Breadcrumbs work (if applicable)

**Consistency:**
- [ ] All pages use same layout/template family
- [ ] Header consistent across all pages
- [ ] Footer consistent across all pages
- [ ] Typography consistent
- [ ] Color scheme consistent
- [ ] Tone and voice consistent

**User Journey:**
- [ ] Visitor can find services/products easily
- [ ] Path from service to contact is clear
- [ ] About page builds trust
- [ ] Contact page is easy to find
- [ ] Logical flow between pages

### Common Issues and Solutions

| Issue | Solution |
|-------|----------|
| **Page not in navigation** | Update layout file or menu management |
| **404 error on internal link** | Check URL spelling, ensure page published |
| **Form not working** | Verify form action, check email configuration |
| **Inconsistent styling** | Ensure all pages use same layout/template |
| **Mobile menu not opening** | Check JavaScript in layout, clear cache |
| **Broken images** | Re-upload to File Manager, update URLs |

---

## 8. Phase 5 Completion Checklist

Verify you've completed all essential tasks:

### Pages Created and Published

- [ ] Home page (from Phase 4)
- [ ] About page
- [ ] Services/Products pages
- [ ] Contact page
- [ ] Any additional pages from sitemap

### Navigation Configured

- [ ] Header navigation includes all essential pages
- [ ] Footer navigation updated
- [ ] Mobile navigation works
- [ ] All navigation links tested

### Content Quality

- [ ] All pages have unique, valuable content
- [ ] No placeholder text ("Lorem ipsum")
- [ ] Images relevant and high-quality
- [ ] Tone and voice consistent
- [ ] Grammar and spelling checked

### SEO Implementation

- [ ] Every page has unique title
- [ ] Every page has unique meta description
- [ ] Heading hierarchy correct on all pages
- [ ] All images have alt text
- [ ] URLs clean and descriptive

### Internal Linking

- [ ] Key pages linked from home page
- [ ] Related services/products cross-linked
- [ ] About page links to services and contact
- [ ] Service pages link to contact (CTAs)
- [ ] Anchor text descriptive

### Testing Complete

- [ ] All pages tested on mobile
- [ ] All pages tested on desktop
- [ ] All links work (no 404s)
- [ ] Forms tested and working
- [ ] Browser compatibility verified

### Documentation

- [ ] Page URLs documented
- [ ] Login credentials stored securely
- [ ] Any custom functionality documented
- [ ] Admin contacts identified

---

## What's Next?

🎉 **Congratulations!** You've built the core pages of your website.

### Phase 6: Preparing for Handoff

If you're handing off the site to content creators or a client:

1. **Set up user roles and permissions**
2. **Create documentation** for content editors
3. **Provide training** on using SkyCMS
4. **Establish content workflows**
5. **Set up support processes**

See: [Phase 6: Preparing for Handoff](06-Preparing-for-Handoff.md) *(coming soon)*

### Pre-Launch Final Checks

Before officially launching:

1. **Run through Pre-Launch Checklist**
2. **Performance optimization** (image compression, caching)
3. **Security review** (SSL, permissions, backups)
4. **Analytics setup** (Google Analytics, etc.)
5. **Final content review**

See: [Pre-Launch Checklist](./Checklists/Pre-Launch-Checklist.md) *(coming soon)*

### Ongoing Maintenance

After launch:

- **Regular content updates** (keep pages fresh)
- **Monitor analytics** (identify improvement opportunities)
- **Backup regularly** (protect your work)
- **Update plugins/CMS** (security and features)
- **Review and refine** (iterative improvements)

See: [Monthly Maintenance Tasks](../Checklists/Monthly-Maintenance.md) *(coming soon)*

---

## Additional Resources

### Related Guides

- [Phase 1: Design & Planning](01-Design-and-Planning.md)
- [Phase 2: Creating Layouts](02-Creating-Layouts.md)
- [Phase 3: Creating Templates](03-Creating-Templates.md)
- [Phase 4: Building Home Page](04-Building-Home-Page.md)
- [Website Launch Workflow](Website-Launch-Workflow.md)

### SkyCMS Documentation

- [Creating Pages](../ContentCreation-QuickStart.md)
- [Live Editor Guide](../Editors/LiveEditor/README.md)
- [Templates Guide](../Templates/Readme.md)
- [File Manager](../FileManagement/index.md)
- [SkyCMS Official Docs](https://www.moonrise.net/cosmos/documentation/)

### Content Writing Resources

**Writing Tips:**
- Keep paragraphs short (2-4 sentences)
- Use active voice ("We create" not "Websites are created by us")
- Focus on benefits, not features
- Write for your audience, not yourself
- Use specific examples and numbers
- Break up text with headings and lists

**SEO Writing:**
- Use keywords naturally (no stuffing)
- Include keyword in H1, first paragraph
- Use related keywords (variations, synonyms)
- Write for humans first, search engines second
- Aim for 300+ words per page (more for key pages)

**Accessibility:**
- Write clear, simple sentences
- Use plain language (avoid jargon)
- Structure content with headings
- Add alt text to all images
- Use sufficient color contrast

---

## Need Help?

**Common Questions:**

**Q: How many pages should I create in Phase 5?**  
A: Focus on essential pages: Home, About, Services/Products, Contact. Additional pages (blog, FAQ, etc.) can come later.

**Q: Should I create all service pages at once?**  
A: Start with your most important services. You can add more pages later as needed.

**Q: What if I don't have all content ready?**  
A: Create pages with placeholder content and mark as Draft. Publish when content is complete.

**Q: How do I know if my navigation is correct?**  
A: Test user journey: Can you get from home to every page in 1-2 clicks? If yes, it's good.

**Q: Can I change page URLs after publishing?**  
A: Possible but not recommended (breaks links, SEO impact). Choose URLs carefully.

**Q: How long should each page be?**  
A: About: 400-800 words. Services: 500-1000 words. Contact: 150-300 words. Quality over quantity.

---

*For additional support, check the [SkyCMS Documentation](https://www.moonrise.net/cosmos/documentation/) or contact your administrator.*



