(function () {
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
            editorKind: editorContext.editorSurface || 'ckeditor',
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

    class CkeditorCopilotChat {
        constructor() {
            this.initialized = false;
            this.status = null;
            this.statusPromise = null;
            this.catalog = null;
            this.catalogPromise = null;
            this.available = false;
            this.isSending = false;
            this.nextMessageId = 1;
            this.activeEditor = null;
            this.activeEditorId = null;
            this.sessions = new Map();
            this.dragState = null;
            this.actionPrompts = {
                'improve-selection': 'Improve the selected content for grammar, clarity, and flow while preserving the intent and markup. Return the revised HTML in a ```html``` block.',
                'rewrite-selection': 'Rewrite the selected content so it reads more naturally and professionally. Return the revised HTML in a ```html``` block.',
                'shorten-selection': 'Shorten the selected content while preserving the key message and markup. Return the revised HTML in a ```html``` block.',
                'expand-selection': 'Expand the selected content with a bit more detail and polish while preserving the markup style. Return the revised HTML in a ```html``` block.',
                'replace-block': 'Rewrite the entire current editor region so it reads better and is ready to publish. Return the full replacement HTML in a ```html``` block.'
            };
            this.boundDragMove = this.handleDragMove.bind(this);
            this.boundDragEnd = this.stopDrag.bind(this);
        }

        initialize() {
            if (this.initialized) {
                return;
            }

            this.cacheElements();
            if (!this.windowElement) {
                return;
            }

            this.attachListeners();
            this.initialized = true;
            this.ensureStatus();
        }

        cacheElements() {
            this.windowElement = document.getElementById('ckeditorCopilotWindow');
            this.header = document.getElementById('ckeditorCopilotHeader');
            this.subtitle = document.getElementById('ckeditorCopilotSubtitle');
            this.messagesContainer = document.getElementById('ckeditorCopilotMessages');
            this.statusContainer = document.getElementById('ckeditorCopilotStatus');
            this.form = document.getElementById('ckeditorCopilotForm');
            this.input = document.getElementById('ckeditorCopilotInput');
            this.sendButton = document.getElementById('btnCkeditorCopilotSend');
            this.clearButton = document.getElementById('btnCkeditorCopilotClear');
            this.closeButton = document.getElementById('btnCkeditorCopilotClose');
            this.minimizeButton = document.getElementById('btnCkeditorCopilotMinimize');
            this.modelSelect = document.getElementById('ckeditorCopilotModelSelect');
            this.modelHelp = document.getElementById('ckeditorCopilotModelHelp');
            this.refreshModelsButton = document.getElementById('btnCkeditorRefreshModels');
            this.actionButtons = Array.from(document.querySelectorAll('#ckeditorCopilotWindow [data-copilot-action]'));
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
                    const session = this.getActiveSession();
                    if (!session) {
                        return;
                    }

                    session.messages = [];
                    this.renderMessages();
                    this.hideStatus();
                });
            }

            if (this.closeButton) {
                this.closeButton.addEventListener('click', () => {
                    this.windowElement.classList.add('is-hidden');
                    this.hideStatus();
                });
            }

            if (this.minimizeButton) {
                this.minimizeButton.addEventListener('click', () => {
                    this.windowElement.classList.toggle('is-minimized');
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
                    const message = inputText || this.actionPrompts[action] || 'Help improve this editor region.';
                    await this.sendChatMessage(action, message);
                });
            });

            if (this.messagesContainer) {
                this.messagesContainer.addEventListener('click', (event) => {
                    const button = event.target.closest('[data-apply-mode]');
                    if (!button) {
                        return;
                    }

                    const messageId = Number(button.getAttribute('data-message-id'));
                    const mode = button.getAttribute('data-apply-mode');
                    if (!Number.isNaN(messageId) && mode) {
                        this.applySuggestion(messageId, mode);
                    }
                });
            }

            if (this.header) {
                this.header.addEventListener('mousedown', (event) => {
                    if (window.innerWidth <= 768 || event.target.closest('button')) {
                        return;
                    }

                    const rect = this.windowElement.getBoundingClientRect();
                    this.dragState = {
                        offsetX: event.clientX - rect.left,
                        offsetY: event.clientY - rect.top
                    };

                    this.windowElement.style.left = `${rect.left}px`;
                    this.windowElement.style.top = `${rect.top}px`;
                    this.windowElement.style.right = 'auto';

                    document.addEventListener('mousemove', this.boundDragMove);
                    document.addEventListener('mouseup', this.boundDragEnd);
                });
            }
        }

        async ensureStatus() {
            if (this.statusPromise) {
                return this.statusPromise;
            }

            this.statusPromise = this.getStatus()
                .then((status) => {
                    this.status = status;
                    this.syncStateFromStatus();
                    this.updateAvailability();
                    return this.loadModelCatalog().then(() => status);
                })
                .finally(() => {
                    this.statusPromise = null;
                });

            return this.statusPromise;
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
            } catch {
                return null;
            }
        }

        updateAvailability() {
            const enabled = this.status && (this.status.enabled ?? this.status.Enabled);
            const configured = this.status && (this.status.configured ?? this.status.Configured);
            const model = this.getDisplayedModel();
            this.available = !!(enabled && configured);

            if (!this.activeEditorId) {
                if (this.subtitle) {
                    this.subtitle.textContent = 'Scoped to the current editor region.';
                }
            } else {
                this.updateSubtitle(model);
            }

            if (this.input) {
                this.input.disabled = !this.available;
            }

            if (this.sendButton) {
                this.sendButton.disabled = !this.available;
            }

            this.actionButtons.forEach((button) => {
                button.disabled = !this.available;
            });

            this.updateModelPicker();

            if (!this.available) {
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
                    providerKey: 'ckeditor'
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
            const catalog = getAiModelCatalog();
            if (catalog) {
                return await catalog.saveSelectedModelPreference(selectedModel, {
                    context: {
                        editorKind: 'ckeditor',
                        documentKind: window.ccmsEditorContext && window.ccmsEditorContext.documentKind
                    }
                });
            }

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

        open(editor) {
            this.initialize();
            this.setActiveEditor(editor);

            if (!this.windowElement) {
                return;
            }

            this.windowElement.classList.remove('is-hidden');
            this.windowElement.classList.remove('is-minimized');
            this.hideStatus();

            if (this.input && !this.input.disabled) {
                this.input.focus();
            }

            this.ensureStatus();
        }

        setActiveEditor(editor) {
            const requiredMethods = ['renderMessages', 'captureEditorContext', 'buildPayload'];
            const missingMethods = requiredMethods.filter((methodName) => typeof this[methodName] !== 'function');
            if (missingMethods.length > 0) {
                const message = `CKEditor AI assistant is in an invalid state. Missing required method(s): ${missingMethods.join(', ')}`;
                this.showStatus(message);
                throw new Error(message);
            }

            this.activeEditor = editor || null;
            this.activeEditorId = this.getEditorId(editor);

            const session = this.getActiveSession();
            if (session && session.messages.length === 0) {
                session.messages.push(this.createMessage(
                    'system',
                    'AI suggestions stay scoped to this editor region. Ask for grammar fixes, stronger wording, rewrites, summaries, or fresh copy.',
                    { editorId: this.activeEditorId }
                ));
            }

            if (this.input) {
                this.input.value = '';
            }

            this.updateSubtitle(this.status && (this.status.model ?? this.status.Model));
            this.renderMessages();
        }

        getEditorId(editor) {
            return editor && editor.sourceElement
                ? editor.sourceElement.getAttribute('data-ccms-ceid')
                : null;
        }

        getActiveSession() {
            if (!this.activeEditorId) {
                return null;
            }

            if (!this.sessions.has(this.activeEditorId)) {
                this.sessions.set(this.activeEditorId, { messages: [] });
            }

            return this.sessions.get(this.activeEditorId);
        }

        updateSubtitle(modelName) {
            if (!this.subtitle) {
                return;
            }

            if (!this.activeEditorId) {
                this.subtitle.textContent = 'Scoped to the current editor region.';
                return;
            }

            const shortEditorId = this.activeEditorId.length > 8
                ? this.activeEditorId.substring(0, 8)
                : this.activeEditorId;
            const modelText = this.available && modelName ? ` via ${modelName}` : '';
            this.subtitle.textContent = `Scoped to region ${shortEditorId}${modelText}.`;
        }

        createMessage(role, content, context) {
            return {
                id: this.nextMessageId++,
                role: role,
                content: content,
                context: context || {}
            };
        }

        async sendChatMessage(action, message) {
            const trimmed = (message || '').trim();
            if (!trimmed || this.isSending) {
                return;
            }

            if (!this.activeEditor || !this.activeEditorId) {
                this.showStatus('Open the assistant from a CKEditor toolbar button first.');
                return;
            }

            await this.ensureStatus();
            if (!this.available) {
                this.showStatus('AI chat is unavailable for this tenant right now.');
                return;
            }

            const session = this.getActiveSession();
            if (!session) {
                return;
            }

            const context = this.captureEditorContext(this.activeEditor);
            const payload = this.buildPayload(action, trimmed, session, context);

            this.isSending = true;
            this.setBusy(true);
            this.hideStatus();

            let hadError = false;

            session.messages.push(this.createMessage('user', trimmed, { editorId: this.activeEditorId }));
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

                session.messages.push(this.createMessage('assistant', reply, {
                    editorId: this.activeEditorId,
                    action: action,
                    selectionSnapshot: context.selectionSnapshot,
                    caretSnapshot: context.caretSnapshot,
                    hasExpandedSelection: context.hasExpandedSelection,
                    contentFingerprint: context.contentFingerprint
                }));
                this.renderMessages();

                if (this.input) {
                    this.input.value = '';
                }
            } catch {
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

            if (this.input) {
                this.input.disabled = isBusy || !this.available;
            }

            this.actionButtons.forEach((button) => {
                button.disabled = isBusy || !this.available;
            });

            if (isBusy) {
                this.showThinking();
            } else {
                this.hideThinking();
            }
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

        buildPayload(action, message, session, context) {
            const editorContext = window.ccmsEditorContext || {};
            const selectedModel = this.getSelectedModel();

            return {
                editorKind: editorContext.editorSurface || 'ckeditor',
                action: action,
                message: message,
                selection: context.selectionSnapshot || null,
                currentCode: context.currentHtml || null,
                language: 'html',
                fieldName: context.fieldName || null,
                title: editorContext.title || null,
                articleNumber: editorContext.articleNumber || null,
                documentKind: editorContext.documentKind || null,
                sectionKind: context.sectionKind || editorContext.sectionKind || null,
                articleType: editorContext.articleType || null,
                category: editorContext.category || null,
                urlPath: editorContext.urlPath || null,
                selectedModel: selectedModel,
                messages: session.messages.slice(-8).map((entry) => ({
                    role: entry.role,
                    content: entry.content
                }))
            };
        }

        captureEditorContext(editor) {
            const editorContext = window.ccmsEditorContext || {};
            const sourceElement = editor && editor.sourceElement ? editor.sourceElement : null;
            const selection = editor && editor.model && editor.model.document
                ? editor.model.document.selection
                : null;

            let selectedHtml = null;
            if (editor && selection && !selection.isCollapsed) {
                try {
                    editor.model.change((writer) => {
                        const selectedContent = editor.model.getSelectedContent(selection);
                        const viewFragment = editor.data.toView(selectedContent);
                        selectedHtml = editor.data.processor.toData(viewFragment);
                    });
                } catch {
                    selectedHtml = null;
                }
            }

            const currentHtml = editor && typeof editor.getData === 'function'
                ? editor.getData()
                : null;

            return {
                fieldName: sourceElement ? sourceElement.getAttribute('name') : null,
                sectionKind: sourceElement ? sourceElement.getAttribute('data-ccms-section-kind') : null,
                selectionSnapshot: selectedHtml,
                caretSnapshot: null,
                hasExpandedSelection: !!(selection && !selection.isCollapsed),
                currentHtml: currentHtml,
                contentFingerprint: this.hashText(currentHtml || ''),
                documentKind: editorContext.documentKind || null
            };
        }

        renderMessages() {
            if (!this.messagesContainer) {
                return;
            }

            const session = this.getActiveSession();
            const messages = session ? session.messages : [];
            this.messagesContainer.innerHTML = '';

            if (!messages.length) {
                const empty = document.createElement('div');
                empty.className = 'copilot-chat-empty';
                empty.textContent = 'Open the assistant from a CKEditor toolbar button to start a region-scoped chat.';
                this.messagesContainer.appendChild(empty);
                return;
            }

            messages.forEach((entry) => {
                const item = document.createElement('div');
                item.className = `copilot-chat-message ${entry.role}`;

                const content = document.createElement('div');
                content.className = 'copilot-chat-message-content';
                content.textContent = entry.content;
                item.appendChild(content);

                if (entry.role === 'assistant') {
                    const suggestedHtml = this.extractHtmlFromReply(entry.content);
                    if (suggestedHtml) {
                        const actions = document.createElement('div');
                        actions.className = 'copilot-chat-message-actions mt-2 d-flex gap-2 flex-wrap';

                        const replaceSelection = document.createElement('button');
                        replaceSelection.type = 'button';
                        replaceSelection.className = 'btn btn-sm btn-outline-light';
                        replaceSelection.textContent = 'Apply to selection';
                        replaceSelection.setAttribute('data-message-id', String(entry.id));
                        replaceSelection.setAttribute('data-apply-mode', 'replace-selection');
                        actions.appendChild(replaceSelection);

                        const replaceBlock = document.createElement('button');
                        replaceBlock.type = 'button';
                        replaceBlock.className = 'btn btn-sm btn-outline-light';
                        replaceBlock.textContent = 'Replace region';
                        replaceBlock.setAttribute('data-message-id', String(entry.id));
                        replaceBlock.setAttribute('data-apply-mode', 'replace-block');
                        actions.appendChild(replaceBlock);

                        item.appendChild(actions);
                    }
                }

                this.messagesContainer.appendChild(item);
            });

            this.messagesContainer.scrollTop = this.messagesContainer.scrollHeight;
        }

        extractHtmlFromReply(reply) {
            if (!reply || typeof reply !== 'string') {
                return null;
            }

            const htmlFenceMatch = reply.match(/```html\s*([\s\S]*?)```/i);
            if (htmlFenceMatch && htmlFenceMatch[1]) {
                return htmlFenceMatch[1].trim();
            }

            const genericFenceMatch = reply.match(/```\s*([\s\S]*?)```/);
            if (genericFenceMatch && genericFenceMatch[1] && /<[^>]+>/.test(genericFenceMatch[1])) {
                return genericFenceMatch[1].trim();
            }

            if (/<[^>]+>/.test(reply)) {
                return reply.trim();
            }

            return null;
        }

        applySuggestion(messageId, mode) {
            const session = this.getActiveSession();
            if (!session || !this.activeEditor) {
                this.showStatus('No active editor context is available for applying suggestions.');
                return;
            }

            const entry = session.messages.find((message) => message.id === messageId && message.role === 'assistant');
            if (!entry) {
                this.showStatus('The selected AI response is no longer available.');
                return;
            }

            const html = this.extractHtmlFromReply(entry.content);
            if (!html) {
                this.showStatus('This AI response does not contain HTML that can be applied.');
                return;
            }

            try {
                if (mode === 'replace-block') {
                    this.activeEditor.setData(html);
                    this.hideStatus();
                    return;
                }

                const selection = this.activeEditor.model.document.selection;
                if (!selection || selection.isCollapsed) {
                    this.showStatus('Select content in the editor before applying to selection.');
                    return;
                }

                const viewFragment = this.activeEditor.data.processor.toView(html);
                const modelFragment = this.activeEditor.data.toModel(viewFragment);
                this.activeEditor.model.change((writer) => {
                    this.activeEditor.model.insertContent(modelFragment, selection);
                });
                this.hideStatus();
            } catch {
                this.showStatus('Could not apply the AI suggestion to the editor content.');
            }
        }

        handleDragMove(event) {
            if (!this.dragState || !this.windowElement) {
                return;
            }

            const maxLeft = Math.max(12, window.innerWidth - this.windowElement.offsetWidth - 12);
            const maxTop = Math.max(72, window.innerHeight - this.windowElement.offsetHeight - 12);
            const left = Math.min(Math.max(12, event.clientX - this.dragState.offsetX), maxLeft);
            const top = Math.min(Math.max(72, event.clientY - this.dragState.offsetY), maxTop);

            this.windowElement.style.left = `${left}px`;
            this.windowElement.style.top = `${top}px`;
        }

        stopDrag() {
            this.dragState = null;
            document.removeEventListener('mousemove', this.boundDragMove);
            document.removeEventListener('mouseup', this.boundDragEnd);
        }

        hashText(text) {
            const source = text || '';
            let hash = 0;
            for (let index = 0; index < source.length; index += 1) {
                hash = ((hash << 5) - hash) + source.charCodeAt(index);
                hash |= 0;
            }

            return hash;
        }
    }

    window.ckeditorCopilotChat = new CkeditorCopilotChat();
})();