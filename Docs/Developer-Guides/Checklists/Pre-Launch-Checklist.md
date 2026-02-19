---
title: Pre-Launch Checklist
description: Comprehensive pre-launch testing and verification checklist for reliability, security, performance, and SEO
keywords: pre-launch, checklist, testing, verification, security, performance, SEO
audience: [developers, qa, project-managers]
---

# Pre-Launch Checklist

## Overview

Before launching your website to the public, conduct thorough testing and verification using this comprehensive checklist. This ensures your site is reliable, secure, fast, and ready for visitors.

### Purpose

This checklist helps you:
- Identify and fix issues before launch
- Verify all functionality works correctly
- Ensure compliance with best practices
- Protect your site and users
- Optimize performance and user experience

### Who Should Use This

- **Project Leads**: Overall responsibility
- **QA/Testing**: Verification and testing
- **Developers**: Technical verification
- **Content Team**: Content review
- **SEO/Marketing**: SEO and analytics setup

### When to Use This

**Timing:**
- 1-2 weeks before planned launch
- After all content is finalized
- After Phase 5 (Building Initial Pages) is complete
- Before Phase 6 handoff begins

**Duration:**
- Estimated 4-6 hours for full verification
- Can be split across multiple days

### Scoring

Track your progress:
- **✅ Complete**: Item verified and passed
- **⚠️ In Progress**: Item started, needs completion
- **❌ Issue**: Item failed, needs fixing
- **N/A**: Not applicable to your site

---

## 1. Functionality Verification

Verify all core features work correctly.

### Page Loading & Access

- [ ] **Homepage loads**: Visit `/` and verify page displays
- [ ] **All pages accessible**: Can navigate to every published page
- [ ] **Page URLs work**: URLs in address bar match expected format
- [ ] **No 404 errors**: Check main pages don't show 404 errors
- [ ] **Redirects work**: Old URLs redirect correctly (if applicable)
- [ ] **SSL certificate active**: URL shows 🔒 lock icon (HTTPS)
- [ ] **Load time acceptable**: Pages load within 3 seconds
- [ ] **Mobile viewable**: Pages display on mobile devices

### Navigation & Links

- [ ] **Header navigation works**: All header links clickable and functional
- [ ] **Footer navigation works**: All footer links clickable and functional
- [ ] **Breadcrumbs work**: (If used) Breadcrumbs navigate correctly
- [ ] **Internal links work**: Links between pages function correctly
- [ ] **External links work**: Links to external sites open properly
- [ ] **Links open correctly**: Internal links same window, external links new tab (as intended)
- [ ] **No broken links**: No 404 errors from internal links
- [ ] **CTA buttons work**: All call-to-action buttons lead to correct destinations

### Forms & User Input

- [ ] **Contact form present**: Contact form visible and accessible
- [ ] **Form fields appear**: All form fields display correctly
- [ ] **Form validation works**: Required fields validated
- [ ] **Form submits**: Submit button functions
- [ ] **Confirmation message**: Success message appears after submission
- [ ] **Email received**: Confirm submission email received by admin
- [ ] **No sensitive data exposed**: Password fields masked
- [ ] **CAPTCHA works** (if used): Anti-spam verification functional

### Interactive Elements

- [ ] **Buttons clickable**: All buttons respond to clicks
- [ ] **Hover effects work**: (If designed) Hover states appear correctly
- [ ] **Dropdowns functional**: (If used) Dropdown menus open/close properly
- [ ] **Search works** (if available): Search function returns results
- [ ] **Filters work** (if applicable): Content filtering functions correctly
- [ ] **Sorting works** (if applicable): Sort options work as expected
- [ ] **Animations smooth**: Any animations play smoothly without stuttering

---

## 2. Content Verification

Ensure all content is accurate and complete.

### Text Content

- [ ] **No placeholder text**: Lorem ipsum or temp content removed
- [ ] **No typos**: All text proofread for spelling errors
- [ ] **No grammar errors**: Professional language throughout
- [ ] **Complete sentences**: No truncated or incomplete text
- [ ] **Current dates**: All dates are current (no old dates)
- [ ] **Accurate phone numbers**: Phone numbers correct and clickable
- [ ] **Correct email addresses**: Email addresses current and functional
- [ ] **Consistent branding**: Brand name spelled correctly throughout

