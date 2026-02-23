/**
 * Cosmos CMS Image Widget (Enhanced)
 * 
 * Provides an interactive image upload and management widget for the Cosmos CMS editor.
 * Uses FilePond for drag-and-drop file uploads with automatic save integration.
 * 
 * REQUIREMENTS:
 * - FilePond library and FilePondPluginFileMetadata plugin must be loaded before this script
 * - Parent page may optionally define:
 *   - parent.saveInProgress [optional] - Boolean flag set to false after upload completes
 *   - articleNumber [optional] - Global variable for article context (determines upload path)
 * 
 * USAGE:
 * 1. Add a div with data-editor-config="image-widget" and data-ccms-ceid="unique-id"
 *    Example: <div data-editor-config="image-widget" data-ccms-ceid="img-123"></div>
 * 
 * 2. For new widgets (will auto-generate ID), use: data-ccms-new="true"
 *    Example: <div data-editor-config="image-widget" data-ccms-new="true"></div>
 * 
 * 3. To pre-populate with an image, include an <img> tag inside:
 *    <div data-editor-config="image-widget" data-ccms-ceid="img-123">
 *      <img src="/pub/images/photo.jpg" class="ccms-img-widget-img" alt="Description" />
 *    </div>
 * 
 * 4. Enable the Alt/Title editor (optional, per widget):
 *    Add data-ccms-enable-alt-editor="true" to the widget container to enable the built-in
 *    modal editor for Alt and Title attributes.
 *    Example:
 *    <div data-editor-config="image-widget"
 *         data-ccms-ceid="img-123"
 *         data-ccms-enable-alt-editor="true"></div>
 * 
 * CONFIGURATION:
 * - Upload endpoint: /FileManager/UploadImage
 * - Image library endpoint: /FileManager/GetImageAssets
 * - Accepted file types: PNG, JPG, JPEG, WebP, GIF
 * - Max file size: 25MB (enforced server-side)
 * - Upload path: /pub/articles/{articleNumber}/ (if articleNumber exists) or /pub/images/
 * - Alt editor behavior:
 *   - Global UI toggle: CCMS_IMAGE_WIDGET_CONFIG.enableAltTextEditor (default: true) controls whether
 *     the edit button is shown and whether the editor is auto-prompted after upload.
 *   - Per-widget activation: the editor only opens when the widget element has
 *     data-ccms-enable-alt-editor="true". If absent or not "true", the button will have no effect and
 *     auto-prompt after upload is skipped.
 * 
 * WIDGET DATA ATTRIBUTES:
 * - data-ccms-ceid: Required stable id for existing widgets.
 * - data-ccms-new="true": Auto-generate a new id for newly inserted widgets.
 * - data-ccms-enable-alt-editor="true" [optional]: Enables the Alt/Title editor modal for this widget.
 *   Defaults to disabled when not present.
 * 
 * FEATURES:
 * - Alt text editor for accessibility (per-widget opt-in via data-ccms-enable-alt-editor)
 * - Drag-and-drop image replacement
 * - Upload progress indicator
 * - Image library browser for reusing uploaded images
 * - Enhanced error handling with user-friendly messages
 * - Custom event system (CCMSImageWidgetEvents) for responding to image lifecycle events
 * 
 * CUSTOM EVENTS:
 * - The widget exposes a global event dispatcher: window.CCMSImageWidgetEvents
 *   - Use this to register custom functions that trigger after an image is uploaded or deleted.
 *   - Supported event: 'imageChanged'
 *     - Fired after an image is uploaded or deleted.
 *     - Callback receives an object: { type, id, element, imageSrc }
 *         - type: 'uploaded' or 'deleted'
 *         - id: widget id (data-ccms-ceid)
 *         - element: widget container HTMLElement
 *         - imageSrc: image src (for uploaded), undefined for deleted
 *   - Example usage:
 *       window.CCMSImageWidgetEvents.on('imageChanged', function(info) {
 *           // info.type: 'uploaded' or 'deleted'
 *           // info.id: widget id
 *           // info.element: widget container
 *           // info.imageSrc: image src (for uploaded), undefined for deleted
 *           // Your custom logic here
 *       });
 */

// ============================================================================
// AUTO-LOAD REQUIRED STYLESHEETS
// ============================================================================

/**
 * Automatically loads required CSS files if not already present.
 */
(function ccms___autoLoadStyles() {
    const requiredStyles = [
        {
            id: 'ccms-font-awesome-css',
            href: 'https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.0.0/css/all.min.css',
            integrity: 'sha512-9usAa10IRO0HhonpyAIVpjrylPvoDwiPUiKdWk5t3PyolY1cOd4DSE0Ga+ri4AuTroPR5aQvXU9xC6qOPnzFeg==',
            crossorigin: 'anonymous'
        },
        {
            id: 'ccms-image-widget-css',
            href: '/lib/cosmos/image-widget/image-widget.css',
            appendVersion: true // Will add ?v=timestamp
        }
    ];

    requiredStyles.forEach(style => {
        // Check if already loaded
        if (document.getElementById(style.id)) {
            return;
        }

        // Create link element
        const link = document.createElement('link');
        link.id = style.id;
        link.rel = 'stylesheet';
        
        // Add cache-busting version parameter if needed
        let href = style.href;
        if (style.appendVersion) {
            const timestamp = new Date().getTime();
            href += `?v=${timestamp}`;
        }
        link.href = href;

        // Add optional attributes
        if (style.integrity) {
            link.integrity = style.integrity;
        }
        if (style.crossorigin) {
            link.crossOrigin = style.crossorigin;
        }

        // Append to head
        document.head.appendChild(link);
        console.log(`Auto-loaded stylesheet: ${style.id}`);
    });
})();

