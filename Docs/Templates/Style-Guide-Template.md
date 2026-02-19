---
title: Style Guide Template
description: Fillable brand standards template including logos, colors, typography, images, tone of voice, and component styles
keywords: template, style-guide, brand, guidelines, design, standards
audience: [designers, content-creators, brand-managers]
---

# Style Guide Template

## Document Information

**Organization**: [Your Organization Name]  
**Website**: [Your Website URL]  
**Last Updated**: [Date]  
**Version**: 1.0  
**Owner**: [Brand/Marketing Team]  
**Approved By**: [Manager/Executive]

---

## 1. Brand Overview

### Brand Mission
[Describe what your organization does and why. This is the core reason your brand exists.]

**Example**: *At [Company], we help [target audience] [achieve goal] by [what makes you unique].*

**Your Mission**:  
[Fill in your brand mission here]

---

### Brand Values
List the core values that define your brand. These should influence all communication and design decisions.

**Example Values**: 
- Innovation
- Transparency
- Customer-Focused
- Sustainability

**Your Brand Values**:
1. [Value 1]: [Brief description]
2. [Value 2]: [Brief description]
3. [Value 3]: [Brief description]
4. [Value 4]: [Brief description]

---

### Brand Personality
Describe your brand's personality using adjectives. How would you describe your brand if it were a person?

**Example**: Professional yet approachable, innovative but trusted, modern and forward-thinking.