### Images & Media

- [ ] **All images display**: No missing or broken images
- [ ] **Images properly sized**: No distorted or stretched images
- [ ] **Images optimized**: File sizes reasonable (under 500KB for web)
- [ ] **Alt text present**: All images have descriptive alt text
- [ ] **Correct images**: Images match their context
- [ ] **Professional quality**: Images are high quality and brand-appropriate
- [ ] **Media files work** (if used): Videos play, audio files play
- [ ] **Captions present** (if needed): Videos have captions

### Page Structure

- [ ] **One H1 per page**: Exactly one main heading
- [ ] **Proper heading hierarchy**: H1 → H2 → H3 (no skipped levels)
- [ ] **Descriptive headings**: Headings describe content (not generic)
- [ ] **Logical flow**: Content organized sensibly
- [ ] **Scannable layout**: Headings, bullets, whitespace break up text
- [ ] **Complete pages**: No "under construction" or placeholder pages
- [ ] **Consistent formatting**: Similar page types formatted consistently

### Contact Information

- [ ] **Address current**: Physical address (if used) is correct
- [ ] **Phone number correct**: Phone number is current and accurate
- [ ] **Email correct**: Contact email is current and monitored
- [ ] **Business hours accurate**: Hours listed are correct (if applicable)
- [ ] **Contact form working**: Form receives submissions
- [ ] **Multiple contact methods**: Phone, email, and/or form available

---

## 3. Design & Visual Verification

Ensure visual design is polished and consistent.

### Layout & Spacing

- [ ] **Consistent margins**: Margins uniform across pages
- [ ] **Consistent padding**: Spacing within elements consistent
- [ ] **Whitespace adequate**: Page not too cramped or sparse
- [ ] **Alignment proper**: Elements aligned to grid or left/center/right
- [ ] **No overlapping**: Elements don't overlap unintentionally
- [ ] **Mobile friendly**: Layout adapts to small screens
- [ ] **Tablet friendly**: Layout works on medium screens
- [ ] **Desktop friendly**: Layout uses available space on large screens

### Colors & Typography

- [ ] **Brand colors used**: Colors match brand guidelines
- [ ] **Color contrast adequate**: Text readable on backgrounds
- [ ] **Typography consistent**: Limited font choices (2-3 fonts maximum)
- [ ] **Font sizes readable**: Base text 16px or larger
- [ ] **Line height adequate**: Vertical spacing between lines appropriate
- [ ] **Line length reasonable**: Text lines not too wide (max 80 characters)
- [ ] **Color accessible**: No red-green only color distinctions

### Visual Elements

- [ ] **Buttons distinct**: Buttons look clickable
- [ ] **Links underlined/styled**: Links visually distinct from text
- [ ] **Hover states work**: Links/buttons show hover effect
- [ ] **Icons appropriate**: Icons match their purpose
- [ ] **Borders/dividers clean**: Lines and dividers properly rendered
- [ ] **Backgrounds clean**: No odd texture or pattern issues
- [ ] **Visual hierarchy clear**: Important content stands out
- [ ] **Brand consistency**: Logo, fonts, colors match brand

---

## 4. Responsive Design Testing

Verify site works across all devices.

### Mobile (375px - 480px)

- [ ] **Readable on small screen**: Text size adequate
- [ ] **Buttons tappable**: Buttons large enough (min 44×44px)
- [ ] **No horizontal scroll**: Can view without scrolling left/right
- [ ] **Touch targets adequate**: Form inputs usable
- [ ] **Navigation accessible**: Menu works on mobile
- [ ] **Images scale properly**: No overlapping or distortion
- [ ] **Forms work**: Can fill and submit on mobile
- [ ] **CTAs visible**: Call-to-action prominent

### Tablet (600px - 900px)

- [ ] **Layout adapts**: Multi-column layout properly adjusted
- [ ] **Images appropriate**: Scale well for tablet size
- [ ] **Touch targets adequate**: Everything easily tappable
- [ ] **Text readable**: Font sizes appropriate for tablet
- [ ] **Whitespace balanced**: Not too cramped or sparse
- [ ] **Navigation functional**: Menu works on tablet
- [ ] **Forms work**: Input fields properly sized

