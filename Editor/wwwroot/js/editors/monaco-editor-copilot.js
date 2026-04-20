/**
 * Monaco Copilot Integration
 * Wires Monaco inline completions to the SkyCMS Copilot proxy API.
 */
(function () {
    class MonacoCopilot {
        constructor() {
            this.initialized = false;
            this.core = null;
            this.providerDisposables = [];
            this.lastRequestAt = 0;
            this.minRequestIntervalMs = 1500;
            this.cooldownUntil = 0;
            this.lastRequestSignature = null;
            this.inlineSuggestionsEnabled = false;
            this.supportedLanguages = [
                'javascript',
                'typescript',
                'html',
                'css',
                'json',
                'markdown',
                'xml',
                'sql',
                'csharp',
                'plaintext'
            ];
        }

        async initialize(core, options) {
            if (this.initialized) {
                return;
            }

            this.core = core;
            this.inlineSuggestionsEnabled = !!(options && options.enableInlineSuggestions === true);

            if (!window.monaco || !window.monaco.languages || !window.monaco.languages.registerInlineCompletionsProvider) {
                this._setIndicator(false);
                return;
            }

            const status = await this._getStatus();
            const enabled = status && (status.enabled ?? status.Enabled);
            const configured = status && (status.configured ?? status.Configured);
            const model = status && (status.model ?? status.Model);
            const copilotOn = !!(enabled && configured);

            this._setIndicator(copilotOn, model);
            if (!copilotOn || !this.inlineSuggestionsEnabled) {
                return;
            }

            this._registerInlineProviders();
            this.initialized = true;
        }

        dispose() {
            this.providerDisposables.forEach(d => {
                if (d && typeof d.dispose === 'function') {
                    d.dispose();
                }
            });
            this.providerDisposables = [];
            this.initialized = false;
        }

        async _getStatus() {
            try {
                const response = await fetch('/api/copilot/status', {
                    method: 'GET',
                    credentials: 'same-origin',
                    headers: {
                        'Accept': 'application/json'
                    }
                });

                if (!response.ok) {
                    return null;
                }

                return await response.json();
            } catch (error) {
                console.warn('Copilot status check failed:', error);
                return null;
            }
        }

        _setIndicator(enabled, model) {
            this._setSingleIndicator('ccmsCopilotStatusIndicator', 'ccmsCopilotStatusBadge', enabled, model);
        }

        _setSingleIndicator(indicatorId, badgeId, enabled, model) {
            const indicator = document.getElementById(indicatorId);
            const badge = document.getElementById(badgeId);
            if (!indicator || !badge) {
                return;
            }

            if (enabled) {
                indicator.style.display = '';
                badge.classList.remove('text-bg-secondary');
                badge.classList.add('text-bg-success');
                badge.title = model
                    ? `AI assistant available (${model})`
                    : 'AI assistant available';
            } else {
                indicator.style.display = 'none';
            }
        }

        _registerInlineProviders() {
            this.supportedLanguages.forEach(language => {
                const disposable = window.monaco.languages.registerInlineCompletionsProvider(language, {
                    provideInlineCompletions: async (model, position, context, token) => {
                        if (!model || !position || token?.isCancellationRequested) {
                            return { items: [], dispose() {} };
                        }

                        if (Date.now() < this.cooldownUntil) {
                            return { items: [], dispose() {} };
                        }

                        const now = Date.now();
                        if (now - this.lastRequestAt < this.minRequestIntervalMs) {
                            return { items: [], dispose() {} };
                        }

                        const currentLinePrefix = (model.getLineContent(position.lineNumber) || '').slice(0, Math.max(0, position.column - 1));
                        const currentLinePrefixTrimmed = currentLinePrefix.trim();
                        if (currentLinePrefixTrimmed.length < 3) {
                            return { items: [], dispose() {} };
                        }

                        const signature = `${model.uri?.toString() || ''}|${position.lineNumber}|${position.column}|${currentLinePrefix.slice(-120)}`;
                        if (signature === this.lastRequestSignature) {
                            return { items: [], dispose() {} };
                        }

                        this.lastRequestAt = now;
                        this.lastRequestSignature = signature;

                        const completion = await this._fetchCompletion(model, position, token);
                        if (!completion || token?.isCancellationRequested) {
                            return { items: [], dispose() {} };
                        }

                        return {
                            items: [
                                {
                                    insertText: completion,
                                    range: new window.monaco.Range(
                                        position.lineNumber,
                                        position.column,
                                        position.lineNumber,
                                        position.column
                                    )
                                }
                            ],
                            dispose() {}
                        };
                    },
                    // Keep both method names for Monaco API compatibility across versions.
                    freeInlineCompletions: () => {},
                    disposeInlineCompletions: () => {}
                });

                this.providerDisposables.push(disposable);
            });
        }

        async _fetchCompletion(model, position, token) {
            try {
                const prefixRange = new window.monaco.Range(1, 1, position.lineNumber, position.column);
                const suffixRange = new window.monaco.Range(
                    position.lineNumber,
                    position.column,
                    model.getLineCount(),
                    model.getLineMaxColumn(model.getLineCount())
                );

                let prefix = model.getValueInRange(prefixRange) || '';
                let suffix = model.getValueInRange(suffixRange) || '';

                if (prefix.length > 4000) {
                    prefix = prefix.slice(prefix.length - 4000);
                }
                if (suffix.length > 1000) {
                    suffix = suffix.slice(0, 1000);
                }

                const ctx = window.ccmsEditorContext || {};
                const activeFieldName = this._getActiveFieldName();
                const sectionKind = (ctx.sectionKindMap && activeFieldName && ctx.sectionKindMap[activeFieldName])
                    || ctx.sectionKind
                    || null;

                const payload = {
                    prefix: prefix,
                    suffix: suffix,
                    language: model.getLanguageId(),
                    fieldId: this._getActiveFieldId(),
                    uri: model.uri ? model.uri.toString() : null,
                    documentKind: ctx.documentKind || null,
                    sectionKind: sectionKind
                };

                const response = await fetch('/api/copilot/complete', {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: {
                        'Content-Type': 'application/json',
                        'Accept': 'application/json'
                    },
                    body: JSON.stringify(payload)
                });

                if (response.status === 429) {
                    const retryAfterRaw = response.headers.get('Retry-After');
                    const retryAfterSeconds = Number.parseInt(retryAfterRaw || '0', 10);
                    if (Number.isFinite(retryAfterSeconds) && retryAfterSeconds > 0) {
                        this.cooldownUntil = Date.now() + (retryAfterSeconds * 1000);
                    } else {
                        this.cooldownUntil = Date.now() + 5000;
                    }

                    return null;
                }

                if (!response.ok || token?.isCancellationRequested) {
                    return null;
                }

                const data = await response.json();
                const completion = (data && (data.completion || (data.completions && data.completions[0]))) || '';
                return typeof completion === 'string' ? completion : null;
            } catch (error) {
                return null;
            }
        }

        _getActiveFieldId() {
            if (window.monacoEditorCore && window.monacoEditorCore.currentField && window.monacoEditorCore.currentField.FieldId) {
                return window.monacoEditorCore.currentField.FieldId;
            }

            const editingField = document.getElementById('EditingField');
            return editingField ? editingField.value : null;
        }

        _getActiveFieldName() {
            if (window.monacoEditorCore && window.monacoEditorCore.currentField && window.monacoEditorCore.currentField.FieldName) {
                return window.monacoEditorCore.currentField.FieldName;
            }

            return null;
        }
    }

    window.monacoCopilot = new MonacoCopilot();
})();