// ============================================================================
// CONSTANTS AND CONFIGURATION
// ============================================================================

if (typeof window.CCMS_IMAGE_WIDGET_CONFIG === "undefined") {
    window.CCMS_IMAGE_WIDGET_CONFIG = CCMS_IMAGE_WIDGET_CONFIG = {
        uploadEndpoint: '/FileManager/UploadImage',
        imageLibraryEndpoint: '/FileManager/GetImageAssets',
        acceptedFileTypes: ['image/png', 'image/jpg', 'image/jpeg', 'image/webp', 'image/gif'],
        maxFileSize: 26214400, // 25MB in bytes (server enforces, this is for reference)
        defaultUploadPath: '/pub/images/',
        articleUploadPath: '/pub/articles/',
        trashIconHtml: '<i class="fa-solid fa-trash"></i>',
        editIconHtml: '<i class="fa-solid fa-edit"></i>',
        libraryIconHtml: '<i class="fa-solid fa-images"></i>',
        replaceIconHtml: '<i class="fa-solid fa-sync"></i>',
        placeholderImage: '/images/AddImageHere.webp',
        zIndexOffset: 1000, // Safe z-index for overlays
        showProgressBar: true,
        enableAltTextEditor: true,      // Add this
        enableDragReplace: true,         // Add this
        enableImageLibrary: true         // Add this
    };
}

// Shared GUID generator with fallback if the shared helper is not loaded yet.
const ccmsGenerateGuid = (typeof window !== 'undefined' && window.ccmsGenerateGuid)
    ? window.ccmsGenerateGuid
    : function ccmsGenerateGuidFallback() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    };

if (typeof window !== 'undefined') {
    window.ccmsGenerateGuid = ccmsGenerateGuid;
    window.ccms__generateGUID = ccmsGenerateGuid;
    window.ccms___generateGUID = ccmsGenerateGuid;
}

// ============================================================================
// UTILITY FUNCTIONS
// ============================================================================

/**
 * Creates a debounced version of a function that delays execution.
 * @param {Function} func - The function to debounce
 * @param {number} wait - Milliseconds to wait before execution
 * @returns {Function} The debounced function
 */
function ccms___debounce(func, wait) {
    let timeout;
    return function (...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), wait);
    };
}

/**
 * Extracts image dimensions from a file blob using the Image API.
 * @param {Blob} blob - The image file blob
 * @param {Function} callback - Callback receiving {width, height} object
 */
function ccms___getImageDimensions(blob, callback) {
    const reader = new FileReader();
    reader.onload = function (e) {
        const img = new Image();
        img.onload = function () {
            const dimensions = {
                width: img.naturalWidth,
                height: img.naturalHeight
            };
            callback(dimensions);
        };
        img.onerror = function () {
            console.error('Failed to load image for dimension detection');
            callback({ width: 0, height: 0 });
        };
        img.src = e.target.result;
    };
    reader.onerror = function () {
        console.error('Failed to read image file');
        callback({ width: 0, height: 0 });
    };
    reader.readAsDataURL(blob);
}

/**
 * Shows a user-friendly error notification.
 * @param {string} message - Error message to display
 * @param {HTMLElement} container - Container to show error in
 */
function ccms___showError(message, container) {
    const errorDiv = document.createElement('div');
    errorDiv.classList.add('ccms-img-widget-error');
    errorDiv.innerHTML = `
        <div class="alert alert-danger alert-dismissible fade show" role="alert">
            <strong>Error:</strong> ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>
    `;

    container.appendChild(errorDiv);

    // Auto-dismiss after 5 seconds
    setTimeout(() => {
        errorDiv.remove();
    }, 5000);
}

/**
 * Shows an informational notice (non-error) to the user.
 * @param {string} message - Message to display
 * @param {HTMLElement} container - Container to show message in
 */
function ccms___showInfo(message, container) {
    const infoDiv = document.createElement('div');
    infoDiv.classList.add('ccms-img-widget-info');
    infoDiv.innerHTML = `
        <div class="alert alert-info alert-dismissible fade show" role="alert">
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>
    `;

    container.appendChild(infoDiv);

    // Auto-dismiss after 4 seconds
    setTimeout(() => {
        infoDiv.remove();
    }, 4000);
}

/**
 * Validates a file before upload.
 * @param {File} file - File to validate
 * @returns {{valid: boolean, error: string|null}} Validation result
 */