### Desktop (1200px+)

- [ ] **Full design visible**: All design elements display
- [ ] **Content centered**: (If applicable) Content properly centered
- [ ] **Content not stretched**: Text lines not excessively long
- [ ] **Images crisp**: Larger images look sharp
- [ ] **Hover effects visible**: Hover states work properly
- [ ] **Multi-column layouts**: Columns properly distributed
- [ ] **Footer properly positioned**: Footer at bottom

### Orientation Testing

- [ ] **Portrait mode works**: Mobile in portrait orientation
- [ ] **Landscape mode works**: Mobile in landscape orientation
- [ ] **Tablet portrait works**: Tablet in portrait orientation
- [ ] **Tablet landscape works**: Tablet in landscape orientation

---

## 5. Browser Compatibility

Test in multiple browsers.

### Desktop Browsers

- [ ] **Chrome**: Latest version tested
  - [ ] All pages load
  - [ ] No console errors
  - [ ] Forms work
  - [ ] Styling correct

- [ ] **Safari**: Latest version tested
  - [ ] All pages load
  - [ ] No console errors
  - [ ] Forms work
  - [ ] Styling correct

- [ ] **Firefox**: Latest version tested
  - [ ] All pages load
  - [ ] No console errors
  - [ ] Forms work
  - [ ] Styling correct

- [ ] **Edge**: Latest version tested
  - [ ] All pages load
  - [ ] No console errors
  - [ ] Forms work
  - [ ] Styling correct

### Mobile Browsers

- [ ] **Chrome Mobile**: Android version tested
- [ ] **Safari Mobile**: iOS version tested
- [ ] **Mobile Firefox**: If supported

### Known Issues

Document any issues found:

| Browser | Issue | Severity | Status |
|---------|-------|----------|--------|
| | | | |

---

## 6. Performance Verification

Ensure site loads quickly and efficiently.

### Page Speed

- [ ] **Homepage loads < 3 seconds**: Use Google PageSpeed Insights
- [ ] **Other pages load quickly**: No excessively slow pages
- [ ] **Images optimized**: File sizes under 500KB
- [ ] **CSS minified**: No unnecessary whitespace
- [ ] **JavaScript minified**: Code is compressed
- [ ] **Caching enabled**: Browser caching configured
- [ ] **CDN enabled** (if applicable): Static files cached globally

### Core Web Vitals (Google's Metrics)

- [ ] **Largest Contentful Paint (LCP)**: < 2.5 seconds
- [ ] **First Input Delay (FID)**: < 100 milliseconds
- [ ] **Cumulative Layout Shift (CLS)**: < 0.1

### Performance Tools

Check using:
- [ ] Google PageSpeed Insights
- [ ] Google Lighthouse
- [ ] GTmetrix
- [ ] Browser DevTools (Network tab)

### Recommendations Implemented

- [ ] Address any "Critical" recommendations
- [ ] Address any "High" priority recommendations
- [ ] Defer non-critical "Medium" priority items

---

## 7. Security Verification

Protect your site and users.

### SSL/HTTPS

- [ ] **SSL certificate installed**: HTTPS active on all pages
- [ ] **No mixed content**: No "insecure content" warnings
- [ ] **Certificate valid**: Not expired
- [ ] **Certificate trusted**: From trusted certificate authority
- [ ] **Redirects to HTTPS**: HTTP redirects to HTTPS automatically

### Data Protection

- [ ] **Password fields masked**: Passwords show as dots/asterisks
- [ ] **No sensitive data in URLs**: Passwords/tokens not in address bar
- [ ] **Form data encrypted**: Submitted over HTTPS
- [ ] **Sensitive files protected**: Database files not web-accessible
- [ ] **Admin areas protected**: Admin login required to access

### User Permissions

- [ ] **Roles configured**: Admin, Editor, Contributor properly set
- [ ] **Permissions enforced**: Users can only access appropriate content
- [ ] **Public pages public**: Published pages accessible to all
- [ ] **Draft pages hidden**: Draft pages not accessible to public
- [ ] **Admin areas secured**: Not accessible without authentication

### Backups

