/**
 * Unified editor save service for Edit, EditCode, and Designer editors.
 * Provides encryption, orchestration, and response handling.
 */
const EditorSaveService = {
  // State
  state: {
    saveInProgress: false,
    encryptionContextReady: false,
    lastSaveTime: null,
    inFlightFingerprint: null,
    lastCompletedFingerprint: null
  },

  // ============================================================================
  // ENCRYPTION CONTEXT
  // ============================================================================

  /**
   * Ensure encryption context is bootstrapped.
   * @returns {Promise<boolean>}
   */
  async ensureEncryptionContext() {
    if (this.state.encryptionContextReady) {
      return true;
    }

    try {
      const response = await fetch("/Editor/GetEncryptionKey", {
        method: "GET",
        credentials: "same-origin"
      });

      if (!response.ok) {
        console.error("Failed to fetch encryption context");
        return false;
      }

      const payload = await response.json();
      if (payload && payload.keyText) {
        if (typeof setEncryptionKey === 'function') {
          setEncryptionKey(payload.keyText, payload.contextToken || "");
        }
        this.state.encryptionContextReady = true;
        return true;
      }
    } catch (e) {
      console.warn("Could not load encryption context", e);
    }

    return false;
  },

  /**
   * Get current encryption context token.
   * @returns {string}
   */
  getContextToken() {
    return typeof getEncryptionContextToken === 'function' 
      ? getEncryptionContextToken() 
      : "";
  },

  // ============================================================================
  // ENCRYPTION UTILITIES
  // ============================================================================

  /**
   * Encrypt a single field.
   * @param {string} plaintext
   * @returns {string}
   */
  encryptField(plaintext) {
    if (!plaintext) return "";
    return typeof encryptData === 'function' ? encryptData(plaintext) : plaintext;
  },

  /**
   * Encrypt multiple fields in a model.
   * @param {object} model
   * @param {string[]} fieldsToEncrypt
   * @returns {object}
   */
  encryptModel(model, fieldsToEncrypt = ['Payload', 'HeadJavaScript', 'FooterJavaScript']) {
    const encrypted = { ...model };
    fieldsToEncrypt.forEach(field => {
      if (encrypted[field]) {
        encrypted[field] = this.encryptField(encrypted[field]);
      }
    });
    return encrypted;
  },

  // ============================================================================
  // FORM UTILITIES
  // ============================================================================

  /**
   * Get antiforgery token from form.
   * @returns {string}
   */
  getAntiforgeryToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
  },

  /**
   * Get form value by field ID.
   * @param {string} fieldId
   * @returns {string}
   */
  getFormValue(fieldId) {
    const element = document.getElementById(fieldId);
    return element ? element.value : "";
  },

  /**
   * Set form value by field ID.
   * @param {string} fieldId
   * @param {string} value
   */
  setFormValue(fieldId, value) {
    const element = document.getElementById(fieldId);
    if (element) element.value = value;
  },

  // ============================================================================
  // VALIDATION
  // ============================================================================

  /**
   * Check if model state is valid.
   * @returns {boolean}
   */
  validateModelState() {
    // Check for common validation errors
    // Can be extended per editor
    return true;
  },

  // ============================================================================
  // SAVE ORCHESTRATION
  // ============================================================================

  /**
   * Create a deterministic fingerprint for deduping saves.
   * @param {object} model
   * @param {string} command
   * @returns {string}
   */
  createSaveFingerprint(model, command) {
    const fingerprintPayload = {
      command: command || '',
      model: {
        Id: model?.Id ?? null,
        ArticleNumber: model?.ArticleNumber ?? null,
        EditorId: model?.EditorId ?? null,
        Payload: model?.Payload ?? '',
        HeadJavaScript: model?.HeadJavaScript ?? '',
        FooterJavaScript: model?.FooterJavaScript ?? '',
        CssContent: model?.CssContent ?? '',
        Title: model?.Title ?? '',
        UrlPath: model?.UrlPath ?? '',
        BannerImage: model?.BannerImage ?? '',
        Published: model?.Published ?? '',
        Updated: model?.Updated ?? '',
        VersionNumber: model?.VersionNumber ?? '',
        EditingField: model?.EditingField ?? '',
        ArticleType: model?.ArticleType ?? '',
        Category: model?.Category ?? '',
        Introduction: model?.Introduction ?? ''
      }
    };

    return this.stableStringify(fingerprintPayload);
  },

  /**
   * Stable JSON stringify with sorted keys.
   * @param {any} value
   * @returns {string}
   */
  stableStringify(value) {
    if (value === null || typeof value !== 'object') {
      return JSON.stringify(value);
    }

    if (Array.isArray(value)) {
      return `[${value.map(v => this.stableStringify(v)).join(',')}]`;
    }

    const keys = Object.keys(value).sort();
    const content = keys
      .map(k => `${JSON.stringify(k)}:${this.stableStringify(value[k])}`)
      .join(',');
    return `{${content}}`;
  },

  /**
   * Wait for current save to complete.
   * @param {number} timeoutMs
   * @param {number} pollMs
   * @returns {Promise<boolean>}
   */
  async waitForSaveSlot(timeoutMs = 3000, pollMs = 100) {
    const start = Date.now();
    while (this.state.saveInProgress && (Date.now() - start) < timeoutMs) {
      await this.sleep(pollMs);
    }
    return !this.state.saveInProgress;
  },

  /**
   * Unified save for all editor types.
   * @param {object} model - EditPostViewModel
   * @param {string} command - SaveBody, SaveRegion, SaveCode, SavePageProperties
   * @param {object} options - Optional config { encryptFields, endpoint }
   * @returns {Promise<EditorResponse>}
   */
  async saveArticle(model, command, options = {}) {
    const {
      encryptFields = ['Payload', 'HeadJavaScript', 'FooterJavaScript'],
      endpoint = "/Editor/Edit"
    } = options;

    const fingerprint = this.createSaveFingerprint(model, command);

    // Prevent concurrent saves and skip redundant requests.
    if (this.state.saveInProgress) {
      // Exact same content is already being saved.
      if (fingerprint === this.state.inFlightFingerprint) {
        return null;
      }

      const acquiredSlot = await this.waitForSaveSlot(3000, 100);
      if (!acquiredSlot) {
        return null;
      }

      // If the just-finished save already persisted this exact content, skip.
      if (fingerprint === this.state.lastCompletedFingerprint) {
        return null;
      }
    }

    this.state.saveInProgress = true;
    this.state.inFlightFingerprint = fingerprint;
    this.state.lastSaveTime = new Date();

    try {
      // Ensure encryption context before sending
      await this.ensureEncryptionContext();

      // Prepare model with encryption
      const encryptedModel = this.encryptModel(model, encryptFields);
      encryptedModel.CryptoContextToken = this.getContextToken();

      // Include antiforgery token
      const token = this.getAntiforgeryToken();
      if (token) {
        encryptedModel.__RequestVerificationToken = token;
      }

      // Build query string with command
      const url = `${endpoint}?Command=${encodeURIComponent(command)}`;

      // POST to server
      const response = await fetch(url, {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(encryptedModel),
        credentials: "same-origin"
      });

      // Parse response
      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`HTTP ${response.status}: ${errorText}`);
      }

      const result = await response.json();
      if (result?.ServerSideSuccess) {
        this.state.lastCompletedFingerprint = fingerprint;
      }
      return result;

    } catch (error) {
      console.error("Save failed:", error);
      throw error;
    } finally {
      this.state.saveInProgress = false;
      this.state.inFlightFingerprint = null;
    }
  },

  // ============================================================================
  // RESPONSE HANDLING
  // ============================================================================

  /**
   * Handle successful save response.
   * @param {EditorResponse} response
   * @param {object} callbacks - { onSuccess, onError, updateUI }
   */
  handleSaveResponse(response, callbacks = {}) {
    const {
      onSuccess = null,
      onError = null,
      updateUI = null
    } = callbacks;

    if (!response) {
      const errorMessage = "No response from server.";
      if (onError) {
        onError(errorMessage);
      } else {
        this.handleSaveError(errorMessage);
      }

      return false;
    }

    if (!response.ServerSideSuccess) {
      const errorMessage = this.getErrorMessage(response);
      if (onError) {
        onError(errorMessage);
      } else {
        this.handleSaveError(errorMessage);
      }

      return false;
    }

    // Success
    if (updateUI && response.Model) {
      this.updatePageProperties(response.Model);
    }

    if (response.CdnResults && response.CdnResults.length > 0) {
      this.handleCdnResults(response.CdnResults);
    }

    if (onSuccess) onSuccess(response);
    return true;
  },

  /**
   * Build a readable error message from a save response.
   * @param {object} response
   * @returns {string}
   */
  getErrorMessage(response) {
    const errors = response?.Errors || response?.errors;
    if (errors && typeof errors === 'object') {
      const errorMessages = [];
      for (const [field, messages] of Object.entries(errors)) {
        const text = Array.isArray(messages) ? messages.join(', ') : String(messages);
        errorMessages.push(`${field}: ${text}`);
      }

      if (errorMessages.length > 0) {
        return errorMessages.join('\n');
      }
    }

    return response?.ErrorMessage
      || response?.errorMessage
      || response?.message
      || 'An error occurred while saving.';
  },

  /**
   * Update page properties from model.
   * @param {object} model
   */
  updatePageProperties(model) {
    if (model.UrlPath !== undefined && model.UrlPath !== null) this.setFormValue("UrlPath", model.UrlPath);
    if (model.RoleList !== undefined && model.RoleList !== null) this.setFormValue("RoleList", model.RoleList);
    if (model.Title !== undefined && model.Title !== null) {
      this.setFormValue("Title", model.Title);
      const titleDiv = document.getElementById("divTitle");
      if (titleDiv) titleDiv.innerText = model.Title;
    }
    if (model.Published !== undefined && model.Published !== null) this.setFormValue("Published", model.Published);
    if (model.Updated !== undefined && model.Updated !== null) this.setFormValue("Updated", model.Updated);
    if (model.BannerImage !== undefined && model.BannerImage !== null) this.setFormValue("BannerImage", model.BannerImage);
    if (model.VersionNumber !== undefined && model.VersionNumber !== null) {
      this.setFormValue("VersionNumber", model.VersionNumber);
      const versionSpan = document.getElementById("spanWorkingVersionNo");
      if (versionSpan) versionSpan.innerText = `Working Version: ${model.VersionNumber}`;
    }
  },

  /**
   * Handle CDN flush results.
   * @param {array} cdnResults
   */
  handleCdnResults(cdnResults) {
    if (!cdnResults || cdnResults.length === 0) return;

    const result = cdnResults[0];
    if (result.EstimatedFlushDateTime) {
      const dateTime = new Date(result.EstimatedFlushDateTime);
      const cdnMsg = document.getElementById("cdnMsg");
      if (cdnMsg) {
        cdnMsg.innerText = `CDN refresh: ${dateTime.toLocaleString()}`;
        cdnMsg.style.display = "block";

        // Clear message after 2 minutes
        setTimeout(() => {
          cdnMsg.innerText = "";
          cdnMsg.style.display = "none";
        }, 120000);
      }
    }
  },

  /**
   * Handle save error and show modal.
   * @param {Error|string} error
   * @param {object} options - { modalId }
   */
  handleSaveError(error, options = {}) {
    const { modalId = "modalSavingError" } = options;

    const errorMessage = error instanceof Error ? error.message : String(error);
    const errorLog = document.getElementById("divErrorLog");

    if (typeof window !== 'undefined') {
      window.ccmsSaveStatusMode = 'error';
    }

    if (errorLog) {
      errorLog.innerHTML = `<p>Save failed:</p><pre>${this.escapeHtml(errorMessage)}</pre>`;
    }

    window.alert(errorMessage);

    const modal = document.getElementById(modalId);
    if (modal && typeof bootstrap !== 'undefined') {
      new bootstrap.Modal(modal).show();
    }
  },

  /**
   * Escape HTML to prevent XSS.
   * @param {string} text
   * @returns {string}
   */
  escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  },

  // ============================================================================
  // UTILITY
  // ============================================================================

  /**
   * Sleep for specified milliseconds.
   * @param {number} ms
   * @returns {Promise}
   */
  sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
  }
};