function ccms___validateFile(file) {
    // Check file size
    if (file.size > CCMS_IMAGE_WIDGET_CONFIG.maxFileSize) {
        return {
            valid: false,
            error: `File size (${(file.size / 1048576).toFixed(2)}MB) exceeds maximum allowed size (25MB).`
        };
    }

    // Check file type
    const extension = '.' + file.name.split('.').pop().toLowerCase();
    const validExtensions = ['.png', '.jpg', '.jpeg', '.webp', '.gif'];

    if (!validExtensions.includes(extension)) {
        return {
            valid: false,
            error: `File type "${extension}" is not supported. Please use: ${validExtensions.join(', ')}`
        };
    }

    return { valid: true, error: null };
}

/**
 * Gets the image source URL from an image widget.
 * @param {string|HTMLElement} widgetIdentifier - Either the widget container element, 
 *        the element's ID, or the data-ccms-ceid attribute value
 * @returns {string|null} The image src URL, or null if no image exists
 * @example
 * // Using element
 * const element = document.getElementById('my-widget');
 * const imageSrc = ccms___getImageWidgetSrc(element);
 * 
 * // Using data-ccms-ceid value
 * const imageSrc = ccms___getImageWidgetSrc('img-123');
 * 
 * // Using element ID
 * const imageSrc = ccms___getImageWidgetSrc('my-widget');
 */
function ccms___getImageWidgetSrc(widgetIdentifier) {
    let widgetElement = null;

    // Check if it's already an HTMLElement
    if (widgetIdentifier instanceof HTMLElement) {
        widgetElement = widgetIdentifier;
    } else if (typeof widgetIdentifier === 'string') {
        // Try to find by element ID first
        widgetElement = document.getElementById(widgetIdentifier);

        // If not found, try to find by data-ccms-ceid attribute
        if (!widgetElement) {
            widgetElement = document.querySelector(`[data-ccms-ceid="${widgetIdentifier}"]`);
        }
    }

    // Validate we found a widget element
    if (!widgetElement) {
        console.warn(`Image widget not found: ${widgetIdentifier}`);
        return null;
    }

    // Find the image element inside the widget
    const img = widgetElement.querySelector('img');

    // Return the src if image exists, otherwise null
    if (img && img.src) {
        return img.src;
    }

    return null;
}

// Export to window for global access
if (typeof window !== 'undefined') {
    window.ccms___getImageWidgetSrc = ccms___getImageWidgetSrc;
    window.ccmsGetImageWidgetSrc = ccms___getImageWidgetSrc; // Shorter alias
}

// ============================================================================
// ALT TEXT EDITOR
// ============================================================================

/**
 * Shows the alt text editor modal for an image.
 * @param {HTMLElement} imageElement - The image element to edit
 * @param {HTMLElement} widgetContainer - The widget container element
 */
function ccms___showAltTextEditor(imageElement, widgetContainer) {
    const enableEditor = widgetContainer.getAttribute('data-ccms-enable-alt-editor');
    if (enableEditor === null || enableEditor !== 'true') {
        return;
    }
    const id = widgetContainer.getAttribute('data-ccms-ceid');
    const currentAlt = imageElement.getAttribute('alt') || '';
    const currentTitle = imageElement.getAttribute('title') || '';

    // Escape HTML entities for safe attribute insertion
    const escapeHtml = (text) => {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML.replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    };

    const escapedAlt = escapeHtml(currentAlt);
    const escapedTitle = escapeHtml(currentTitle);

    // Create modal
    const modal = document.createElement('div');
    modal.classList.add('ccms-img-widget-modal');
    modal.innerHTML = `
        <div class="ccms-modal-overlay"></div>
        <div class="ccms-modal-dialog">
            <div class="ccms-modal-content">
                <div class="ccms-modal-header">
                    <h5>Edit Image Properties</h5>
                    <button type="button" class="ccms-modal-close" aria-label="Close">
                        <i class="fa-solid fa-times"></i>
                    </button>
                </div>
                <div class="ccms-modal-body">
                    <div class="mb-3">
                        <label for="ccms-alt-text-${id}" class="form-label">
                            Alt Text <span class="text-danger">*</span>
                            <small class="text-muted">(Required for accessibility)</small>
                        </label>
                        <input 
                            type="text" 
                            class="form-control" 
                            id="ccms-alt-text-${id}" 
                            value="${escapedAlt}"
                            placeholder="Describe this image..."
                            maxlength="255"
                        />
                        <small class="form-text text-muted">
                            Describe the image for screen readers and SEO (max 255 characters).
                        </small>
                    </div>
                    <div class="mb-3">
                        <label for="ccms-title-text-${id}" class="form-label">
                            Title (Optional)
                        </label>
                        <input 
                            type="text" 
                            class="form-control" 
                            id="ccms-title-text-${id}" 
                            value="${escapedTitle}"
                            placeholder="Additional information..."
                            maxlength="255"
                        />
                        <small class="form-text text-muted">
                            Appears as a tooltip when hovering over the image.
                        </small>
                    </div>
                    <div class="mb-3">
                        <img src="${imageElement.src}" class="img-thumbnail" style="max-width: 100%; max-height: 200px;" alt="Preview" />
                    </div>
                </div>
                <div class="ccms-modal-footer">
                    <button type="button" class="btn btn-secondary ccms-modal-cancel">Cancel</button>
                    <button type="button" class="btn btn-primary ccms-modal-save">Save Changes</button>
                </div>
            </div>
        </div>
    `;

    document.body.appendChild(modal);

    // Focus the alt text input
    const altInput = modal.querySelector(`#ccms-alt-text-${id}`);
    altInput.focus();
    altInput.select();

    // Close handlers
    const closeModal = () => modal.remove();
    modal.querySelector('.ccms-modal-close').addEventListener('click', closeModal);
    modal.querySelector('.ccms-modal-cancel').addEventListener('click', closeModal);
    modal.querySelector('.ccms-modal-overlay').addEventListener('click', closeModal);

    // Save handler
    modal.querySelector('.ccms-modal-save').addEventListener('click', () => {
        const newAlt = modal.querySelector(`#ccms-alt-text-${id}`).value.trim();
        const newTitle = modal.querySelector(`#ccms-title-text-${id}`).value.trim();

        if (!newAlt) {
            altInput.classList.add('is-invalid');
            return;
        }

        // Update image attributes
        imageElement.setAttribute('alt', newAlt);

        if (newTitle) {
            imageElement.setAttribute('title', newTitle);
        } else {
            imageElement.removeAttribute('title');
        }

        closeModal();
    });

    // Enter key to save
    altInput.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') {
            modal.querySelector('.ccms-modal-save').click();
        }
    });
}

