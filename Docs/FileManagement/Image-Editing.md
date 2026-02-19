---
title: Image Editing Guide
description: Powerful integrated image editor powered by Filerobot for file manager
keywords: image-editing, Filerobot, editor, cropping, filters, effects
audience: [content-creators, developers]
---

# Image Editing Guide

SkyCMS includes a powerful integrated image editor powered by Filerobot. This guide covers all the image editing capabilities available in the File Manager.

## Overview

The Filerobot image editor provides professional-level image editing without requiring external software. You can crop, resize, adjust colors, add annotations, apply filters, and much more - all from within your web browser.

## Accessing the Image Editor

### From List View

1. Navigate to the folder containing your image
2. Locate the image in the file list
3. Click the **Filerobot icon** (looks like an image editor tool)
4. The editor opens in a new view with your image loaded

### From Thumbnail View

1. Click **Show image thumbnails** to switch to gallery view
2. Find your image card
3. Click the **Filerobot icon** on the card
4. The editor opens immediately

### Supported Image Formats

The image editor supports the following formats:

- **JPEG/JPG** - Best for photographs
- **PNG** - Best for graphics with transparency
- **GIF** - Animated or simple graphics
- **WebP** - Modern format with better compression

## Editor Interface

When the image editor opens, you'll see:

- **Main canvas** - Your image in the center
- **Tool tabs** - Along the left side
- **Tool options** - Right panel when a tool is active
- **Save button** - Top right corner
- **Close button** - Top right, next to Save

## Editing Tools

### Adjust Tab

The Adjust tab provides basic image corrections:

**Brightness**

- Lighten or darken your image
- Range: -100 to +100
- Use for fixing underexposed or overexposed photos

**Contrast**

- Increase or decrease the difference between light and dark areas
- Range: -100 to +100
- Enhance details by increasing contrast

**Exposure**

- Fine-tune overall image brightness
- More subtle than brightness adjustment
- Useful for RAW or high-dynamic-range images

**Saturation**

- Control color intensity
- Reduce to create muted colors
- Increase for vibrant, punchy colors

**Hue**

- Shift colors around the color wheel
- Create artistic color effects
- Adjust white balance issues

**Shadows**

- Lighten or darken shadow areas specifically
- Recover detail in dark areas
- Create mood and atmosphere

**Highlights**

- Control the bright areas of your image
- Reduce blown-out highlights
- Add brightness to specific areas

**Warmth**

- Add or remove warm (orange) tones
- Simulate golden hour lighting
- Correct color temperature

### Annotate Tab

Add text, shapes, and drawings to your images:

**Text Tool**

1. Click the Text tool
2. Click on the image where you want text
3. Type your text
4. Customize:
   - Font family
   - Font size
   - Color
   - Alignment
   - Background

**Shape Tool**

Available shapes:

- **Rectangle** - Draw boxes and frames
- **Circle** - Create circular overlays
- **Triangle** - Add directional indicators
- **Polygon** - Custom multi-sided shapes

Shape options:

- Fill color
- Border color
- Border width
- Opacity

**Arrow Tool**

- Draw arrows to point out features
- Customize color and thickness
- Adjust arrow head size

**Line Tool**

- Draw straight lines
- Set color and thickness
- Create dividers or emphasis

**Pen Tool (Free Draw)**

- Draw freehand on your image
- Variable brush sizes
- Choose any color
- Create custom annotations

### Filters Tab

Apply Instagram-style filters and effects:

**Pre-built Filters:**

- **Vintage** - Classic aged photo look
- **Sepia** - Brown-toned antique effect
- **Grayscale** - Convert to black and white
- **Blur** - Soften the entire image
- **Sharpen** - Enhance edges and details

**Custom Filters:**

Each filter can be adjusted with sliders to control the intensity of the effect.

### Fine-Tune Tab

Advanced color and tone adjustments:

**RGB Channels**

- Adjust Red channel independently
- Adjust Green channel independently
- Adjust Blue channel independently
- Create color grading effects

**Gamma**

- Adjust mid-tone brightness
- More precise than overall brightness
- Useful for correcting display differences

**Color Curves**

- Advanced tone mapping
- Individual control over shadows, midtones, and highlights
- Professional-level color grading

### Resize Tab

Change your image dimensions:

**Custom Size**

1. Enter width and height in pixels
2. Lock aspect ratio to maintain proportions
3. Unlock to resize freely

**Preset Sizes**

Common sizes for:

- Social media posts
- Profile pictures
- Cover photos
- Standard web sizes

**Resize Options:**

- **Fit** - Scale to fit within dimensions
- **Fill** - Scale to fill dimensions (may crop)
- **Stretch** - Ignore aspect ratio

### Crop Tab

Remove unwanted areas of your image:

**Freeform Crop**

1. Click and drag handles to select crop area
2. Move the selection by dragging the center
3. Resize by dragging corners or edges
4. Apply crop

**Aspect Ratio Presets**

- **Square (1:1)** - Perfect for Instagram posts
- **Classic TV (4:3)** - Traditional photo ratio
- **Cinema (21:9)** - Widescreen format
- **Portrait (3:4)** - Vertical orientation
- **Landscape (4:3)** - Horizontal orientation

**Social Media Presets**

Pre-configured sizes for:

- Facebook profile pictures (180x180)
- Facebook cover photos (820x312)
- Instagram posts (1080x1080)
- Twitter headers
- LinkedIn banners

**Crop Tips:**

- Use the grid to apply rule of thirds
- Drag outside the selection to rotate
- Double-click to apply crop
- Click outside to cancel

