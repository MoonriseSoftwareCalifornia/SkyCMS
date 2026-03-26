/**
 * Common Monaco Editor initialization and utilities
 * Shared across all EditCode views (Editor, FileManager, Templates, Layouts)
 * Version: 2.0
 */

/**
 * Initialize code editor with common configuration and extensible hooks
 * @param {Object} config - Configuration options
 * @returns {Object} API for interacting with the editor
 */
function initializeCodeEditor(config) {
    const defaults = {
        initialField: "",
        autoSaveDelay: 1500,
        readOnly: false,
        blockUIOnLoad: false,
        onBeforeInit: null,
        onAfterInit: null,
        onBeforeEditorCreate: null,
        onAfterEditorCreate: null
    };

    const settings = Object.assign({}, defaults, config);

    // Block UI if requested
    if (settings.blockUIOnLoad) {
        $.blockUI();
    }

    // Call pre-initialization hook
    if (typeof settings.onBeforeInit === 'function') {
        settings.onBeforeInit();
    }

    // Override global hooks if provided
    if (typeof settings.onBeforeEditorCreate === 'function') {
        window.beforeEditorCreate = settings.onBeforeEditorCreate;
    }

    if (typeof settings.onAfterEditorCreate === 'function') {
        window.afterEditorCreate = settings.onAfterEditorCreate;
    }

    // Initialize Monaco editor core
    initializeMonacoEditor({
        initialField: settings.initialField,
        autoSaveDelay: settings.autoSaveDelay,
        readOnly: settings.readOnly
    });

    // Call post-initialization hook
    if (typeof settings.onAfterInit === 'function') {
        settings.onAfterInit();
    }

    // Return API for view-specific customization
    return {
        registerSaveHandler: function(handler) {
            window.saveChanges = handler;
        },
        getEditor: function() {
            return window.editor;
        },
        getCurrentFieldId: function() {
            return window.fieldId;
        }
    };
}

/**
 * Build error message HTML from validation errors
 * @param {Array} errors - Array of validation errors
 * @returns {string} HTML error message
 */
function buildErrorMessage(errors) {
    let msg = "<h5>Error(s) detected while saving:</h5>";
    $.each(errors, function(index, error) {
        msg += "<p>" + error.Key + "</p><ul>";
        $.each(error.Errors, function(i, innerError) {
            const message = innerError.ErrorMessage || 
                          (innerError.Exception && innerError.Exception.Message) || 
                          "Unknown error";
            msg += "<li>" + message + "</li>";
        });
        msg += "</ul>";
    });
    return msg;
}

/**
 * Display validation/save errors in modal
 * @param {Array} errors - Array of errors to display
 */
function displaySaveErrors(errors) {
    const errorMsg = buildErrorMessage(errors);
    $("#divErrorLog").html(errorMsg);
    const errorModal = new bootstrap.Modal(document.getElementById('modalSavingError'));
    errorModal.show();
}

/**
 * Handle successful/failed save response
 * @param {Object} response - Server response
 * @param {Function} onSuccess - Success callback (optional)
 * @param {Function} onError - Error callback (optional)
 */
function handleSaveResponse(response, onSuccess, onError) {
    window.saveInProgress = false;
    doneSaving();

    if (response.isValid || response.IsValid) {
        toastMsg("Successfully saved.");
        
        if (typeof onSuccess === 'function') {
            onSuccess(response);
        }
        
        // Execute any pending next function
        if (window.next && typeof window.next === 'function') {
            window.next();
            window.next = null;
        }
    } else {
        if (typeof onError === 'function') {
            onError(response);
        } else {
            // Default error handling
            displaySaveErrors(response.Errors || response.errors || []);
        }
    }
}

/**
 * Handle AJAX save failure
 * @param {Object} xhr - XMLHttpRequest object
 * @param {string} status - Error status
 * @param {string} error - Error message
 */
function handleSaveFailure(xhr, status, error) {
    window.saveInProgress = false;
    doneSaving();
    
    const errorMsg = xhr.responseText || error || "An error occurred while saving.";
    $("#divErrorLog").html("<h5>Error:</h5><p>" + errorMsg + "</p>");
    
    const errorModal = new bootstrap.Modal(document.getElementById('modalSavingError'));
    errorModal.show();
}

/**
 * Prepare form data with encryption for specified fields
 * @param {string} formSelector - jQuery selector for form
 * @param {Array<string>} encryptFields - Fields to encrypt
 * @returns {Object} Prepared model object
 */
function prepareEncryptedModel(formSelector, encryptFields) {
    const form = $(formSelector);
    const model = {};
    
    // Serialize all form fields
    form.serializeArray().forEach(function(field) {
        model[field.name] = field.value;
    });
    
    // Encrypt specified fields
    encryptFields.forEach(function(fieldName) {
        if (model[fieldName]) {
            model[fieldName] = encryptData(model[fieldName]);
        }
    });
    
    // Add current editor content if available
    if (window.editor && window.fieldId) {
        const currentFieldValue = window.editor.getValue();
        model[window.fieldId] = encryptData(currentFieldValue);
        
        // Update hidden field
        $("#" + window.fieldId).val(currentFieldValue);
    }
    
    return model;
}

/**
 * Setup common UI elements (tooltips, insert buttons, etc.)
 */
function setupCommonEditorUI() {
    // Initialize Bootstrap tooltips
    $('[data-bs-toggle="tooltip"]').tooltip();
    
    // Setup insert button handlers if the function exists
    if (typeof setupInsertButtonHandlers === 'function') {
        setupInsertButtonHandlers();
    }
}

/**
 * Setup cleanup handlers for page unload
 */
function setupEditorCleanup() {
    $(document).on("unload", function() {
        if (typeof window.fileMgrPopup !== "undefined" && 
            window.fileMgrPopup !== null && 
            window.fileMgrPopup.location) {
            window.fileMgrPopup.close();
        }
    });
}