// ============================================================================
// IMAGE LIBRARY BROWSER
// ============================================================================

/**
 * Shows the image library browser modal.
 * @param {HTMLElement} widgetContainer - The widget container element
 * @param {Function} onSelect - Callback when image is selected
 */
async function ccms___showImageLibrary(widgetContainer, onSelect) {
    const id = widgetContainer.getAttribute('data-ccms-ceid');

    // Create modal
    const modal = document.createElement('div');
    modal.classList.add('ccms-img-widget-modal', 'ccms-img-library-modal');
    modal.innerHTML = `
        <div class="ccms-modal-overlay"></div>
        <div class="ccms-modal-dialog ccms-modal-lg">
            <div class="ccms-modal-content">
                <div class="ccms-modal-header">
                    <h5>Image Library</h5>
                    <button type="button" class="ccms-modal-close" aria-label="Close">
                        <i class="fa-solid fa-times"></i>
                    </button>
                </div>
                <div class="ccms-modal-body">
                    <div class="mb-3">
                        <input 
                            type="text" 
                            class="form-control" 
                            id="ccms-library-search-${id}" 
                            placeholder="Search images..."
                        />
                    </div>
                    <div class="ccms-library-loading text-center py-5">
                        <div class="spinner-border text-primary" role="status">
                            <span class="visually-hidden">Loading...</span>
                        </div>
                        <p class="mt-2">Loading images...</p>
                    </div>
                    <div class="ccms-library-grid" style="display: none;"></div>
                    <div class="ccms-library-empty text-center py-5" style="display: none;">
                        <p class="text-muted">No images found in library.</p>
                    </div>
                </div>
                <div class="ccms-modal-footer">
                    <button type="button" class="btn btn-secondary ccms-modal-cancel">Cancel</button>
                </div>
            </div>
        </div>
    `;

    document.body.appendChild(modal);

    // Close handlers
    const closeModal = () => modal.remove();
    modal.querySelector('.ccms-modal-close').addEventListener('click', closeModal);
    modal.querySelector('.ccms-modal-cancel').addEventListener('click', closeModal);
    modal.querySelector('.ccms-modal-overlay').addEventListener('click', closeModal);

    // Load images from library
    try {
        const uploadPath = (typeof articleNumber !== 'undefined' && articleNumber)
            ? `${CCMS_IMAGE_WIDGET_CONFIG.articleUploadPath}${articleNumber}/`
            : CCMS_IMAGE_WIDGET_CONFIG.defaultUploadPath;

        const response = await fetch(`${CCMS_IMAGE_WIDGET_CONFIG.imageLibraryEndpoint}?path=${encodeURIComponent(uploadPath)}`);

        if (!response.ok) {
            throw new Error(`Failed to load images: ${response.statusText}`);
        }

        const images = await response.json();

        const loadingDiv = modal.querySelector('.ccms-library-loading');
        const gridDiv = modal.querySelector('.ccms-library-grid');
        const emptyDiv = modal.querySelector('.ccms-library-empty');

        loadingDiv.style.display = 'none';

        if (images && images.length > 0) {
            gridDiv.style.display = 'grid';

            images.forEach(imagePath => {
                const imageCard = document.createElement('div');
                imageCard.classList.add('ccms-library-item');
                imageCard.innerHTML = `
                    <img src="${imagePath}" alt="Library image" />
                    <div class="ccms-library-item-overlay">
                        <button type="button" class="btn btn-sm btn-primary">Select</button>
                    </div>
                `;

                imageCard.querySelector('button').addEventListener('click', () => {
                    onSelect(imagePath);
                    closeModal();
                });

                gridDiv.appendChild(imageCard);
            });

            // Search functionality
            const searchInput = modal.querySelector(`#ccms-library-search-${id}`);
            searchInput.addEventListener('input', ccms___debounce((e) => {
                const searchTerm = e.target.value.toLowerCase();
                const items = gridDiv.querySelectorAll('.ccms-library-item');

                items.forEach(item => {
                    const imageSrc = item.querySelector('img').src.toLowerCase();
                    item.style.display = imageSrc.includes(searchTerm) ? 'block' : 'none';
                });
            }, 300));
        } else {
            emptyDiv.style.display = 'block';
        }
    } catch (error) {
        console.error('Error loading image library:', error);
        modal.querySelector('.ccms-library-loading').innerHTML = `
            <div class="alert alert-danger">
                <strong>Error:</strong> Failed to load image library. ${error.message}
            </div>
        `;
    }
}

