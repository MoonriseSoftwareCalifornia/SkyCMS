// <copyright file="ccms-author-widget.js" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

/**
 * CCMS Author Widget
 * 
 * A JavaScript widget for displaying author information with configurable display options.
 * Integrates with SkyCMS's CCMS widget ecosystem similar to the image widget.
 * 
 * Usage:
 * <div class="ccms-author-widget-container"
 *      data-editor-config="author-widget"
 *      data-ccms-ceid="skycms-author-info"
 *      data-author-json='{"AuthorName":"John Doe","EmailAddress":"john@example.com",...}'
 *      data-display-mode="card"
 *      data-show-email="true"
 *      data-show-social="true"
 *      data-show-description="true">
 * </div>
 */

(function (window) {
    'use strict';

    // Event bus for author widget events
    window.CCMSAuthorWidgetEvents = window.CCMSAuthorWidgetEvents || {
        listeners: {},
        on: function (event, callback) {
            if (!this.listeners[event]) {
                this.listeners[event] = [];
            }
            this.listeners[event].push(callback);
        },
        emit: function (event, data) {
            if (this.listeners[event]) {
                this.listeners[event].forEach(callback => callback(data));
            }
        }
    };

    /**
     * Parse author JSON string handling both single-quoted and double-quoted formats
     * @param {string} jsonString - The JSON string to parse
     * @returns {object|null} - Parsed author object or null
     */
    function parseAuthorJson(jsonString) {
        if (!jsonString) return null;

        try {
            // First try parsing as-is
            return JSON.parse(jsonString);
        } catch (e) {
            try {
                // If that fails, try replacing single quotes with double quotes
                const normalized = jsonString.replace(/'/g, '"');
                return JSON.parse(normalized);
            } catch (e2) {
                console.error('Failed to parse author JSON:', e2);
                return null;
            }
        }
    }

    /**
     * Display modes for the author widget
     */
    const DisplayMode = {
        CARD: 'card',           // Full card with all details
        INLINE: 'inline',       // Compact inline display
        COMPACT: 'compact',     // Minimal display (name only or name + link)
        DETAILED: 'detailed'    // Extended card with all available information
    };

    /**
     * Generate HTML for author display based on configuration
     * @param {object} authorInfo - The AuthorInfo object
     * @param {object} config - Display configuration
     * @returns {string} - HTML string
     */
    function generateAuthorHtml(authorInfo, config) {
        if (!authorInfo) return '';

        const mode = config.displayMode || DisplayMode.INLINE;
        const showEmail = config.showEmail !== false;
        const showSocial = config.showSocial !== false;
        const showDescription = config.showDescription !== false;
        const showWebsite = config.showWebsite !== false;

        let html = '';

        switch (mode) {
            case DisplayMode.CARD:
                html = generateCardHtml(authorInfo, { showEmail, showSocial, showDescription, showWebsite });
                break;
            case DisplayMode.INLINE:
                html = generateInlineHtml(authorInfo, { showEmail, showWebsite });
                break;
            case DisplayMode.COMPACT:
                html = generateCompactHtml(authorInfo);
                break;
            case DisplayMode.DETAILED:
                html = generateDetailedHtml(authorInfo);
                break;
            default:
                html = generateInlineHtml(authorInfo, { showEmail, showWebsite });
        }

        return html;
    }

    /**
     * Generate compact HTML (name only or with link)
     */
    function generateCompactHtml(author) {
        const name = escapeHtml(author.AuthorName || 'Unknown Author');
        if (author.Website) {
            return `<span class="ccms-author-compact"><a href="${escapeHtml(author.Website)}" target="_blank" rel="noopener noreferrer">${name}</a></span>`;
        }
        return `<span class="ccms-author-compact">${name}</span>`;
    }

    /**
     * Generate inline HTML (name with optional email/website)
     */
    function generateInlineHtml(author, options) {
        const name = escapeHtml(author.AuthorName || 'Unknown Author');
        const parts = [name];

        if (options.showEmail && author.EmailAddress) {
            parts.push(`<a href="mailto:${escapeHtml(author.EmailAddress)}">${escapeHtml(author.EmailAddress)}</a>`);
        }

        if (options.showWebsite && author.Website) {
            parts.push(`<a href="${escapeHtml(author.Website)}" target="_blank" rel="noopener noreferrer">Website</a>`);
        }

        return `<span class="ccms-author-inline">${parts.join(' · ')}</span>`;
    }

    /**
     * Generate card HTML (standard card with selectable fields)
     */
    function generateCardHtml(author, options) {
        let html = '<div class="ccms-author-card">';
        html += `<div class="ccms-author-name">${escapeHtml(author.AuthorName || 'Unknown Author')}</div>`;

        if (options.showDescription && author.AuthorDescription) {
            html += `<div class="ccms-author-description">${escapeHtml(author.AuthorDescription)}</div>`;
        }

        const links = [];

        if (options.showEmail && author.EmailAddress) {
            links.push(`<a href="mailto:${escapeHtml(author.EmailAddress)}" class="ccms-author-link" title="Email">
                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                    <path d="M.05 3.555A2 2 0 0 1 2 2h12a2 2 0 0 1 1.95 1.555L8 8.414.05 3.555ZM0 4.697v7.104l5.803-3.558L0 4.697ZM6.761 8.83l-6.57 4.027A2 2 0 0 0 2 14h12a2 2 0 0 0 1.808-1.144l-6.57-4.027L8 9.586l-1.239-.757Zm3.436-.586L16 11.801V4.697l-5.803 3.546Z"/>
                </svg>
                Email
            </a>`);
        }

        if (options.showWebsite && author.Website) {
            links.push(`<a href="${escapeHtml(author.Website)}" class="ccms-author-link" target="_blank" rel="noopener noreferrer" title="Website">
                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                    <path d="M0 8a8 8 0 1 1 16 0A8 8 0 0 1 0 8zm7.5-6.923c-.67.204-1.335.82-1.887 1.855A7.97 7.97 0 0 0 5.145 4H7.5V1.077zM4.09 4a9.267 9.267 0 0 1 .64-1.539 6.7 6.7 0 0 1 .597-.933A7.025 7.025 0 0 0 2.255 4H4.09zm-.582 3.5c.03-.877.138-1.718.312-2.5H1.674a6.958 6.958 0 0 0-.656 2.5h2.49zM4.847 5a12.5 12.5 0 0 0-.338 2.5H7.5V5H4.847zM8.5 5v2.5h2.99a12.495 12.495 0 0 0-.337-2.5H8.5zM4.51 8.5a12.5 12.5 0 0 0 .337 2.5H7.5V8.5H4.51zm3.99 0V11h2.653c.187-.765.306-1.608.338-2.5H8.5zM5.145 12c.138.386.295.744.468 1.068.552 1.035 1.218 1.65 1.887 1.855V12H5.145zm.182 2.472a6.696 6.696 0 0 1-.597-.933A9.268 9.268 0 0 1 4.09 12H2.255a7.024 7.024 0 0 0 3.072 2.472zM3.82 11a13.652 13.652 0 0 1-.312-2.5h-2.49c.062.89.291 1.733.656 2.5H3.82zm6.853 3.472A7.024 7.024 0 0 0 13.745 12H11.91a9.27 9.27 0 0 1-.64 1.539 6.688 6.688 0 0 1-.597.933zM8.5 12v2.923c.67-.204 1.335-.82 1.887-1.855.173-.324.33-.682.468-1.068H8.5zm3.68-1h2.146c.365-.767.594-1.61.656-2.5h-2.49a13.65 13.65 0 0 1-.312 2.5zm2.802-3.5a6.959 6.959 0 0 0-.656-2.5H12.18c.174.782.282 1.623.312 2.5h2.49zM11.27 2.461c.247.464.462.98.64 1.539h1.835a7.024 7.024 0 0 0-3.072-2.472c.218.284.418.598.597.933zM10.855 4a7.966 7.966 0 0 0-.468-1.068C9.835 1.897 9.17 1.282 8.5 1.077V4h2.355z"/>
                </svg>
                Website
            </a>`);
        }

        if (options.showSocial && author.TwitterHandle) {
            const twitterUrl = author.TwitterHandle.startsWith('@') 
                ? `https://twitter.com/${author.TwitterHandle.substring(1)}`
                : `https://twitter.com/${author.TwitterHandle}`;
            links.push(`<a href="${escapeHtml(twitterUrl)}" class="ccms-author-link" target="_blank" rel="noopener noreferrer" title="Twitter">
                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                    <path d="M5.026 15c6.038 0 9.341-5.003 9.341-9.334 0-.14 0-.282-.006-.422A6.685 6.685 0 0 0 16 3.542a6.658 6.658 0 0 1-1.889.518 3.301 3.301 0 0 0 1.447-1.817 6.533 6.533 0 0 1-2.087.793A3.286 3.286 0 0 0 7.875 6.03a9.325 9.325 0 0 1-6.767-3.429 3.289 3.289 0 0 0 1.018 4.382A3.323 3.323 0 0 1 .64 6.575v.045a3.288 3.288 0 0 0 2.632 3.218 3.203 3.203 0 0 1-.865.115 3.23 3.23 0 0 1-.614-.057 3.283 3.283 0 0 0 3.067 2.277A6.588 6.588 0 0 1 .78 13.58a6.32 6.32 0 0 1-.78-.045A9.344 9.344 0 0 0 5.026 15z"/>
                </svg>
                Twitter
            </a>`);
        }

        if (options.showSocial && author.InstagramUrl) {
            links.push(`<a href="${escapeHtml(author.InstagramUrl)}" class="ccms-author-link" target="_blank" rel="noopener noreferrer" title="Instagram">
                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                    <path d="M8 0C5.829 0 5.556.01 4.703.048 3.85.088 3.269.222 2.76.42a3.917 3.917 0 0 0-1.417.923A3.927 3.927 0 0 0 .42 2.76C.222 3.268.087 3.85.048 4.7.01 5.555 0 5.827 0 8.001c0 2.172.01 2.444.048 3.297.04.852.174 1.433.372 1.942.205.526.478.972.923 1.417.444.445.89.719 1.416.923.51.198 1.09.333 1.942.372C5.555 15.99 5.827 16 8 16s2.444-.01 3.298-.048c.851-.04 1.434-.174 1.943-.372a3.916 3.916 0 0 0 1.416-.923c.445-.445.718-.891.923-1.417.197-.509.332-1.09.372-1.942C15.99 10.445 16 10.173 16 8s-.01-2.445-.048-3.299c-.04-.851-.175-1.433-.372-1.941a3.926 3.926 0 0 0-.923-1.417A3.911 3.911 0 0 0 13.24.42c-.51-.198-1.092-.333-1.943-.372C10.443.01 10.172 0 7.998 0h.003zm-.717 1.442h.718c2.136 0 2.389.007 3.232.046.78.035 1.204.166 1.486.275.373.145.64.319.92.599.28.28.453.546.598.92.11.281.24.705.275 1.485.039.843.047 1.096.047 3.231s-.008 2.389-.047 3.232c-.035.78-.166 1.203-.275 1.485a2.47 2.47 0 0 1-.599.919c-.28.28-.546.453-.92.598-.28.11-.704.24-1.485.276-.843.038-1.096.047-3.232.047s-2.39-.009-3.233-.047c-.78-.036-1.203-.166-1.485-.276a2.478 2.478 0 0 1-.92-.598 2.48 2.48 0 0 1-.6-.92c-.109-.281-.24-.705-.275-1.485-.038-.843-.046-1.096-.046-3.233 0-2.136.008-2.388.046-3.231.036-.78.166-1.204.276-1.486.145-.373.319-.64.599-.92.28-.28.546-.453.92-.598.282-.11.705-.24 1.485-.276.738-.034 1.024-.044 2.515-.045v.002zm4.988 1.328a.96.96 0 1 0 0 1.92.96.96 0 0 0 0-1.92zm-4.27 1.122a4.109 4.109 0 1 0 0 8.217 4.109 4.109 0 0 0 0-8.217zm0 1.441a2.667 2.667 0 1 1 0 5.334 2.667 2.667 0 0 1 0-5.334z"/>
                </svg>
                Instagram
            </a>`);
        }

        if (links.length > 0) {
            html += `<div class="ccms-author-links">${links.join('')}</div>`;
        }

        html += '</div>';
        return html;
    }

    /**
     * Generate detailed HTML (all information including schema.org microdata)
     */
    function generateDetailedHtml(author) {
        let html = '<div class="ccms-author-detailed" itemscope itemtype="https://schema.org/Person">';
        html += `<h3 class="ccms-author-name" itemprop="name">${escapeHtml(author.AuthorName || 'Unknown Author')}</h3>`;

        if (author.AuthorDescription) {
            html += `<p class="ccms-author-description" itemprop="description">${escapeHtml(author.AuthorDescription)}</p>`;
        }

        html += '<div class="ccms-author-contact">';
        
        if (author.EmailAddress) {
            html += `<div class="ccms-author-contact-item">
                <strong>Email:</strong> <a href="mailto:${escapeHtml(author.EmailAddress)}" itemprop="email">${escapeHtml(author.EmailAddress)}</a>
            </div>`;
        }

        if (author.Website) {
            html += `<div class="ccms-author-contact-item">
                <strong>Website:</strong> <a href="${escapeHtml(author.Website)}" target="_blank" rel="noopener noreferrer" itemprop="url">${escapeHtml(author.Website)}</a>
            </div>`;
        }

        if (author.TwitterHandle) {
            const twitterUrl = author.TwitterHandle.startsWith('@') 
                ? `https://twitter.com/${author.TwitterHandle.substring(1)}`
                : `https://twitter.com/${author.TwitterHandle}`;
            html += `<div class="ccms-author-contact-item">
                <strong>Twitter:</strong> <a href="${escapeHtml(twitterUrl)}" target="_blank" rel="noopener noreferrer" itemprop="sameAs">${escapeHtml(author.TwitterHandle)}</a>
            </div>`;
        }

        if (author.InstagramUrl) {
            html += `<div class="ccms-author-contact-item">
                <strong>Instagram:</strong> <a href="${escapeHtml(author.InstagramUrl)}" target="_blank" rel="noopener noreferrer" itemprop="sameAs">View Profile</a>
            </div>`;
        }

        html += '</div>'; // close contact
        html += '</div>'; // close detailed
        return html;
    }

    /**
     * Escape HTML to prevent XSS
     */
    function escapeHtml(text) {
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return String(text).replace(/[&<>"']/g, m => map[m]);
    }

    /**
     * Initialize all author widgets on the page
     */
    function initializeAuthorWidgets() {
        const containers = document.querySelectorAll('.ccms-author-widget-container[data-editor-config="author-widget"]');

        containers.forEach(container => {
            // Get configuration from data attributes
            const authorJson = container.getAttribute('data-author-json');
            const displayMode = container.getAttribute('data-display-mode') || DisplayMode.INLINE;
            const showEmail = container.getAttribute('data-show-email') !== 'false';
            const showSocial = container.getAttribute('data-show-social') !== 'false';
            const showDescription = container.getAttribute('data-show-description') !== 'false';
            const showWebsite = container.getAttribute('data-show-website') !== 'false';

            // Parse author information
            const authorInfo = parseAuthorJson(authorJson);

            if (authorInfo) {
                const config = {
                    displayMode,
                    showEmail,
                    showSocial,
                    showDescription,
                    showWebsite
                };

                // Generate and insert HTML
                const html = generateAuthorHtml(authorInfo, config);
                container.innerHTML = html;

                // Emit event
                window.CCMSAuthorWidgetEvents.emit('authorRendered', {
                    container,
                    authorInfo,
                    config
                });
            } else {
                console.warn('No valid author information found for widget', container);
            }
        });
    }

    /**
     * Update author widget with new information
     * @param {string} containerId - The ID or selector of the container
     * @param {object} authorInfo - New author information
     * @param {object} config - Optional new configuration
     */
    function updateAuthorWidget(containerId, authorInfo, config) {
        const container = typeof containerId === 'string' 
            ? document.querySelector(containerId)
            : containerId;

        if (!container) {
            console.warn('Author widget container not found:', containerId);
            return;
        }

        // Merge with existing config if not provided
        if (!config) {
            config = {
                displayMode: container.getAttribute('data-display-mode') || DisplayMode.INLINE,
                showEmail: container.getAttribute('data-show-email') !== 'false',
                showSocial: container.getAttribute('data-show-social') !== 'false',
                showDescription: container.getAttribute('data-show-description') !== 'false',
                showWebsite: container.getAttribute('data-show-website') !== 'false'
            };
        }

        // Update data attribute
        container.setAttribute('data-author-json', JSON.stringify(authorInfo));

        // Regenerate HTML
        const html = generateAuthorHtml(authorInfo, config);
        container.innerHTML = html;

        // Emit event
        window.CCMSAuthorWidgetEvents.emit('authorUpdated', {
            container,
            authorInfo,
            config
        });
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeAuthorWidgets);
    } else {
        initializeAuthorWidgets();
    }

    // Export public API
    window.CCMSAuthorWidget = {
        initialize: initializeAuthorWidgets,
        update: updateAuthorWidget,
        DisplayMode: DisplayMode,
        parseAuthorJson: parseAuthorJson
    };

})(window);
