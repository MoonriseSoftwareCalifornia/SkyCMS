/**
 * Monaco Editor Core - Modern Implementation
 * Handles editor initialization, field switching, and auto-save
 */

class MonacoEditorCore {
    constructor() {
        this.editor = null;
        this.currentField = null;
        this.autoSaveTimer = null;
        this.editFields = [];
        this.config = {};
        this.saveInProgress = false;
    }

    async switchField(fieldName) {
        // Save current field before switching
        if (this.currentField && this.editor) {
            this.saveCurrentFieldContent();
        }

        const fieldInfo = this.fields.find(f => f.FieldName === fieldName);
        if (!fieldInfo) {
            console.error('Field not found:', fieldName);
            return;
        }

        this.currentField = fieldInfo;

        // Get content from hidden field
        const content = $(`#${fieldInfo.FieldId}`).val() || '';

        // Update editor
        if (this.editor) {
            const model = monaco.editor.createModel(
                content,
                fieldInfo.EditorMode
            );
            this.editor.setModel(model);

            // Track content changes for dirty state
            model.onDidChangeContent(() => {
                if (this.activeDocumentTracker) {
                    this.activeDocumentTracker.markDirty();
                }
            });
        }

        // Update active document tracker
        if (this.activeDocumentTracker) {
            this.activeDocumentTracker.setActiveDocument({
                fieldName: fieldInfo.FieldName,
                language: fieldInfo.EditorMode,
                title: $('#Title').val() || 'Untitled',
                articleNumber: $('#ArticleNumber').val(),
                isDirty: false
            });
        }

        // Update UI
        $('#EditingField').val(fieldName);
        this.updateTabHighlight(fieldName);
    }

    saveCurrentFieldContent() {
        if (!this.editor || !this.currentField) return;

        const content = this.editor.getValue();
        $(`#${this.currentField.FieldId}`).val(content);

        // Mark as clean after save
        if (this.activeDocumentTracker) {
            this.activeDocumentTracker.markClean();
        }
    }

    async initialize(config) {
        try {
            // Show loading indicator
            if (typeof $.blockUI === 'function') {
                $.blockUI({
                    message: '<div class="bg-dark text-light" role="status">Loading editor...</div>'
                });
            }
            
            this.config = {
                autoSaveDelay: 1200,
                readOnly: false,
                basePath: '/lib/monaco-editor/min',
                ...config
            };

            this.editFields = this.config.editFields || [];

            // Load Monaco using modern loader
            if (!window.monacoLoader) {
                throw new Error('Monaco loader not found. Ensure monaco-loader-modern.js is loaded first.');
            }

            await window.monacoLoader.load({
                basePath: this.config.basePath,
                timeout: 10000
            });

            // Find initial field
            const fieldInfo = this.editFields.find(f =>
                f.FieldName === this.config.initialField ||
                f.FieldId === this.config.initialField
            );

            if (!fieldInfo) {
                throw new Error(`Initial field "${this.config.initialField}" not found`);
            }

            // Create editor instance
            await this.createEditor(fieldInfo);

            // Setup UI interactions
            this.setupTabs();

            // Setup auto-save if enabled
            if (!this.config.readOnly && this.config.autoSaveDelay > 0) {
                this.setupAutoSave();
            }

            // Setup keyboard shortcuts
            this.setupKeyboardShortcuts();

            // Hide loading indicator
            if (typeof $.unblockUI === 'function') {
                $.unblockUI();
            }

            // Notify completion
            if (typeof afterEditorCreate === 'function') {
                afterEditorCreate(fieldInfo);
            }

            return this;

        } catch (error) {
            console.error('Editor initialization failed:', error);

            if (typeof $.unblockUI === 'function') {
                $.unblockUI();
            }

            alert(`Failed to initialize editor: ${error.message}\n\nPlease refresh the page and try again.`);
            throw error;
        }
    }