- [ ] **Automated backups enabled**: Daily backups configured
- [ ] **Backup tested**: Verified backups can be restored
- [ ] **Backup location secure**: Stored separately from main server
- [ ] **Backup schedule documented**: Team knows backup process
- [ ] **Recovery process documented**: Know how to restore from backup

### Updates & Patches

- [ ] **CMS updated**: Running latest SkyCMS version
- [ ] **Plugins updated**: All extensions current
- [ ] **Dependencies updated**: Libraries and frameworks current
- [ ] **No known vulnerabilities**: Check security advisories

---

## 8. SEO Verification

Ensure site is discoverable in search engines.

### On-Page SEO

- [ ] **Page titles optimized**: 50-60 characters, includes keyword
- [ ] **Meta descriptions present**: 150-160 characters, compelling
- [ ] **Heading hierarchy correct**: One H1, proper H2/H3 structure
- [ ] **Keywords used naturally**: Not keyword-stuffed
- [ ] **Alt text on images**: All images have descriptive alt text
- [ ] **Internal links present**: Pages link to related content
- [ ] **URLs descriptive**: Not /page1, /temp, etc.
- [ ] **Mobile-friendly**: Responsive design implemented

### Technical SEO

- [ ] **Sitemap created**: XML sitemap generated
- [ ] **Sitemap submitted**: Submitted to Google Search Console
- [ ] **Robots.txt present**: Configured correctly
- [ ] **Structured data**: Schema markup implemented (if applicable)
- [ ] **Canonical tags**: Implemented to avoid duplicate content
- [ ] **Mobile index first**: Site optimized for mobile
- [ ] **No crawl errors**: No blocked resources

### Search Console & Analytics

- [ ] **Google Search Console**: Verified and connected
- [ ] **Google Analytics**: Installed and tracking traffic
- [ ] **Bing Webmaster**: Connected (optional but recommended)
- [ ] **Tracking code working**: Data being collected
- [ ] **Goals configured**: Analytics goals set up
- [ ] **No tracking errors**: Console shows no errors

---

## 9. Analytics & Monitoring

Set up tracking and monitoring.

### Google Analytics

- [ ] **Tracking code installed**: GA code on all pages
- [ ] **Tracking code working**: Real-time data shows visits
- [ ] **Goals created**: Key actions tracked
- [ ] **Conversions configured**: Goal completions tracked
- [ ] **Funnels created**: User flow tracking set up
- [ ] **Segments created**: Visitor segments configured

### Monitoring & Alerts

- [ ] **Uptime monitoring**: Service monitoring downtime
- [ ] **Error alerts**: Notified of site errors
- [ ] **Performance alerts**: Notified if site slows down
- [ ] **Security alerts**: Receive security notifications
- [ ] **Backup alerts**: Notified if backups fail

### Reporting Setup

- [ ] **Dashboard created**: Analytics dashboard for key metrics
- [ ] **Reports scheduled**: Auto-emails to stakeholders
- [ ] **KPIs identified**: Know what to measure success by

---

## 10. Accessibility Verification

Ensure site is usable by everyone.

### Keyboard Navigation

- [ ] **Tab order logical**: Can tab through interactive elements
- [ ] **Skip links present**: Skip to content link available
- [ ] **Focus visible**: Can see which element has focus
- [ ] **No keyboard traps**: Can tab out of all elements
- [ ] **Buttons keyboard accessible**: Buttons work with Enter/Space

### Screen Reader Compatibility

- [ ] **Images have alt text**: Described for screen readers
- [ ] **Form labels present**: Labels associated with inputs
- [ ] **Links descriptive**: Link text describes destination
- [ ] **Headings semantic**: Using H1-H6 properly, not for styling
- [ ] **ARIA labels** (if needed): Used for complex elements
- [ ] **No color only**: Information not conveyed by color alone

### Color & Contrast

- [ ] **Contrast ratio adequate**: Text readable (min 4.5:1)
- [ ] **Large text contrast**: (min 3:1)
- [ ] **No red-green only**: Not using only color to differentiate
- [ ] **Focus visible**: Focus indicators have adequate contrast

### Font & Text

- [ ] **Font readable**: No decorative fonts for body text
- [ ] **Font size adequate**: Base size 16px or larger
- [ ] **Line height adequate**: Vertical spacing between lines
- [ ] **Line length reasonable**: Max ~80 characters per line
- [ ] **Text not justified**: Left-aligned, not justified (for readability)