// ============================================================================
// EVENT DISPATCHER FOR CUSTOM EVENTS
// ============================================================================
window.CCMSImageWidgetEvents = (function () {
    const listeners = {};
    return {
        on: function (event, callback) {
            if (!listeners[event]) listeners[event] = [];
            listeners[event].push(callback);
        },
        off: function (event, callback) {
            if (!listeners[event]) return;
            listeners[event] = listeners[event].filter(cb => cb !== callback);
        },
        trigger: function (event, data) {
            if (!listeners[event]) return;
            listeners[event].forEach(cb => cb(data));
        }
    };
})();

// ============================================================================
// TRASH CAN AND ACTION BUTTONS
// ============================================================================

/**
 * Handles click on the trash can icon - removes the image and resets the widget.
 * @param {string} id - The widget's data-ccms-ceid value
 * @param {HTMLElement} element - The widget container element
 */
function ccms___onClickTrashCan(id, element) {
    console.log(`Removing image from widget: ${id}`);

    // Clean up all child elements
    while (element.hasChildNodes()) {
        element.removeChild(element.firstChild);
    }

    // Trigger custom event for deletion
    window.CCMSImageWidgetEvents.trigger('imageChanged', {
        type: 'deleted',
        id,
        element,
        imageSrc: undefined
    });

    // Reinitialize the upload widget
    ccms___initializePond(element);
}

/**
 * Mouse leave handler for the image widget - removes the action buttons overlay.
 * @param {MouseEvent} event - The mouseleave event
 */
function ccms___handleWidgetMouseLeave(event) {
    const element = event.currentTarget;
    const toolbar = element.querySelector('.ccms-img-toolbar');

    // Check if mouse is moving to the toolbar itself
    if (toolbar && (toolbar === event.relatedTarget || toolbar.contains(event.relatedTarget))) {
        return;
    }

    // Remove the toolbar
    if (toolbar) {
        toolbar.remove();
    }

    // Reset event listeners for next hover
    element.removeEventListener('mouseleave', ccms___handleWidgetMouseLeave);
    element.removeEventListener('mouseover', ccms___handleWidgetMouseOver);
    element.addEventListener('mouseover', ccms___handleWidgetMouseOver, { once: true });
}

/**
 * Mouse over handler for the image widget - shows the action buttons.
 * @param {MouseEvent} event - The mouseover event
 */