### Watermark Tab

Protect your images with watermarks:

**Text Watermark**

1. Enter your watermark text
2. Choose font and size
3. Select color
4. Adjust opacity
5. Position on the image

**Image Watermark**

1. Upload your logo or watermark image
2. Scale to desired size
3. Set opacity (recommend 30-50% for subtlety)
4. Position on the image

**Watermark Positions:**

- Top-left
- Top-center
- Top-right
- Middle-left
- Center
- Middle-right
- Bottom-left
- Bottom-center
- Bottom-right

## Saving Your Work

### Save Process

1. Click the **Save** button in the top-right corner
2. The editor processes your changes
3. A progress indicator appears
4. When complete, the file is automatically saved to the server
5. Click **Close** to return to the File Manager

### What Happens When You Save

- The original image is **replaced** with your edited version
- The file keeps the same name and location
- Metadata (dimensions, size) is updated
- The CDN cache is automatically purged (if applicable)

### Important Notes

- **Saves are permanent** - The original is replaced
- **No undo after save** - Keep backups of important originals
- **Format is preserved** - JPEGs stay JPEG, PNGs stay PNG
- **Quality is optimized** - Images are compressed intelligently

## Common Image Editing Tasks

### Fixing Dark Photos

1. Open the image in the editor
2. Go to **Adjust** tab
3. Increase **Brightness** (+20 to +40)
4. Increase **Exposure** (+10 to +30)
5. Lift **Shadows** (+20 to +40)
6. Save your changes

### Creating Black and White Images

1. Open the image
2. Go to **Filters** tab
3. Select **Grayscale** filter
4. Adjust intensity if needed
5. Optional: Adjust contrast for more impact
6. Save

### Cropping for Social Media

1. Open the image
2. Go to **Crop** tab
3. Select the appropriate preset:
   - Instagram: Square (1:1)
   - Facebook Cover: 820x312
   - Profile Picture: 180x180
4. Position your subject in the crop area
5. Apply and save

### Adding Text to Images

1. Open the image
2. Go to **Annotate** tab
3. Select **Text** tool
4. Click where you want the text
5. Type your message
6. Customize font, size, and color
7. Position as needed
8. Save

### Reducing File Size

1. Open large image
2. Go to **Resize** tab
3. Enter smaller dimensions (e.g., 1920x1080 for web)
4. Lock aspect ratio
5. Save
6. File size is automatically optimized

### Brightening Faces

1. Open the portrait
2. Go to **Adjust** tab
3. Increase **Exposure** slightly (+10 to +20)
4. Lift **Shadows** (+15 to +30)
5. Reduce **Highlights** if face is too bright (-10 to -20)
6. Adjust **Warmth** for better skin tones (+5 to +15)
7. Save

## Tips and Best Practices

### Before You Edit

- **Keep a backup** - Download the original before major edits
- **Work on a copy** - For important images, copy first and edit the copy
- **Plan your changes** - Know what you want to achieve before starting

### While Editing

- **Make subtle adjustments** - Start with small changes and build up
- **Use presets as starting points** - Then customize to your needs
- **Preview at 100%** - Zoom in to check details
- **Consistent style** - Use similar edits for image sets

### After Editing

- **Check on different devices** - View results on phone and desktop
- **Test performance** - Ensure edited images load quickly
- **Document your workflow** - Note settings for repeatable results

### Professional Results

- **Less is more** - Avoid over-editing
- **Maintain quality** - Don't over-sharpen or over-saturate
- **Consider context** - Edit appropriately for where the image will be used
- **Be consistent** - Develop a style and stick with it

## Keyboard Shortcuts

While in the image editor:

- **Ctrl+Z / Cmd+Z** - Undo last action
- **Ctrl+Y / Cmd+Y** - Redo
- **Ctrl+S / Cmd+S** - Save
- **Delete** - Remove selected annotation
- **Escape** - Cancel current tool

## Troubleshooting

### Image Won't Load

**Problem:** Editor opens but image doesn't appear

**Solutions:**
- Check image file size (very large images may timeout)
- Try a smaller version of the image
- Check image format is supported
- Refresh the page and try again

### Can't Save Changes

**Problem:** Save button doesn't work or fails

**Solutions:**
- Check your internet connection
- Verify you have write permissions
- Ensure sufficient storage space
- Try saving again after a few seconds
- Reduce image quality/size if file is very large

### Edits Look Different

**Problem:** Saved image looks different from preview

**Solutions:**
- Check color profile settings on your display
- View on multiple devices to compare
- Consider monitor calibration
- Adjust edit values and re-save

### Editor is Slow

**Problem:** Editor is laggy or unresponsive

**Solutions:**
- Close other browser tabs
- Work with smaller images
- Restart your browser
- Clear browser cache
- Try a different browser

## Advanced Techniques

### Creating Thumbnails

1. Open your full-size image
2. Go to **Resize** tab
3. Enter thumbnail dimensions (e.g., 300x300)
4. Select **Fill** mode for cropping to fit
5. Save with a different name if possible

### Batch Consistency

For editing multiple related images:

1. Edit your first image
2. Note all the adjustment values you used
3. Open subsequent images
4. Apply the same adjustment values
5. Make minor tweaks as needed
6. Results will be consistent across all images

### Color Correction

For off-color images:

1. Start with **Warmth** to fix color temperature
2. Adjust **Hue** for overall color shift
3. Use **Saturation** to control color intensity
4. Fine-tune with **RGB channels** if needed
5. Check on neutral areas (white, gray) for accuracy

---

For more information about the File Manager, see the [main documentation](index.md).