---

## 11. Legal & Compliance

Ensure legal requirements met.

### Legal Pages

- [ ] **Privacy Policy**: Present and complete
- [ ] **Terms of Service**: Present (if collecting data)
- [ ] **Cookie Policy**: Present (if using cookies)
- [ ] **Disclaimers**: Included if applicable
- [ ] **Copyright notice**: Footer copyright up-to-date

### Compliance

- [ ] **GDPR compliant**: If targeting EU users
- [ ] **CCPA compliant**: If targeting California users
- [ ] **Accessibility compliant**: WCAG 2.1 AA standard
- [ ] **Email compliance**: CAN-SPAM compliant (if marketing emails)
- [ ] **Cookie consent**: Consent obtained if using cookies

### Cookies & Tracking

- [ ] **Cookie banner present** (if needed): Users informed of cookies
- [ ] **Privacy settings available**: Users can control tracking
- [ ] **Consent obtained**: Tracked with consent management tool
- [ ] **Third-party scripts disclosed**: Analytics, ads, etc. disclosed

---

## 12. Launch Readiness

Final verification before launch.

### Team Readiness

- [ ] **Team trained**: Content creators trained on SkyCMS
- [ ] **Documentation ready**: User guides and training materials prepared
- [ ] **Support contacts identified**: Know who to contact for issues
- [ ] **Workflows established**: Publishing process documented
- [ ] **Communication plan ready**: Launch announcement prepared

### Infrastructure

- [ ] **Hosting ready**: Server configured and tested
- [ ] **Database ready**: Database backed up and verified
- [ ] **Email configured**: Contact forms can send emails
- [ ] **Domain points to site**: DNS records configured
- [ ] **CDN configured** (if used): Static files optimized
- [ ] **Load balancer ready** (if applicable): Traffic distribution ready

### Documentation

- [ ] **Admin guide**: Technical documentation complete
- [ ] **User guide**: Content creator documentation complete
- [ ] **API documentation** (if applicable): Available
- [ ] **Architecture documented**: System design documented
- [ ] **Credentials stored securely**: Passwords in secure vault

### Launch Plan

- [ ] **Launch date confirmed**: Team aligned on date/time
- [ ] **Launch window identified**: Off-peak time chosen
- [ ] **Launch communication**: Email/announcement scheduled
- [ ] **Post-launch support**: Team available for issues
- [ ] **Rollback plan**: Know how to revert if critical issues

---

## Summary & Sign-Off

### Verification Summary

**Total Items Checked**: ___ / ___

**Items Passed**: ___  
**Items In Progress**: ___  
**Items Failed**: ___  
**Items N/A**: ___

**Overall Status**: 
- [ ] ✅ Ready to Launch
- [ ] ⚠️ Ready with known issues (document below)
- [ ] ❌ Not Ready (resolve issues before launch)

### Known Issues

Document any unresolved items:

| Issue | Severity | Planned Fix | Owner | Due Date |
|-------|----------|-------------|-------|----------|
| | | | | |

### Sign-Offs

**QA/Testing Verified**: _________________ Date: _____  
**Technical Lead Approved**: _________________ Date: _____  
**Content Manager Approved**: _________________ Date: _____  
**Project Lead Approved**: _________________ Date: _____  

### Notes

```
[Additional notes about launch readiness, concerns, or successes]
```

---

## Resources

**Related Documentation:**
- [Phase 6: Preparing for Handoff](../06-Preparing-for-Handoff.md)
- [Website Launch Workflow](../Website-Launch-Workflow.md)

**Testing Tools:**
- [Google PageSpeed Insights](https://pagespeed.web.dev/)
- [Google Lighthouse](https://developers.google.com/web/tools/lighthouse)
- [GTmetrix](https://gtmetrix.com/)
- [Google Search Console](https://search.google.com/search-console)
- [Google Analytics](https://analytics.google.com/)

**Accessibility:**
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- [Web Accessibility Evaluation Tool (WAVE)](https://wave.webaim.org/)
- [Contrast Checker](https://webaim.org/resources/contrastchecker/)

---

**Congratulations on completing this comprehensive pre-launch checklist! Your website is now ready to launch with confidence.**