function ccms___handleWidgetMouseOver(event) {
    const element = event.currentTarget;
    const img = element.querySelector('img');

    // Only show toolbar if an image is present
    if (!img || !img.src) {
        return;
    }

    const id = element.getAttribute('data-ccms-ceid');

    // Don't create duplicate toolbars
    let toolbar = element.querySelector('.ccms-img-toolbar');
    if (toolbar) {
        return;
    }

    // Create the toolbar overlay
    toolbar = document.createElement('div');
    toolbar.classList.add('ccms-img-toolbar');

    // Calculate a safe z-index
    const computedZIndex = window.getComputedStyle(element).zIndex;
    const baseZIndex = (computedZIndex === 'auto' || isNaN(parseInt(computedZIndex)))
        ? 0
        : parseInt(computedZIndex);
    toolbar.style.zIndex = baseZIndex + CCMS_IMAGE_WIDGET_CONFIG.zIndexOffset;

    // Edit Alt Text button
    if (CCMS_IMAGE_WIDGET_CONFIG.enableAltTextEditor) {
        const editBtn = document.createElement('button');
        editBtn.classList.add('ccms-img-toolbar-btn', 'ccms-img-edit-btn');
        editBtn.innerHTML = CCMS_IMAGE_WIDGET_CONFIG.editIconHtml;
        editBtn.title = 'Edit alt text';
        editBtn.setAttribute('type', 'button');
        editBtn.onclick = (e) => {
            e.preventDefault();
            e.stopPropagation();
            const enableEditor = element.getAttribute('data-ccms-enable-alt-editor');
            if (enableEditor !== 'true') {
                ccms___showInfo('Alt editor is disabled for this image. Add data-ccms-enable-alt-editor="true" to enable.', element);
                return;
            }
            ccms___showAltTextEditor(img, element);
        };
        toolbar.appendChild(editBtn);
    }

    // Replace Image button
    if (CCMS_IMAGE_WIDGET_CONFIG.enableDragReplace) {
        const replaceBtn = document.createElement('button');
        replaceBtn.classList.add('ccms-img-toolbar-btn', 'ccms-img-replace-btn');
        replaceBtn.innerHTML = CCMS_IMAGE_WIDGET_CONFIG.replaceIconHtml;
        replaceBtn.title = 'Replace image';
        replaceBtn.setAttribute('type', 'button');
        replaceBtn.onclick = (e) => {
            e.preventDefault();
            e.stopPropagation();
            ccms___replaceImage(element);
        };
        toolbar.appendChild(replaceBtn);
    }

    // Image Library button
    if (CCMS_IMAGE_WIDGET_CONFIG.enableImageLibrary) {
        const libraryBtn = document.createElement('button');
        libraryBtn.classList.add('ccms-img-toolbar-btn', 'ccms-img-library-btn');
        libraryBtn.innerHTML = CCMS_IMAGE_WIDGET_CONFIG.libraryIconHtml;
        libraryBtn.title = 'Choose from library';
        libraryBtn.setAttribute('type', 'button');
        libraryBtn.onclick = (e) => {
            e.preventDefault();
            e.stopPropagation();
            ccms___showImageLibrary(element, (imagePath) => {
                img.src = imagePath;
                const widgetId = element.getAttribute('data-ccms-ceid');
                // Trigger custom event for library selection
                window.CCMSImageWidgetEvents.trigger('imageChanged', {
                    type: 'uploaded',
                    id: widgetId,
                    element,
                    imageSrc: imagePath
                });
            });
        };
        toolbar.appendChild(libraryBtn);
    }

    // Delete button
    const deleteBtn = document.createElement('button');
    deleteBtn.classList.add('ccms-img-toolbar-btn', 'ccms-img-delete-btn');
    deleteBtn.innerHTML = CCMS_IMAGE_WIDGET_CONFIG.trashIconHtml;
    deleteBtn.title = 'Remove image';
    deleteBtn.setAttribute('type', 'button');
    deleteBtn.onclick = (e) => {
        e.preventDefault();
        e.stopPropagation();
        ccms___onClickTrashCan(id, element);
    };
    toolbar.appendChild(deleteBtn);

    element.appendChild(toolbar);

    // Set up mouseleave to remove toolbar
    element.addEventListener('mouseleave', ccms___handleWidgetMouseLeave);
    element.removeEventListener('mouseover', ccms___handleWidgetMouseOver);
}

/**
 * Replaces the current image by triggering upload.
 * @param {HTMLElement} element - The widget container element
 */
function ccms___replaceImage(element) {
    const img = element.querySelector('img');
    
    // Safely get alt and title text with null checks
    const altText = img ? (img.getAttribute('alt') || '') : '';
    const titleText = img ? (img.getAttribute('title') || '') : '';

    // Store metadata for restoration after upload
    element.dataset.preservedAlt = altText;
    element.dataset.preservedTitle = titleText;

    // Clear and reinitialize
    ccms___destroyPondsInElement(element);
    while (element.hasChildNodes()) {
        element.removeChild(element.firstChild);
    }

    ccms___initializePond(element);
}

// ============================================================================
// FILEPOND UPLOAD WIDGET INITIALIZATION
// ============================================================================

/**
 * Initializes a FilePond upload widget within the given container element.
 * @param {HTMLElement} element - The widget container element
 */
