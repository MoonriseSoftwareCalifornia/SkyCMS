(function () {
    function escapeHtml(value) {
        return (value || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/\"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function appendTextSegment(container, text) {
        if (!text) {
            return;
        }

        const textBlock = document.createElement('div');
        textBlock.className = 'copilot-chat-markdown-text';
        textBlock.textContent = text;
        container.appendChild(textBlock);
    }

    function appendCodeFence(container, language, codeText) {
        const wrapper = document.createElement('div');
        wrapper.className = 'copilot-chat-code-wrapper';

        if (language) {
            const label = document.createElement('div');
            label.className = 'copilot-chat-code-language';
            label.textContent = language;
            wrapper.appendChild(label);
        }

        const pre = document.createElement('pre');
        pre.className = 'copilot-chat-code-block';
        const code = document.createElement('code');
        if (language) {
            code.className = `language-${escapeHtml(language.toLowerCase())}`;
        }

        code.textContent = codeText || '';
        pre.appendChild(code);
        wrapper.appendChild(pre);
        container.appendChild(wrapper);
    }

    function renderMarkdownLikeContent(container, text) {
        if (!container) {
            return;
        }

        container.innerHTML = '';

        if (!text) {
            return;
        }

        const source = String(text).replace(/\r\n/g, '\n');
        const fenceRegex = /```([a-zA-Z0-9_-]+)?\n?([\s\S]*?)```/g;
        let lastIndex = 0;
        let match;

        while ((match = fenceRegex.exec(source)) !== null) {
            const preceding = source.slice(lastIndex, match.index);
            appendTextSegment(container, preceding);

            const language = (match[1] || '').trim();
            const codeText = (match[2] || '').replace(/\n$/, '');
            appendCodeFence(container, language, codeText);

            lastIndex = fenceRegex.lastIndex;
        }

        const trailing = source.slice(lastIndex);
        appendTextSegment(container, trailing);
    }

    function getAiModelCatalog() {
        return window.ccmsAiModelCatalog;
    }

    function getAiModelSelectionState() {
        if (!window.ccmsAiModelSelection) {
            window.ccmsAiModelSelection = {
                selectedModel: null,
                status: null,
                catalog: null
            };
        }

        return window.ccmsAiModelSelection;
    }

    function getAiPreferenceContext() {
        const editorContext = window.ccmsEditorContext || {};
        return {
            editorKind: editorContext.editorSurface || 'monaco',
            documentKind: editorContext.documentKind || null
        };
    }

    function buildPreferenceQueryString() {
        const context = getAiPreferenceContext();
        const params = new URLSearchParams();
        if (context.editorKind) {
            params.set('editorKind', context.editorKind);
        }

        if (context.documentKind) {
            params.set('documentKind', context.documentKind);
        }

        return params.toString();
    }

    async function normalizeSelectedModelAgainstCatalog(catalog, savePreference) {
        const state = getAiModelSelectionState();
        const supportsSelection = !!(catalog && (catalog.supportsUserModelSelection ?? catalog.SupportsUserModelSelection));
        const models = catalog && (catalog.models ?? catalog.Models) ? (catalog.models ?? catalog.Models) : [];
        if (!supportsSelection || !state.selectedModel) {
            if (!supportsSelection && state.selectedModel) {
                state.selectedModel = null;
                if (savePreference) {
                    await savePreference(null);
                }
            }

            return;
        }

        const selectionExists = models.some((model) => {
            const modelId = model.id ?? model.Id ?? '';
            return modelId.toLowerCase() === state.selectedModel.toLowerCase();
        });

        if (!selectionExists) {
            state.selectedModel = null;
            if (savePreference) {
                await savePreference(null);
            }
        }
    }

    class MonacoCopilotChat {
        constructor() {
            this.initialized = false;
            this.core = null;
            this.messages = [];
            this.isSending = false;
            this.status = null;
            this.catalog = null;
            this.catalogPromise = null;
            this.available = false;
            this.nextMessageId = 1;
            this.largeInsertThreshold = 1500;
            this.actionPrompts = {
                'explain-file': 'Explain the current file at a high level and call out anything important or risky.',
                'fix-syntax': 'Review the current code for syntax or structural issues and propose the smallest safe fix.',
                'improve-selection': 'Improve the current selection while preserving intent and surrounding style.',
                'generate-section': 'Generate the next useful section or block for the current file and explain where it should go.',
                'convert-selection': 'Convert the current selection into the most appropriate Razor, HTML, CSS, or JavaScript for this editing context.'
            };
        }

        async initialize(core) {
            if (this.initialized) {
                this.core = core;
                return;
            }

            this.core = core;
            this.cacheElements();
            if (!this.panel) {
                return;
            }

            this.attachListeners();
            this.status = await this.getStatus();
            this.syncStateFromStatus();
            this.updateAvailability();
            await this.loadModelCatalog();
            this.renderMessages();
            this.initialized = true;
        }

        cacheElements() {
            this.panel = document.getElementById('copilotChatPanel');
            this.subtitle = document.getElementById('copilotChatSubtitle');
            this.messagesContainer = document.getElementById('copilotChatMessages');
            this.statusContainer = document.getElementById('copilotChatStatus');
            this.form = document.getElementById('copilotChatForm');
            this.input = document.getElementById('copilotChatInput');
            this.sendButton = document.getElementById('btnCopilotSend');
            this.clearButton = document.getElementById('btnCopilotClearChat');
            this.modelSelect = document.getElementById('copilotModelSelect');
            this.modelHelp = document.getElementById('copilotModelHelp');
            this.refreshModelsButton = document.getElementById('btnCopilotRefreshModels');
            this.actionButtons = Array.from(document.querySelectorAll('[data-copilot-action]'));
        }

        attachListeners() {
            if (this.form) {
                this.form.addEventListener('submit', async (event) => {
                    event.preventDefault();
                    await this.sendChatMessage('chat', this.input ? this.input.value : '');
                });
            }

            if (this.input) {
                this.input.addEventListener('keydown', async (event) => {
                    if (event.key === 'Enter' && !event.shiftKey) {
                        event.preventDefault();
                        await this.sendChatMessage('chat', this.input.value);
                    }
                });
            }

            if (this.clearButton) {
                this.clearButton.addEventListener('click', () => {
                    this.messages = [];
                    this.renderMessages();
                    this.hideStatus();
                });
            }

            if (this.modelSelect) {
                this.modelSelect.addEventListener('change', async () => {
                    const previousSelection = getAiModelSelectionState().selectedModel;
                    getAiModelSelectionState().selectedModel = this.modelSelect.value || null;
                    this.updateAvailability();

                    const saved = await this.saveSelectedModelPreference(getAiModelSelectionState().selectedModel);
                    if (!saved) {
                        getAiModelSelectionState().selectedModel = previousSelection;
                        this.updateAvailability();
                        this.updateModelPicker();
                    }
                });
            }

            if (this.refreshModelsButton) {
                this.refreshModelsButton.addEventListener('click', async () => {
                    await this.loadModelCatalog(true);
                });
            }

            this.actionButtons.forEach((button) => {
                button.addEventListener('click', async () => {
                    const action = button.getAttribute('data-copilot-action') || 'chat';
                    const inputText = this.input ? this.input.value.trim() : '';
                    const message = inputText || this.actionPrompts[action] || 'Help with the current editor content.';
                    await this.sendChatMessage(action, message);
                });
            });
        }

        async getStatus() {
            const catalog = getAiModelCatalog();
            if (catalog) {
                return await catalog.getStatus();
            }

            try {
                const queryString = buildPreferenceQueryString();
                const response = await fetch(`/api/ai-proxy/status${queryString ? `?${queryString}` : ''}`, {
                    method: 'GET',
                    credentials: 'same-origin',
                    headers: {
                        Accept: 'application/json'
                    }
                });

                if (!response.ok) {
                    return null;
                }

                return await response.json();
            } catch (error) {
                return null;
            }
        }

        updateAvailability() {
            const enabled = this.status && (this.status.enabled ?? this.status.Enabled);
            const configured = this.status && (this.status.configured ?? this.status.Configured);
            const model = this.getDisplayedModel();
            const available = !!(enabled && configured);
            this.available = available;

            if (this.subtitle) {
                this.subtitle.textContent = available
                    ? (model ? `Available via ${model}.` : 'Available for the current editor context.')
                    : 'Unavailable until the AI proxy is configured.';
            }

            if (this.input) {
                this.input.disabled = !available;
            }

            if (this.sendButton) {
                this.sendButton.disabled = !available;
            }

            this.actionButtons.forEach((button) => {
                button.disabled = !available;
            });

            this.updateModelPicker();

            if (!available) {
                this.showStatus('AI chat is unavailable for this tenant right now.');
            }
        }

        syncStateFromStatus() {
            getAiModelSelectionState().status = this.status;
            getAiModelSelectionState().selectedModel = this.status && ((this.status.selectedModel ?? this.status.SelectedModel) || null);
        }

        async loadModelCatalog(forceRefresh) {
            const catalog = getAiModelCatalog();
            if (catalog) {
                this.catalog = await catalog.getCatalog({
                    forceRefresh: !!forceRefresh,
                    providerKey: 'monaco'
                });
                getAiModelSelectionState().selectedModel = catalog.getSelectedModel();
                await catalog.updateModelSelectionFromCatalog(this.catalog, (selectedModel) => catalog.saveSelectedModelPreference(selectedModel));
                this.updateModelPicker();
                return this.catalog;
            }

            if (this.catalogPromise && !forceRefresh) {
                return this.catalogPromise;
            }

            const enabled = this.status && (this.status.enabled ?? this.status.Enabled);
            const configured = this.status && (this.status.configured ?? this.status.Configured);
            if (!(enabled && configured)) {
                this.catalog = null;
                this.updateModelPicker();
                return null;
            }

            const params = new URLSearchParams(buildPreferenceQueryString());
            if (forceRefresh) {
                params.set('forceRefresh', 'true');
            }

            const queryString = params.toString();
            this.catalogPromise = fetch(`/api/ai-proxy/models${queryString ? `?${queryString}` : ''}`, {
                method: 'GET',
                credentials: 'same-origin',
                headers: {
                    Accept: 'application/json'
                }
            })
                .then(async (response) => {
                    if (!response.ok) {
                        return null;
                    }

                    return await response.json();
                })
                .catch(() => null)
                .finally(() => {
                    this.catalogPromise = null;
                });

            this.catalog = await this.catalogPromise;
            getAiModelSelectionState().catalog = this.catalog;
            if (this.catalog) {
                const selectedModel = this.catalog.selectedModel ?? this.catalog.SelectedModel;
                if (selectedModel !== undefined) {
                    getAiModelSelectionState().selectedModel = selectedModel || null;
                }
            }

            await normalizeSelectedModelAgainstCatalog(this.catalog, (selectedModel) => this.saveSelectedModelPreference(selectedModel));
            this.updateModelPicker();
            return this.catalog;
        }

        async saveSelectedModelPreference(selectedModel) {
            try {
                const context = getAiPreferenceContext();
                const response = await fetch('/api/ai-proxy/preferences/model', {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: {
                        'Content-Type': 'application/json',
                        Accept: 'application/json'
                    },
                    body: JSON.stringify({
                        editorKind: context.editorKind,
                        documentKind: context.documentKind,
                        selectedModel: selectedModel
                    })
                });

                return response.ok;
            } catch {
                return false;
            }
        }

        getDisplayedModel() {
            const selectedModel = getAiModelSelectionState().selectedModel;
            if (selectedModel) {
                return selectedModel;
            }

            return this.status && ((this.status.effectiveModel ?? this.status.EffectiveModel) || (this.status.model ?? this.status.Model));
        }

        getSelectedModel() {
            return getAiModelSelectionState().selectedModel || null;
        }

        updateModelPicker() {
            if (!this.modelSelect) {
                return;
            }

            const catalog = this.catalog || getAiModelSelectionState().catalog;
            const supportsSelection = !!(catalog && (catalog.supportsUserModelSelection ?? catalog.SupportsUserModelSelection));
            const discoveryStateMessage = catalog && (catalog.discoveryStateMessage ?? catalog.DiscoveryStateMessage);
            const discoveryError = catalog && (catalog.discoveryError ?? catalog.DiscoveryError);
            const models = catalog && (catalog.models ?? catalog.Models) ? (catalog.models ?? catalog.Models) : [];
            const defaultSelectionLabel = (catalog && (catalog.defaultSelectionLabel ?? catalog.DefaultSelectionLabel))
                || (this.status && (this.status.defaultSelectionLabel ?? this.status.DefaultSelectionLabel))
                || 'Default';
            const currentValue = getAiModelSelectionState().selectedModel || '';

            this.modelSelect.innerHTML = '';

            const defaultOption = document.createElement('option');
            defaultOption.value = '';
            defaultOption.textContent = defaultSelectionLabel;
            this.modelSelect.appendChild(defaultOption);

            models.forEach((model) => {
                const option = document.createElement('option');
                option.value = model.id ?? model.Id ?? '';
                option.textContent = model.displayName ?? model.DisplayName ?? option.value;
                this.modelSelect.appendChild(option);
            });

            this.modelSelect.value = currentValue;
            this.modelSelect.disabled = !supportsSelection || models.length === 0 || !this.available;

            if (this.refreshModelsButton) {
                this.refreshModelsButton.disabled = !this.available;
            }

            if (this.modelHelp) {
                if (discoveryError) {
                    this.modelHelp.textContent = discoveryError;
                } else if (supportsSelection && models.length > 0) {
                    this.modelHelp.textContent = 'Select a model for this editor context. Your choice is saved for your account on this site. Leaving the selector on Default uses the tenant default behavior.';
                } else {
                    this.modelHelp.textContent = discoveryStateMessage || defaultSelectionLabel;
                }
            }
        }

        async sendChatMessage(action, message) {
            const trimmed = (message || '').trim();
            if (!trimmed || this.isSending) {
                return;
            }

            if (!this.status) {
                this.status = await this.getStatus();
                this.updateAvailability();
            }

            const enabled = this.status && (this.status.enabled ?? this.status.Enabled);
            const configured = this.status && (this.status.configured ?? this.status.Configured);
            if (!(enabled && configured)) {
                this.showStatus('AI chat is unavailable for this tenant right now.');
                return;
            }

            const payload = this.buildPayload(action, trimmed);
            this.isSending = true;
            this.setBusy(true);
            this.hideStatus();

            let hadError = false;

            this.messages.push(this.createMessage('user', trimmed));
            this.renderMessages();

            try {
                const response = await fetch('/api/ai-proxy/chat', {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: {
                        'Content-Type': 'application/json',
                        Accept: 'application/json'
                    },
                    body: JSON.stringify(payload)
                });

                const data = await response.json().catch(() => null);

                if (response.status === 429) {
                    hadError = true;
                    const retryAfterSeconds = data && (data.retryAfterSeconds ?? data.RetryAfterSeconds)
                        ? (data.retryAfterSeconds ?? data.RetryAfterSeconds)
                        : 5;
                    this.showStatus(`AI chat is rate-limited. Try again in about ${retryAfterSeconds} seconds.`);
                    return;
                }

                if (!response.ok) {
                    hadError = true;
                    const errorMessage = data && (data.error ?? data.Error)
                        ? (data.error ?? data.Error)
                        : 'The AI chat request failed.';
                    this.showStatus(errorMessage);
                    return;
                }

                const reply = data && (data.reply ?? data.Reply)
                    ? (data.reply ?? data.Reply)
                    : 'I do not have a useful answer yet.';
                this.messages.push(this.createMessage('assistant', reply));
                this.renderMessages();

                if (this.input) {
                    this.input.value = '';
                }
            } catch (error) {
                hadError = true;
                this.showStatus('The AI chat request failed before a response was returned.');
            } finally {
                if (hadError) {
                    await this.flashErrorOnSendButton();
                }

                this.isSending = false;
                this.setBusy(false);
            }
        }

        async flashErrorOnSendButton() {
            if (!this.sendButton) {
                return;
            }

            this.sendButton.classList.remove('btn-primary', 'btn-success');
            this.sendButton.classList.add('btn-danger');
            this.sendButton.innerHTML = '<span class="me-1">!</span>Error';
            this.sendButton.disabled = true;
            await new Promise((resolve) => setTimeout(resolve, 900));
        }

        setBusy(isBusy) {
            if (this.sendButton) {
                this.sendButton.classList.remove('btn-primary', 'btn-success', 'btn-danger');
                this.sendButton.classList.add(isBusy ? 'btn-success' : 'btn-primary');
                this.sendButton.disabled = isBusy || !this.available;
                this.sendButton.innerHTML = isBusy
                    ? '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>Thinking...'
                    : 'Send';
            }

            this.actionButtons.forEach((button) => {
                button.disabled = isBusy || !this.available;
            });

            if (this.input) {
                this.input.readOnly = isBusy;
            }

            if (isBusy) {
                this.showThinking();
            } else {
                this.hideThinking();
            }
        }

        buildPayload(action, message) {
            const context = getAiPreferenceContext();
            const editorContext = this.captureEditorContext();
            const selectedModel = this.getSelectedModel();

            return {
                editorKind: context.editorKind,
                action: action,
                message: message,
                selection: editorContext.selection || null,
                currentCode: editorContext.currentCode || null,
                language: editorContext.language || 'plaintext',
                fieldName: editorContext.fieldName || null,
                title: editorContext.title || null,
                articleNumber: editorContext.articleNumber || null,
                documentKind: context.documentKind || editorContext.documentKind || null,
                sectionKind: editorContext.sectionKind || null,
                articleType: editorContext.articleType || null,
                category: editorContext.category || null,
                urlPath: editorContext.urlPath || null,
                templateId: editorContext.templateId || null,
                layoutId: editorContext.layoutId || null,
                selectedModel: selectedModel,
                messages: this.messages.slice(-8).map((msg) => ({
                    role: msg.role,
                    content: msg.content
                }))
            };
        }

        captureEditorContext() {
            if (!this.core || !this.core.editor) {
                return {};
            }

            const editor = this.core.editor;
            const model = editor.getModel();
            if (!model) {
                return {};
            }

            const selection = editor.getSelection();
            const selectedText = selection && !selection.isEmpty()
                ? model.getValueInRange(selection)
                : null;

            const editorContext = window.ccmsEditorContext || {};
            const currentField = this.core.currentField || null;
            const currentFieldName = currentField && currentField.FieldName ? currentField.FieldName : null;
            const sectionKindMap = editorContext.sectionKindMap || {};
            const sectionKind = editorContext.sectionKind
                || (currentFieldName && sectionKindMap[currentFieldName])
                || null;

            const readValue = (id) => {
                const element = document.getElementById(id);
                if (!element) {
                    return null;
                }

                const value = (element.value || '').trim();
                return value.length > 0 ? value : null;
            };

            return {
                selection: selectedText,
                currentCode: model.getValue(),
                language: model.getLanguageId ? model.getLanguageId() : (model.getModeId ? model.getModeId() : 'plaintext'),
                fieldName: currentFieldName || editorContext.fieldName || null,
                title: editorContext.title || readValue('Title'),
                articleNumber: editorContext.articleNumber || readValue('ArticleNumber'),
                documentKind: editorContext.documentKind || null,
                sectionKind: sectionKind,
                articleType: editorContext.articleType || null,
                category: editorContext.category || null,
                urlPath: editorContext.urlPath || readValue('UrlPath'),
                templateId: editorContext.templateId || null,
                layoutId: editorContext.layoutId || null
            };
        }

        createMessage(role, content) {
            return {
                id: this.nextMessageId++,
                role: role,
                content: content
            };
        }

        renderMessages() {
            if (!this.messagesContainer) {
                return;
            }

            this.messagesContainer.innerHTML = '';
            if (!this.messages.length) {
                const empty = document.createElement('div');
                empty.className = 'copilot-chat-empty';
                empty.textContent = 'Ask your first question to start the Monaco AI chat.';
                this.messagesContainer.appendChild(empty);
                return;
            }

            this.messages.forEach((entry) => {
                const item = document.createElement('div');
                item.className = `copilot-chat-message ${entry.role}`;

                const content = document.createElement('div');
                content.className = 'copilot-chat-message-content';
                renderMarkdownLikeContent(content, entry.content);
                item.appendChild(content);

                this.messagesContainer.appendChild(item);
            });

            this.messagesContainer.scrollTop = this.messagesContainer.scrollHeight;
        }

        showStatus(message) {
            if (!this.statusContainer) {
                return;
            }

            this.statusContainer.classList.remove('is-thinking');
            this.statusContainer.textContent = message;
            this.statusContainer.style.display = 'block';
        }

        hideStatus() {
            if (!this.statusContainer) {
                return;
            }

            this.statusContainer.classList.remove('is-thinking');
            this.statusContainer.textContent = '';
            this.statusContainer.style.display = 'none';
        }

        showThinking() {
            if (!this.statusContainer) {
                return;
            }

            this.statusContainer.classList.add('is-thinking');
            this.statusContainer.textContent = 'Thinking...';
            this.statusContainer.style.display = 'block';
        }

        hideThinking() {
            if (!this.statusContainer || !this.statusContainer.classList.contains('is-thinking')) {
                return;
            }

            this.hideStatus();
        }
    }

    window.monacoCopilotChat = new MonacoCopilotChat();
})();