**Your Brand Personality**:  
[Describe your brand's personality]

---

## 2. Logo & Visual Identity

### Logo
Include logo image here or link to logo files.

**Logo File Locations**:
- Full Color: [file path/link]
- Black & White: [file path/link]
- Icon Version: [file path/link]
- Favicon: [file path/link]

### Logo Usage Guidelines

**✅ DO**:
- Use official logo files provided above
- Maintain clear space around logo (minimum [X] pixels)
- Use on contrasting backgrounds
- Maintain aspect ratio when resizing

**❌ DON'T**:
- Rotate or skew the logo
- Change logo colors
- Add effects (shadows, gradients, outlines)
- Use low-resolution versions
- Combine logo with other graphics

### Clear Space
The minimum clear space around the logo should be equal to the height of the "X" in the logo (or [X] pixels).

### Logo Sizing
- **Minimum Size**: [X] pixels wide
- **Preferred Sizes**: [sizes for different applications]
- **File Formats**: PNG (recommended), SVG, EPS, PDF

---

## 3. Color Palette

### Primary Colors

These colors should appear most frequently in your design. Use these for main UI elements, headers, and primary CTAs.

| Color Name | Color Code | RGB | CMYK | Usage |
|------------|-----------|-----|------|-------|
| Primary Blue | #[CODE] | R:[X] G:[X] B:[X] | C:[X] M:[X] Y:[X] K:[X] | Main headers, CTAs |
| Primary Orange | #[CODE] | R:[X] G:[X] B:[X] | C:[X] M:[X] Y:[X] K:[X] | Accents, highlights |
| Neutral Gray | #[CODE] | R:[X] G:[X] B:[X] | C:[X] M:[X] Y:[X] K:[X] | Text, backgrounds |

**Example Primary Palette**:
```html
<!-- Primary Blue -->
<div style="background-color: #0066CC; color: white; padding: 20px;">
  Primary Blue #0066CC
</div>

<!-- Primary Orange -->
<div style="background-color: #FF6600; color: white; padding: 20px;">
  Primary Orange #FF6600
</div>

<!-- Neutral Gray -->
<div style="background-color: #333333; color: white; padding: 20px;">
  Neutral Gray #333333
</div>
```

### Secondary Colors

Support colors that complement primary colors. Use for secondary elements and variations.

| Color Name | Color Code | RGB | Usage |
|------------|-----------|-----|-------|
| Light Blue | #[CODE] | R:[X] G:[X] B:[X] | Background fills, light accents |
| Dark Gray | #[CODE] | R:[X] G:[X] B:[X] | Dark text, borders |
| Accent Green | #[CODE] | R:[X] G:[X] B:[X] | Success states, confirmations |

### Functional Colors

Colors with specific meanings throughout the interface.

| Function | Color | Code | Usage |
|----------|-------|------|-------|
| Success | Green | #[CODE] | Confirmations, valid states |
| Warning | Yellow/Orange | #[CODE] | Alerts, warnings |
| Error | Red | #[CODE] | Errors, dangerous actions |
| Disabled | Light Gray | #[CODE] | Disabled states, inactive items |
| Information | Blue | #[CODE] | Information messages, hints |

### Color Contrast
All text must maintain minimum WCAG AA contrast ratio:
- **Normal Text**: Minimum 4.5:1 contrast ratio
- **Large Text (18pt+)**: Minimum 3:1 contrast ratio

**Tools for Testing**: [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/)

### Don'ts with Color
- ❌ Don't use more than 5 primary colors
- ❌ Don't rely only on color to convey information (also use icons, text labels)
- ❌ Don't use pure black (#000000) for main text (use #333333)
- ❌ Don't forget about colorblind users (test with accessibility tools)

---

## 4. Typography

### Font Family

**Primary Font** (Headlines, Large Text):
- **Font Name**: [e.g., Roboto, Open Sans]
- **File Name/Source**: [Where to get it]
- **Font Weight**: Bold (700), Semi-bold (600), Regular (400)
- **Usage**: H1, H2, H3, headers, CTAs

**Example**: *"Roboto" from Google Fonts*

**Secondary Font** (Body Text, Small Text):
- **Font Name**: [e.g., Open Sans, Lato]
- **File Name/Source**: [Where to get it]
- **Font Weight**: Regular (400), Light (300)
- **Usage**: Body paragraphs, lists, small text

**Example**: *"Open Sans" from Google Fonts*

### Font Sizing & Hierarchy

| Element | Font Size | Font Weight | Line Height | Letter Spacing | Usage |
|---------|-----------|-----------|------------|-----------------|-------|
| H1 | [X]px / [X]rem | Bold (700) | [X] | [X] | Page titles, main headlines |
| H2 | [X]px / [X]rem | Semi-bold (600) | [X] | [X] | Section headers |
| H3 | [X]px / [X]rem | Semi-bold (600) | [X] | [X] | Sub-section headers |
| Body | [X]px / [X]rem | Regular (400) | [X] | [X] | Paragraph text, main content |
| Small | [X]px / [X]rem | Regular (400) | [X] | [X] | Captions, helper text, labels |
| Button | [X]px / [X]rem | Semi-bold (600) | [X] | [X] | Button text, CTAs |

**Example Hierarchy** (16px base):
```
H1: 32px / 2rem (Bold)
H2: 24px / 1.5rem (Semi-bold)
H3: 20px / 1.25rem (Semi-bold)
Body: 16px / 1rem (Regular)
Small: 14px / 0.875rem (Regular)
```

### Line Height
- **Headlines**: 1.2x font size (tighter, more impactful)
- **Body Text**: 1.6x font size (easier to read in large blocks)
- **Small Text**: 1.5x font size

### Letter Spacing
- **Headlines**: 0 to -0.02em (slightly tight for impact)
- **Body**: 0 to 0.02em (normal, readable)
- **All Caps**: 0.1em to 0.15em (slightly expanded)

### Font Pairing Example

```html
<h1 style="font-family: 'Roboto', sans-serif; font-size: 32px; font-weight: 700; line-height: 1.2;">
  Headline Using Roboto
</h1>

<p style="font-family: 'Open Sans', sans-serif; font-size: 16px; font-weight: 400; line-height: 1.6;">
  This is body text using Open Sans. It should be easy to read in large blocks of text 
  for comfortable reading on screens and printed materials.
</p>
```

### Don'ts with Typography
- ❌ Don't use more than 2-3 font families
- ❌ Don't use all caps for body text (hard to read)
- ❌ Don't use very small fonts (<12px for body text)
- ❌ Don't forget about responsive sizing (smaller on mobile)
- ❌ Don't use low contrast text (see Color Contrast section)

---

## 5. Image Guidelines

### Photography Style

**Overall Aesthetic**: [Describe your photography style]

**Examples**: 
- Professional headshots with consistent background and lighting
- Candid team collaboration photos
- High-quality product photography on white background
- Lifestyle/lifestyle photos showing real people using product

**Your Photography Style**:  
[Fill in your photography style here]

### Image Types & Usage

#### Hero Images (Large banners, backgrounds)
- **Size**: Minimum 1920 x 1080px (16:9 aspect ratio)
- **File Format**: JPG or WebP for web optimization
- **File Size**: 100-300KB
- **Style**: [Your specific hero image style]

#### Product Images
- **Size**: [Standard size] px
- **Background**: [White, Transparent, Styled - specify]
- **Angles**: [Specify which angles are required]
- **File Format**: PNG or JPG
- **File Size**: [Target file size]

#### Team/People Photos
- **Size**: [Standard size] px
- **Background**: [Solid color, gradient, blurred - specify]
- **Clothing**: [Guidelines for professional attire]
- **File Format**: JPG or WebP
- **File Size**: [Target file size]

#### Icons & Illustrations
- **Size**: [Standard size] px
- **Style**: Flat, 3D, Minimalist, etc.
- **File Format**: SVG (preferred), PNG
- **Color**: Use primary color palette

### Image Optimization
All images must be optimized for web:
- **JPG**: 70-85% quality compression
- **PNG**: Use for images requiring transparency
- **WebP**: Modern format for better compression
- **Maximum file size**: 200KB for standard images, 300KB for hero images
- **Responsive images**: Provide multiple sizes for different devices

### Don'ts with Images
- ❌ Don't use low-quality or pixelated images
- ❌ Don't use overly edited or fake-looking photos
- ❌ Don't forget alt text for accessibility
- ❌ Don't mix photography styles inconsistently
- ❌ Don't use copyrighted images without permission

### Image Sources
**Approved Stock Photo Services**: [List any approved providers]
- [Service 1]: [URL]
- [Service 2]: [URL]
- [Custom Photography]: [Who to contact]

---

## 6. Icons & Buttons

### Icons

**Icon Library**: [Name and source, e.g., "Font Awesome Pro", "Material Design Icons", "Custom SVG set"]

**Icon Size Standards**:
- Small icons: 16x16px
- Standard icons: 24x24px
- Large icons: 32x32px
- Extra large icons: 48x48px+

**Icon Color**: 
- Primary: Use primary color from palette
- Hover/Active: Slightly lighter/darker shade
- Disabled: Use light gray (#CCCCCC)

**Icon Example**:
```html
<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#0066CC" stroke-width="2">
  <!-- Icon SVG code here -->
</svg>
```

### Buttons

#### Primary Button
Used for main CTAs (Sign Up, Get Started, Buy Now)

**Appearance**:
- Background Color: [Primary Color]
- Text Color: [Contrasting color, usually white]
- Text Style: [Font details]
- Padding: [X]px horizontal, [X]px vertical
- Border Radius: [X]px

**States**:
- Default: [Description]
- Hover: [Description - usually slightly darker]
- Active/Pressed: [Description]
- Disabled: [Description - grayed out]

**Example CSS**:
```css
.btn-primary {
  background-color: #0066CC;
  color: white;
  padding: 12px 24px;
  border-radius: 4px;
  font-weight: 600;
  border: none;
  cursor: pointer;
}

.btn-primary:hover {
  background-color: #0052A3;
}

.btn-primary:active {
  background-color: #003D7A;
}

.btn-primary:disabled {
  background-color: #CCCCCC;
  cursor: not-allowed;
}
```

#### Secondary Button
Used for less important actions (Learn More, Cancel)

**Appearance**:
- Background Color: Light gray or transparent
- Text Color: Primary color or dark gray
- Border: 2px solid primary color
- Padding: [X]px horizontal, [X]px vertical
- Border Radius: [X]px

#### Tertiary Button
Used for minimal-emphasis actions

**Appearance**:
- Background: Transparent
- Text Color: Primary color
- No border (optional subtle underline on hover)
- Text decoration: Underline on hover

### Button States

All buttons must support these states:

| State | Description | Visual Change |
|-------|-------------|--------------|
| Default | Normal button state | Standard styling |
| Hover | Mouse over button | Slightly darker/lighter color |
| Active | Button pressed | More pronounced color change |
| Disabled | Button cannot be used | Grayed out, no cursor change |
| Loading | Processing action | [Show loading spinner/indicator] |
| Focus | Keyboard focus (accessibility) | Visible outline/border |

---

## 7. Spacing & Layout

### Spacing System

Use a consistent spacing scale throughout design. Based on 8px grid system:

| Scale | Size | Usage |
|-------|------|-------|
| XS | 4px | Very tight spacing, micro-interactions |
| S | 8px | Tight spacing, element padding |
| M | 16px | Standard spacing, general padding |
| L | 24px | Generous spacing, section spacing |
| XL | 32px | Large spacing, major sections |
| 2XL | 48px | Extra large spacing, page sections |

**Example Usage**:
```
Component Padding (Internal): 16px (M)
Component Margin (External): 24px (L)
Section Spacing: 48px (2XL)
```

### Grid System

**Column Structure**: [Specify grid layout - e.g., 12 column, responsive]

**Breakpoints** (Responsive Design):
- Mobile: 320px - 767px
- Tablet: 768px - 1023px
- Desktop: 1024px - 1440px
- Large: 1441px+

### Max Content Width

**Maximum content width**: [Value, e.g., 1200px]  
This ensures long lines of text don't become hard to read on very large screens.

---

## 8. Tone of Voice & Content Guidelines

### Brand Voice

How should we communicate with our audience? Use 3-5 adjectives to describe the voice.

**Example**: Professional, friendly, approachable, trustworthy, innovative

**Your Brand Voice**:  
[Describe your brand voice]

### Writing Guidelines

#### Headline Guidelines
- Start with action or benefit
- Be specific and clear
- Avoid jargon
- Maximum headline length: 70 characters
- **Example**: "Increase Productivity by 40% in 30 Days" ✅ vs "Our Product is Good" ❌

#### Body Text Guidelines
- Use short paragraphs (3-4 sentences max)
- Use simple, clear language (8th grade reading level)
- Active voice over passive voice
- Break up text with subheadings and bullet points
- Avoid marketing hype (be honest and authentic)

#### CTA Text Guidelines
- Action-oriented verbs (Sign Up, Learn More, Get Started)
- Specific benefit when possible (Get Free Trial, Download Guide)
- First person perspective ("Get my free guide" vs "Get the free guide")
- 2-4 words maximum

### Tone Variations

**For Different Contexts**:

| Context | Tone | Example |
|---------|------|---------|
| Error Messages | Helpful, clear | "Email not found. Try another address or create an account." |
| Success Messages | Encouraging, positive | "Great! Your account is ready. Let's get started." |
| Warnings | Cautious but not alarming | "This action cannot be undone. Are you sure?" |
| Help Text | Friendly, supportive | "Need help? Check out our guides or contact support." |
| Legal/Privacy | Clear, transparent | [Standard legal language] |

### Don'ts with Tone & Content
- ❌ Don't use excessive exclamation marks (max 1 per section)
- ❌ Don't use ALL CAPS for emphasis (use bold or italics)
- ❌ Don't use corporate jargon or buzzwords
- ❌ Don't make claims you can't back up
- ❌ Don't ignore your audience's needs (be user-centric)

### Terminology

Create a glossary of approved terms and how to use them:

| Term | Definition | Approved Usage | Don't Use |
|------|-----------|-----------------|----------|
| [Term 1] | [Definition] | [Approved way to write it] | [Incorrect ways] |
| [Term 2] | [Definition] | [Approved way to write it] | [Incorrect ways] |

**Example**:
| Term | Definition | Usage | Avoid |
|------|-----------|-------|-------|
| Product | Our main offering | "Our product helps you..." | "Our solution", "Our app", "Our tool" |
| User | Person using the product | "Users can...", "User dashboard" | "Customers", "Client" (in this context) |

---

## 9. Component Styles

### Forms & Input Fields

**Text Input**:
- Border: 1px solid #CCCCCC
- Padding: 12px
- Border Radius: 4px
- Font Size: 16px
- Hover: Light gray background (#F5F5F5)
- Focus: Blue border (#0066CC), box shadow

**Labels**:
- Font Weight: 600
- Font Size: 14px
- Color: #333333
- Margin Bottom: 8px

### Cards

**Appearance**:
- Background: White (#FFFFFF)
- Border: 1px solid #EEEEEE
- Border Radius: 8px
- Padding: 24px
- Box Shadow: 0 2px 8px rgba(0, 0, 0, 0.1)

**Hover State**:
- Box Shadow: 0 4px 16px rgba(0, 0, 0, 0.15)
- Transform: Slightly elevate (y: -2px)

### Navigation

**Main Navigation**:
- Background: [Color]
- Text: [Color]
- Font Weight: 600
- Padding: 16px 24px (horizontal)
- Active/Current: Underline or highlight with primary color

**Mobile Navigation**:
- Hamburger menu icon (3 horizontal lines)
- Slide-out sidebar or dropdown
- Close button to dismiss

---

## 10. Accessibility Requirements

All web content must meet WCAG 2.1 Level AA standards:

### Color & Contrast
- ✅ Do: Minimum 4.5:1 contrast ratio for text
- ✅ Do: Don't rely only on color (use icons, labels)
- ✅ Do: Test with color blindness simulator

### Images & Icons
- ✅ Do: Include alt text for all images
- ✅ Do: Describe image purpose, not just "image of"
- ✅ Do: Use meaningful icon labels/tooltips

### Typography
- ✅ Do: Use semantic HTML (H1, H2, not styled divs)
- ✅ Do: Ensure proper heading hierarchy
- ✅ Do: Use adequate font sizes (minimum 12px for body)

### Interactive Elements
- ✅ Do: Ensure keyboard navigation works
- ✅ Do: Provide visible focus indicators
- ✅ Do: Test with screen readers

### Accessibility Testing Tools
- [WAVE Browser Extension](https://wave.webaim.org/extension/)
- [Lighthouse (Chrome DevTools)](https://developers.google.com/web/tools/lighthouse)
- [Axe DevTools](https://www.deque.com/axe/devtools/)

---

## 11. Application Examples

### Home Page Example

```html
<header style="background-color: #0066CC; color: white; padding: 48px 24px;">
  <h1 style="font-size: 32px; font-weight: 700; margin-bottom: 16px;">
    Welcome to [Company]
  </h1>
  <p style="font-size: 18px; margin-bottom: 32px;">
    [Your company mission statement]
  </p>
  <button style="background-color: #FF6600; color: white; padding: 12px 24px; 
                 border-radius: 4px; font-weight: 600; border: none; cursor: pointer;">
    Get Started
  </button>
</header>

<section style="padding: 48px 24px; max-width: 1200px; margin: 0 auto;">
  <h2 style="font-size: 24px; font-weight: 600; margin-bottom: 32px;">
    What We Offer
  </h2>
  
  <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 24px;">
    <!-- Cards go here -->
  </div>
</section>
```

### Contact Form Example

```html
<form style="max-width: 500px; margin: 0 auto; padding: 24px;">
  <label for="name" style="display: block; font-weight: 600; font-size: 14px; 
                           color: #333333; margin-bottom: 8px;">
    Full Name
  </label>
  <input type="text" id="name" placeholder="John Doe" 
         style="width: 100%; padding: 12px; border: 1px solid #CCCCCC; 
                border-radius: 4px; font-size: 16px; margin-bottom: 24px;" />
  
  <label for="email" style="display: block; font-weight: 600; font-size: 14px; 
                            color: #333333; margin-bottom: 8px;">
    Email Address
  </label>
  <input type="email" id="email" placeholder="john@example.com" 
         style="width: 100%; padding: 12px; border: 1px solid #CCCCCC; 
                border-radius: 4px; font-size: 16px; margin-bottom: 24px;" />
  
  <button type="submit" style="background-color: #0066CC; color: white; 
                              padding: 12px 24px; border-radius: 4px; 
                              font-weight: 600; border: none; cursor: pointer; width: 100%;">
    Send Message
  </button>
</form>
```

---

## 12. Brand Don'ts

### Never Do These Things

- ❌ Use unauthorized logo versions
- ❌ Change brand colors without approval
- ❌ Use unbranded fonts
- ❌ Mix photography styles
- ❌ Use overly aggressive sales language
- ❌ Ignore accessibility requirements
- ❌ Deviate from spacing guidelines
- ❌ Use more than 5 colors in a design
- ❌ Forget alt text on images
- ❌ Make content hard to read with poor contrast

---

## 13. Resources & Files

### Download Resources

- **Logo Files**: [Link to logo downloads]
- **Font Files**: [Link to Google Fonts or font files]
- **Color Palette** (Adobe, Figma, etc.): [Link to design file]
- **Template Files**: [Link to templates]
- **Icon Set**: [Link to icon library]

### Tools & Software

| Tool | Purpose | Cost |
|------|---------|------|
| Figma | Design tool | Free/Paid |
| Adobe Creative Suite | Design suite | Subscription |
| Google Fonts | Free fonts | Free |
| Coolors | Color palette generator | Free |
| Contrast Checker | Accessibility testing | Free |

### Related Documentation

- [SkyCMS Templates Guide](../Templates/Readme.md)
- [SkyCMS Layouts Documentation](../Layouts/Readme.md)
- [Phase 2: Creating Layouts](../Developer-Guides/02-Creating-Layouts.md)
- [Phase 3: Creating Templates](../Developer-Guides/03-Creating-Templates.md)

### Contact Information

**Questions about the style guide?**

- **Design Lead**: [Name] - [Email] - [Phone]
- **Marketing Manager**: [Name] - [Email] - [Phone]
- **Brand Owner**: [Name] - [Email] - [Phone]

**Slack Channel**: [Channel name or N/A if not applicable]

---

## 14. Change Log & Version Control

| Version | Date | Changes | Updated By |
|---------|------|---------|-----------|
| 1.0 | [Date] | Initial style guide | [Name] |
| | | | |

---

## Approval & Sign-Off

This style guide has been reviewed and approved by:

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Design Lead | _________________ | _________________ | _______ |
| Marketing Manager | _________________ | _________________ | _______ |
| Brand Owner | _________________ | _________________ | _______ |

---

## How to Use This Template

1. **Fill in all bracketed sections** [like this] with your specific information
2. **Update all example colors, fonts, and values** to match your brand
3. **Remove "Example" sections** after filling in your own
4. **Add or remove sections** as needed for your organization
5. **Upload logo and other brand assets** to appropriate locations
6. **Share with your team** and train them on these standards
7. **Review and update annually** or when brand changes

**Tips**:
- Keep this document accessible to all team members
- Use version control to track changes
- Reference this guide during all design decisions
- Train new team members on the brand standards
- Update whenever brand guidelines change

---

**Remember**: A strong, consistent brand comes from following these guidelines consistently across all touchpoints. When everyone follows the same standards, your brand becomes recognizable, trustworthy, and professional.