function ccms___initializePond(element) {
    const id = element.getAttribute('data-ccms-ceid');

    if (!id) {
        console.error('Cannot initialize FilePond: missing data-ccms-ceid attribute');
        return;
    }

    // Create the file input element that FilePond will enhance
    const input = document.createElement('input');
    input.type = 'file';
    input.id = `inp-${id}`;
    input.classList.add('filepond');
    input.name = 'files';
    element.appendChild(input);

    // Initialize FilePond
    const pond = FilePond.create(input, {
        acceptedFileTypes: CCMS_IMAGE_WIDGET_CONFIG.acceptedFileTypes,
        labelIdle: '<span class="filepond--label-action">Drop image here or click to browse</span>',
        allowDrop: true,
        allowBrowse: true,
        allowMultiple: false,
        allowReplace: false,
        maxFileSize: CCMS_IMAGE_WIDGET_CONFIG.maxFileSize,
        labelMaxFileSizeExceeded: 'File is too large',
        labelMaxFileSize: 'Maximum file size is 25MB'
    });

    // Store references for access in event handlers
    pond.editorElement = element;
    pond.editorId = id;
    pond.inputElement = input;

    // Configure the server endpoint
    pond.setOptions({
        server: CCMS_IMAGE_WIDGET_CONFIG.uploadEndpoint
    });

    // Progress indicator
    if (CCMS_IMAGE_WIDGET_CONFIG.showProgressBar) {
        const progressContainer = document.createElement('div');
        progressContainer.classList.add('ccms-upload-progress');
        progressContainer.style.display = 'none';
        progressContainer.innerHTML = `
            <div class="progress">
                <div class="progress-bar progress-bar-striped progress-bar-animated" role="progressbar" style="width: 0%"></div>
            </div>
            <small class="text-muted mt-1">Uploading...</small>
        `;
        element.appendChild(progressContainer);
        pond.progressContainer = progressContainer;
    }

    // ========================================================================
    // EVENT: File Added (before upload starts)
    // ========================================================================
    pond.on('addfile', (error, file) => {
        if (error) {
            console.error('Error adding file:', error);
            ccms___showError(error.main || 'Failed to add file', element);
            return;
        }

        // Validate file
        const validation = ccms___validateFile(file.file);
        if (!validation.valid) {
            pond.removeFile(file.id);
            ccms___showError(validation.error, element);
            return;
        }

        console.log(`File added to pond: ${file.filename}`);

        // Show progress indicator
        if (pond.progressContainer) {
            pond.progressContainer.style.display = 'block';
        }

        // Determine upload path based on context
        const uploadPath = (typeof articleNumber !== 'undefined' && articleNumber)
            ? `${CCMS_IMAGE_WIDGET_CONFIG.articleUploadPath}${articleNumber}/`
            : CCMS_IMAGE_WIDGET_CONFIG.defaultUploadPath;

        // Set metadata that the server expects
        file.setMetadata('Path', uploadPath);
        file.setMetadata('RelativePath', '');
        file.setMetadata('fileName', file.filename.toLowerCase());

        // Extract image dimensions asynchronously
        ccms___getImageDimensions(file.file, function (dimensions) {
            file.setMetadata('imageWidth', dimensions.width);
            file.setMetadata('imageHeight', dimensions.height);
        });
    });

    // ========================================================================
    // EVENT: File Processing Started
    // ========================================================================
    pond.on('processfilestart', (file) => {
        console.log(`Upload started: ${file.filename}`);
    });

    // ========================================================================
    // EVENT: Upload Progress
    // ========================================================================
    pond.on('processfileprogress', (file) => {
        if (pond.progressContainer) {
            const percent = Math.round(file.progress * 100);
            const progressBar = pond.progressContainer.querySelector('.progress-bar');
            progressBar.style.width = `${percent}%`;
            progressBar.textContent = `${percent}%`;
        }
    });

    // ========================================================================
    // EVENT: File Processing Complete (upload successful)
    // ========================================================================
    pond.on('processfile', (error, file) => {
        const element = pond.editorElement;
        if (error) {
            console.error('Error processing file:', error);
            ccms___showError('Upload failed: ' + (error.main || 'Unknown error'), element);

            if (typeof parent !== 'undefined') {
                parent.saveInProgress = false;
            }

            // Hide progress
            if (pond.progressContainer) {
                pond.progressContainer.style.display = 'none';
            }
            return;
        }

        console.log(`Upload complete: ${file.filename}`);
        const id = pond.editorId;

        // Tear down FilePond first, then defer DOM replacement to the next frame
        // so FilePond can finish restore/cleanup safely.
        ccms___destroyPondsInElement(element, {
            cleanupOrphanRoots: false,
            inputs: [pond.inputElement]
        });

        requestAnimationFrame(() => {
            // Remove all children from container
            while (element.hasChildNodes()) {
                element.removeChild(element.firstChild);
            }

            // Create and insert the uploaded image
            const image = document.createElement('img');
            image.id = `img-${id}`;
            // Server returns the path with quotes sometimes, strip them
            image.src = file.serverId.replace(/['"]+/g, '');
            image.classList.add('ccms-img-widget-img');

            // Restore or set default alt text
            const preservedAlt = element.dataset.preservedAlt;
            const preservedTitle = element.dataset.preservedTitle;

            image.alt = preservedAlt || file.filename.replace(/\.[^/.]+$/, ''); // Use filename without extension as default

            if (preservedTitle) {
                image.setAttribute('title', preservedTitle);
            }

            // Clean up preserved data
            delete element.dataset.preservedAlt;
            delete element.dataset.preservedTitle;

            element.appendChild(image);

            // Trigger custom event for upload
            window.CCMSImageWidgetEvents.trigger('imageChanged', {
                type: 'uploaded',
                id,
                element,
                imageSrc: image.src
            });

            // Mark save as complete
            if (typeof parent !== 'undefined') {
                parent.saveInProgress = false;
            }

            // Re-setup the widget for future interactions
            ccms___setupImageWidget(element);

            // Show alt text editor for new uploads
            if (CCMS_IMAGE_WIDGET_CONFIG.enableAltTextEditor && !preservedAlt) {
                setTimeout(() => {
                    ccms___showAltTextEditor(image, element);
                }, 500);
            }

            // Save image widget inner html.
            const editorId = element.getAttribute('data-ccms-ceid');
            const html = element.innerHTML;
            if (typeof parent !== 'undefined' && typeof parent.saveEditorRegion !== 'undefined') {
                parent.saveEditorRegion(html, editorId);
            }
        });
    });

    // ========================================================================
    // EVENT: File Removed (user canceled upload)
    // ========================================================================
    pond.on('removefile', (error, file) => {
        console.log(`File removed from pond: ${file.filename}`);
        if (typeof parent !== 'undefined') {
            parent.saveInProgress = false;
        }

        // Hide progress
        if (pond.progressContainer) {
            pond.progressContainer.style.display = 'none';
        }
    });
}

/**
 * Destroys all FilePond instances found within a container before DOM mutations.
 * @param {HTMLElement} container - Container that may include one or more FilePond inputs
 * @param {{ cleanupOrphanRoots?: boolean, inputs?: Iterable<Element> }} options - Optional teardown behavior overrides
 */
function ccms___destroyPondsInElement(container, options = {}) {
    if (!container || typeof FilePond === 'undefined') {
        return;
    }

    const settings = {
        cleanupOrphanRoots: true,
        inputs: null,
        ...options
    };

    const teardownTargets = settings.inputs
        ? Array.from(settings.inputs)
        : Array.from(container.querySelectorAll('input[type="file"], .filepond'));

    teardownTargets.forEach(input => {
        if (!input) {
            return;
        }

        const pond = FilePond.find(input);
        if (pond) {
            try {
                pond.destroy();
            } catch (error) {
                console.warn('Failed to destroy FilePond instance safely:', error);
            }
        }
    });

    if (settings.cleanupOrphanRoots) {
        const orphanRoots = container.querySelectorAll('.filepond--root');
        orphanRoots.forEach(root => {
            if (root.parentNode) {
                root.parentNode.removeChild(root);
            }
        });
    }
}

if (typeof window !== 'undefined') {
    window.ccms___destroyPondsInElement = ccms___destroyPondsInElement;
}

// ============================================================================
// WIDGET SETUP AND INITIALIZATION
// ============================================================================

/**
 * Sets up an image widget container, initializing either the image display
 * (with hover delete) or the upload interface.
 * @param {HTMLElement} element - The widget container element
 */
function ccms___setupImageWidget(element) {
    // Ensure container is positioned for absolute overlays
    element.style.position = 'relative';
    element.style.display = 'inline-block';

    const isNew = element.getAttribute('data-ccms-new');

    // Clean up any placeholder images
    const placeHolder = element.querySelector('.ccms___placeHolder');
    if (placeHolder) {
        placeHolder.remove();
    }

    // Clean up any existing FilePond instances before mutating the widget DOM
    ccms___destroyPondsInElement(element);

    let id = element.getAttribute('data-ccms-ceid');

    // Validate or generate element ID
    if (!id && !isNew) {
        console.warn('Image widget missing both data-ccms-ceid and data-ccms-new attributes');
        return;
    }

    // Generate ID for new widgets
    if (isNew) {
        const guid = ccmsGenerateGuid();
        element.setAttribute('data-ccms-ceid', guid);
        element.removeAttribute('data-ccms-new');
        id = guid;
        console.log(`Generated new widget ID: ${id}`);
    }

    // Check if widget already contains an image
    const img = element.querySelector('img');
    if (img && img.src) {
        // Image exists - set up hover functionality
        element.addEventListener('mouseleave', ccms___handleWidgetMouseLeave);
        element.addEventListener('mouseover', ccms___handleWidgetMouseOver, { once: true });
        console.log(`Image widget initialized with existing image: ${id}`);
    } else {
        // No image - set up upload interface
        // Clear any existing content first
        ccms___destroyPondsInElement(element);
        while (element.hasChildNodes()) {
            element.removeChild(element.firstChild);
        }

        element.removeEventListener('mouseleave', ccms___handleWidgetMouseLeave);
        element.removeEventListener('mouseover', ccms___handleWidgetMouseOver);
        ccms___initializePond(element);
        console.log(`Image widget initialized for upload: ${id}`);
    }
}

// ============================================================================
// DOCUMENT READY - AUTO-INITIALIZATION
// ============================================================================

document.addEventListener('DOMContentLoaded', function () {
    console.log('Initializing Cosmos CMS Image Widgets (Enhanced)...');

    // Register required FilePond plugins
    if (typeof FilePond === 'undefined') {
        console.error('FilePond library is not loaded. Image widgets will not function.');
        return;
    }

    if (typeof FilePondPluginFileMetadata === 'undefined') {
        console.error('FilePondPluginFileMetadata is not loaded. Image widgets will not function.');
        return;
    }

    // FilePond.registerPlugin(FilePondPluginFileMetadata);

    // Find and initialize all image widgets on the page
    const imageContainers = document.querySelectorAll('div[data-editor-config="image-widget"]');
    console.log(`Found ${imageContainers.length} image widget(s) to initialize`);

    imageContainers.forEach(ccms___setupImageWidget);
});