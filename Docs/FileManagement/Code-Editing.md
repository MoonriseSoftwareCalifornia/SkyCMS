---
title: Code Editing Guide
description: Code editor guide using Monaco Editor for HTML, CSS, JavaScript, and more
keywords: code-editor, Monaco, VS-Code, syntax-highlighting, intellisense
audience: [developers]
---

# Code Editing Guide

SkyCMS includes a powerful code editor based on Microsoft's Monaco Editor (the same editor that powers Visual Studio Code). This guide covers all the code editing capabilities available in the File Manager.

## Overview

The integrated code editor provides a professional development environment right in your browser, with features like syntax highlighting, code completion, and error detection. You can edit HTML, CSS, JavaScript, JSON, XML, and plain text files.

## Accessing the Code Editor

### Opening Files

1. Navigate to the folder containing your code file
2. Locate a supported file (`.html`, `.css`, `.js`, `.json`, `.xml`, `.txt`)
3. Click the **Monaco/VS Code editor icon** (looks like code brackets)
4. The editor opens with syntax highlighting automatically applied

### Supported File Types

The code editor supports the following file extensions:

- **`.js`** - JavaScript files
- **`.css`** - Cascading Style Sheets
- **`.html` / `.htm`** - HTML documents
- **`.json`** - JSON data files
- **`.xml`** - XML documents
- **`.txt`** - Plain text files

## Editor Interface

When the code editor opens, you'll see:

### Top Bar

- **File tabs** - Switch between multiple open files
- **File name** - Current file being edited
- **Save button** - Click to save changes
- **Other toolbar buttons** - Preview, files, etc.

### Main Editor Area

- **Line numbers** - Left margin showing line numbers
- **Code area** - Where you write your code
- **Syntax highlighting** - Colors indicating code structure
- **Scrollbars** - For navigating large files

### Status Bar (Bottom)

- **Current line/column** - Your cursor position
- **File type** - Current syntax mode
- **Encoding** - File character encoding

## Editor Features

### Syntax Highlighting

Code is automatically colored based on its meaning:

**JavaScript Example:**
- Keywords (function, var, const) - Purple/Pink
- Strings - Orange/Red
- Numbers - Green
- Comments - Gray/Green

**HTML Example:**
- Tags - Blue
- Attributes - Light Blue
- Values - Orange
- Text content - White

**CSS Example:**
- Selectors - Yellow
- Properties - Light Blue
- Values - Orange
- Comments - Green

### Code Completion (IntelliSense)

The editor suggests completions as you type:

1. Start typing a keyword or function name
2. A popup appears with suggestions
3. Use arrow keys to select
4. Press Enter or Tab to accept

**HTML:**
- Tag names auto-complete
- Attribute names suggested
- Closing tags added automatically

**CSS:**
- Property names suggested
- Value options shown
- Vendor prefixes offered

**JavaScript:**
- Built-in functions suggested
- Common patterns offered
- Variable names remembered

### Emmet Abbreviations

For HTML, use Emmet shortcuts for rapid coding:

**Examples:**

```
div.container>ul>li*5
```
Expands to:
```html
<div class="container">
  <ul>
    <li></li>
    <li></li>
    <li></li>
    <li></li>
    <li></li>
  </ul>
</div>
```

**Common Emmet Shortcuts:**
- `!` - Full HTML5 boilerplate
- `div.class#id` - Div with class and ID
- `ul>li*3` - Unordered list with 3 items
- `p{Text content}` - Paragraph with text
- `a[href=#]` - Link with href attribute

### Code Folding

Collapse sections of code for better overview:

1. Look for the small arrow icons in the left margin
2. Click to collapse a code block
3. Click again to expand
4. Useful for large files with many functions

### Bracket Matching

The editor helps you track brackets and parentheses:

- **Matching pairs** - Highlighted when cursor is near
- **Auto-closing** - Closing bracket added automatically
- **Smart selection** - Select entire bracketed content easily

### Multi-Cursor Editing

Edit multiple locations simultaneously:

**Add cursors:**
- Hold Alt and click to add cursors
- Alt+Shift+Down - Add cursor below
- Alt+Shift+Up - Add cursor above

**Select multiple:**
- Ctrl+D - Select next occurrence of current word
- Ctrl+Shift+L - Select all occurrences

### Find and Replace

**Find:**
1. Press Ctrl+F
2. Enter search term
3. Use arrows to navigate matches
4. Press Escape to close