    async createEditor(fieldInfo, preserveContent = false) {
        const container = document.getElementById('editspace');

        if (!container) {
            throw new Error('Editor container #editspace not found');
        }

        // Save current content before switching
        if (this.currentField && this.editor && !preserveContent) {
            this.saveCurrentFieldContent();
        }

        // Dispose existing editor
        if (this.editor) {
            this.editor.dispose();
            this.editor = null;
        }

        // Get field content from hidden input
        const content = $(`#${fieldInfo.FieldId}`).val() || '';

        // Determine language mode
        const language = this.getLanguageMode(fieldInfo.EditorMode || 0);

        try {
            // Create new editor instance
            this.editor = await window.monacoLoader.createEditor(container, {
                value: content,
                language: language,
                readOnly: this.config.readOnly
            });

            this.currentField = fieldInfo;

            // Store references globally for backward compatibility
            window.editor = this.editor;
            window.fieldId = fieldInfo.FieldId;

            // Update EditingField hidden input
            $('#EditingField').val(fieldInfo.FieldId);

            // Focus editor
            this.editor.focus();

            // Re-attach auto-save listener for new model
            if (!this.config.readOnly && this.config.autoSaveDelay > 0) {
                this.setupAutoSave();
            }

            console.log(`Editor created for field: ${fieldInfo.FieldName} (${language})`);

            return this.editor;

        } catch (error) {
            console.error('Failed to create editor:', error);
            throw error;
        }
    }

    setupTabs() {
        const tabs = document.querySelectorAll('[data-ccms-fieldname]');

        tabs.forEach(tab => {
            tab.addEventListener('click', async (e) => {
                e.preventDefault();

                try {
                    // Remove active class from all tabs
                    tabs.forEach(t => t.classList.remove('active'));
                    tab.classList.add('active');

                    // Get field info
                    const fieldName = tab.getAttribute('data-ccms-fieldname');
                    const fieldInfo = this.editFields.find(f => f.FieldName === fieldName);

                    if (fieldInfo) {
                        await this.createEditor(fieldInfo);
                    } else {
                        console.error(`Field not found: ${fieldName}`);
                    }
                } catch (error) {
                    console.error('Failed to switch editor field:', error);
                    alert('Failed to switch editor field. Please try again.');
                }
            });
        });
    }

    setupAutoSave() {
        if (!this.editor) return;

        this.editor.onDidChangeModelContent(() => {
            if (this.saveInProgress) return;

            // Clear existing timer
            if (this.autoSaveTimer) {
                clearTimeout(this.autoSaveTimer);
            }

            // Set new timer
            this.autoSaveTimer = setTimeout(() => {
                this.saveCurrentFieldContent();
                this.triggerAutoSave();
            }, this.config.autoSaveDelay);
        });
    }

    setupKeyboardShortcuts() {
        if (!this.editor) return;

        // Ctrl+S / Cmd+S to save
        this.editor.addCommand(
            window.monaco.KeyMod.CtrlCmd | window.monaco.KeyCode.KeyS,
            () => {
                this.saveCurrentFieldContent();
                if (typeof saveChanges === 'function') {
                    saveChanges();
                }
            }
        );
    }

    saveCurrentFieldContent() {
        if (!this.currentField || !this.editor) return;

        const value = this.editor.getValue();
        $(`#${this.currentField.FieldId}`).val(value);

        console.log(`Saved content for field: ${this.currentField.FieldName}`);
    }

    triggerAutoSave() {
        // Actually call the save function instead of just dispatching an event
        if (typeof saveChanges === 'function') {
            saveChanges();
        }

        // Dispatch custom event for external save handlers
        window.dispatchEvent(new CustomEvent('editor-autosave', {
            detail: {
                field: this.currentField,
                value: this.editor.getValue()
            }
        }));

        // Show auto-save indicator if available
        if (typeof showAutoSaveIndicator === 'function') {
            showAutoSaveIndicator();
        }
    }

    getLanguageMode(editorMode) {
        const modes = {
            0: 'javascript',
            1: 'html',
            2: 'css',
            3: 'json',
            4: 'markdown',
            5: 'typescript',
            6: 'xml'
        };
        return modes[editorMode] || 'html';
    }

    getValue() {
        return this.editor ? this.editor.getValue() : '';
    }

    setValue(value) {
        if (this.editor) {
            this.editor.setValue(value || '');
        }
    }

    focus() {
        if (this.editor) {
            this.editor.focus();
        }
    }

    dispose() {
        if (this.autoSaveTimer) {
            clearTimeout(this.autoSaveTimer);
        }

        if (this.editor) {
            this.saveCurrentFieldContent();
            this.editor.dispose();
            this.editor = null;
        }

        this.currentField = null;
    }
}

// Global initialization function for backward compatibility
async function initializeMonacoEditor(config) {
    try {
        const core = new MonacoEditorCore();
        await core.initialize(config);

        // Store globally for access in views
        window.monacoEditorCore = core;

        return core;
    } catch (error) {
        console.error('Monaco editor initialization failed:', error);
        throw error;
    }
}

// Expose to global scope
window.MonacoEditorCore = MonacoEditorCore;
window.initializeMonacoEditor = initializeMonacoEditor;