// <copyright file="setup-sensitive-fields.js" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

/**
 * Setup Wizard Sensitive Field Manager
 * Handles reveal and copy functionality for sensitive fields (passwords, API keys, connection strings)
 */
class SetupSensitiveFieldManager {
    constructor() {
        this.revealedFields = new Set();
        this.init();
    }

    init() {
        // Delegate reveal button clicks
        document.addEventListener('click', (e) => {
            if (e.target.id && e.target.id.startsWith('reveal-')) {
                this.handleRevealClick(e);
            }
            if (e.target.id && e.target.id.startsWith('copy-')) {
                this.handleCopyClick(e);
            }
        });
    }

    /**
     * Handle reveal button click - toggle between masked and actual value
     */
    handleRevealClick(event) {
        event.preventDefault();
        const button = event.target.closest('button');
        const propertyName = this.extractPropertyName(button.id);
        const inputId = `field-${propertyName}`;
        const displayId = `field-display-${propertyName}`;

        const inputField = document.getElementById(inputId);
        const displaySpan = document.getElementById(displayId);

        if (!inputField || !displaySpan) {
            console.warn(`Could not find elements for property: ${propertyName}`);
            return;
        }

        const actualValue = inputField.value;
        const isRevealed = this.revealedFields.has(propertyName);

        if (isRevealed) {
            // Hide (mask) the value
            displaySpan.textContent = this.maskValue(propertyName, actualValue);
            displaySpan.classList.remove('display-revealed');
            displaySpan.classList.add('display-masked');
            button.innerHTML = '<i class="fas fa-eye"></i>';
            button.title = 'Reveal value';
            this.revealedFields.delete(propertyName);
        } else {
            // Show the actual value
            displaySpan.textContent = actualValue;
            displaySpan.classList.remove('display-masked');
            displaySpan.classList.add('display-revealed');
            button.innerHTML = '<i class="fas fa-eye-slash"></i>';
            button.title = 'Hide value';
            this.revealedFields.add(propertyName);
        }
    }

    /**
     * Handle copy button click - copy actual value to clipboard
     */
    async handleCopyClick(event) {
        event.preventDefault();
        const button = event.target.closest('button');
        const propertyName = this.extractPropertyName(button.id);
        const inputId = `field-${propertyName}`;
        const inputField = document.getElementById(inputId);

        if (!inputField) {
            console.warn(`Could not find input field for property: ${propertyName}`);
            return;
        }

        try {
            await navigator.clipboard.writeText(inputField.value);
            
            // Visual feedback: change icon temporarily
            const originalHtml = button.innerHTML;
            button.innerHTML = '<i class="fas fa-check text-success"></i>';
            button.title = 'Copied!';
            
            setTimeout(() => {
                button.innerHTML = originalHtml;
                button.title = 'Copy to clipboard';
            }, 2000);
        } catch (err) {
            console.error('Failed to copy to clipboard:', err);
            alert('Failed to copy value to clipboard. Please try again.');
        }
    }

    /**
     * Extract property name from button ID
     * e.g., "reveal-adminpassword" -> "adminpassword"
     */
    extractPropertyName(buttonId) {
        return buttonId.replace(/^(reveal-|copy-)/, '');
    }

    /**
     * Mask a sensitive value for display
     */
    maskValue(propertyName, value) {
        if (!value) {
            return '(empty)';
        }

        // Passwords: asterisks only
        if (propertyName === 'adminpassword' || propertyName === 'smtppassword') {
            return '?'.repeat(Math.min(value.length, 20));
        }

        // Connection strings and API keys
        if (value.length <= 20) {
            return '?'.repeat(value.length);
        }

        const first10 = value.substring(0, 10);
        const last10 = value.substring(value.length - 10);
        return `${first10}...${('?').repeat(10)}...${last10}`;
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.setupSensitiveFieldManager = new SetupSensitiveFieldManager();
});