**Find and Replace:**
1. Press Ctrl+H
2. Enter search term
3. Enter replacement
4. Click Replace or Replace All

**Find Options:**
- Match case
- Match whole word
- Use regular expressions

### Code Formatting

**Auto-format:**
- Right-click and select "Format Document"
- Automatically indents and organizes code
- Applies consistent styling

**Manual formatting:**
- Tab - Indent selected lines
- Shift+Tab - Un-indent selected lines
- Ctrl+] - Indent current line
- Ctrl+[ - Un-indent current line

### Comments

**Toggle line comment:**
- Select lines
- Press Ctrl+/ (or Cmd+/)
- Lines are commented/uncommented

**Block comment:**
- Select text
- Press Shift+Alt+A
- Wraps selection in block comment

## Saving Your Work

### Manual Save

**Method 1: Button**
1. Click the **Save** button in the toolbar
2. Wait for confirmation message

**Method 2: Keyboard**
1. Press **Ctrl+S** (Windows/Linux) or **Cmd+S** (Mac)
2. Instant save

### Auto-Save

Enable auto-save for automatic saving:

1. Look for the auto-save toggle in settings
2. When enabled, changes save automatically
3. Small delay after you stop typing
4. Visual indicator shows when auto-saving

**Benefits:**
- Never lose work
- No need to remember to save
- Seamless editing experience

**Considerations:**
- May save incomplete changes
- Best for development, not production files
- Disable for critical files

## Common Editing Tasks

### Creating a New HTML File

1. In File Manager, click **New file**
2. Name it with `.html` extension (e.g., `page.html`)
3. Click **Create**
4. In the editor, type `!` and press Tab for HTML5 template
5. Fill in your content
6. Save with Ctrl+S

### Editing CSS Styles

1. Open your `.css` file in the editor
2. Use code completion for property names
3. Edit values (colors, sizes, etc.)
4. Save changes
5. Refresh your webpage to see results

**Tips:**
- Use color picker for color values
- Ctrl+Space for property suggestions
- Copy existing rules as templates

### Editing JavaScript

1. Open your `.js` file
2. Use syntax highlighting to catch errors
3. Leverage code completion for functions
4. Add comments with Ctrl+/
5. Save and test in browser

**Best Practices:**
- Add comments for complex logic
- Use consistent indentation
- Test changes incrementally
- Keep backups of working versions

### Working with JSON

1. Open your `.json` file
2. Editor validates JSON syntax
3. Errors highlighted in red
4. Auto-format with right-click menu
5. Save to apply changes

**JSON Tips:**
- Strings must use double quotes
- No trailing commas allowed
- Format for readability
- Validate before saving

### Bulk Edits with Multi-Cursor

**Example: Change all instances of a class name**

1. Place cursor on the class name
2. Press Ctrl+D repeatedly to select each occurrence
3. Type the new name
4. All instances change simultaneously
5. Save changes

## Keyboard Shortcuts

### Essential Shortcuts

**File Operations:**
- Ctrl+S / Cmd+S - Save
- Ctrl+F / Cmd+F - Find
- Ctrl+H / Cmd+H - Replace
- Ctrl+Z / Cmd+Z - Undo
- Ctrl+Y / Cmd+Y - Redo

**Editing:**
- Ctrl+C / Cmd+C - Copy
- Ctrl+X / Cmd+X - Cut
- Ctrl+V / Cmd+V - Paste
- Ctrl+A / Cmd+A - Select all
- Ctrl+/ / Cmd+/ - Toggle comment

**Navigation:**
- Ctrl+Home - Go to start of file
- Ctrl+End - Go to end of file
- Ctrl+G - Go to line number
- Ctrl+F - Find in file

**Multi-Cursor:**
- Alt+Click - Add cursor
- Ctrl+D - Select next occurrence
- Ctrl+Shift+L - Select all occurrences
- Alt+Shift+Down - Add cursor below
- Alt+Shift+Up - Add cursor above

**Code Formatting:**
- Tab - Indent
- Shift+Tab - Un-indent
- Shift+Alt+F - Format document
- Ctrl+] - Increase indent
- Ctrl+[ - Decrease indent

## Tips and Best Practices

### Before You Edit

**Create backups:**
- Download important files before major changes
- Keep working versions separate from production
- Test changes in development environment first

**Understand the code:**
- Read through the file before editing
- Understand how it relates to other files
- Check for dependencies

**Plan your changes:**
- Know what you want to accomplish
- Consider impact on other parts of the site
- Make one change at a time

### While Editing

**Write clean code:**
- Use consistent indentation (2 or 4 spaces)
- Add comments for complex sections
- Use meaningful variable and function names
- Keep functions short and focused

**Test frequently:**
- Save and test after each significant change
- Don't make too many changes before testing
- Use browser developer tools to debug

**Use features:**
- Leverage code completion to avoid typos
- Use find/replace for bulk changes
- Format code regularly for readability

### After Editing

**Verify changes:**
- Refresh your webpage to see changes
- Test on different browsers if possible
- Check for console errors

**Document changes:**
- Add comments explaining what you changed
- Keep notes about configuration changes
- Update documentation if needed

**Monitor performance:**
- Check page load times
- Validate HTML/CSS/JavaScript
- Optimize if issues arise

## Troubleshooting

### Editor Won't Load

**Problem:** Editor doesn't open or shows blank screen

**Solutions:**
- Clear browser cache and cookies
- Try a different browser (Chrome, Firefox, Edge)
- Disable browser extensions temporarily
- Check internet connection
- Refresh the page and try again

### Can't Save Changes

**Problem:** Save button doesn't work or shows error

**Solutions:**
- Check if file is locked by another user
- Verify you have write permissions
- Check for syntax errors that prevent saving
- Try copying code, refreshing, and pasting back
- Contact administrator if permissions issue

### Syntax Highlighting Not Working

**Problem:** Code appears as plain text without colors

**Solutions:**
- Check file extension is correct
- Try closing and reopening the editor
- Clear browser cache
- Make sure file type is supported

### Code Completion Not Appearing

**Problem:** No suggestions shown while typing

**Solutions:**
- Trigger manually with Ctrl+Space
- Check you're in a context where completion applies
- Wait a moment - it may appear with slight delay
- Restart editor if issue persists

### Formatting Looks Wrong

**Problem:** Code indentation or spacing is incorrect

**Solutions:**
- Select all (Ctrl+A) and format (Shift+Alt+F)
- Check tab settings in editor preferences
- Manually adjust indentation
- Copy to external editor, format, and paste back

### Lost Changes

**Problem:** Edits disappeared or weren't saved

**Solutions:**
- Check browser history - editor may have auto-saved
- Look for auto-recovery or backup features
- Always use Ctrl+S to save explicitly
- Enable auto-save to prevent future loss

## Advanced Techniques

### Regular Expressions in Find/Replace

Use regex for powerful search and replace:

**Example: Convert all color codes to uppercase**
- Find: `#([0-9a-f]{6})`
- Replace: `#$1` (with uppercase option enabled)

**Example: Add semicolons to variable declarations**
- Find: `var (\w+) = (.+)$`
- Replace: `var $1 = $2;`

### Code Snippets

Create reusable code templates:

**CSS Reset Snippet:**
```css
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}
```

**jQuery Document Ready:**
```javascript
$(document).ready(function() {
  // Your code here
});
```

### Working with Multiple Files

When editing related files:

1. Open first file in editor
2. Make edits and save
3. Open second file (new tab)
4. Switch between tabs as needed
5. Save all before testing

### Comparing Code Versions

To compare changes:

1. Copy original code before editing
2. Make your changes
3. Use external diff tool to compare
4. Review what changed before deploying

### Debugging JavaScript

Find and fix JavaScript errors:

1. Use browser developer tools (F12)
2. Check Console tab for errors
3. Set breakpoints in debugger
4. Step through code execution
5. Fix issues in Monaco editor
6. Save and test again

## Integration with SkyCMS

### Article-Specific Files

When editing from an article:

- Editor opens in article's file folder
- Quick access to article resources
- Changes immediately available to article
- Auto-refresh can update previews

### Template Files

For template editing:

- Navigate to template folder
- Edit template HTML/CSS/JS
- Save to update template
- All pages using template are affected

### Global Files

Editing site-wide resources:

- CSS in `/pub/css/`
- JavaScript in `/pub/js/`
- Affects entire site
- Test thoroughly before saving

### CDN Integration

When CDN is enabled:

- Saved files purge CDN cache
- Changes propagate automatically
- May take a few moments to update
- Clear browser cache if changes don't appear

---

For more information about the File Manager, see the [main documentation](index.md).

For image editing capabilities, see the [Image Editing Guide](Image-Editing.md).
