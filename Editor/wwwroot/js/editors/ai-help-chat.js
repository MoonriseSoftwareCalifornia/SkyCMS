(function () {
    class AiHelpChat {
        constructor() {
            this.messages = [];
            this.isSending = false;
            this.status = null;
            this.catalog = null;
            this.selectedModel = null;
            this.currentMode = 'general-help';
            this.launchModeStorageKey = 'skycms.aihelp.launchMode';
            this.context = this.readContextFromQuery();
            this.launchMode = this.resolveLaunchMode();
        }

        readContextFromQuery() {
            const params = new URLSearchParams(window.location.search || '');
            return {
                documentKind: params.get('documentKind'),
                sectionKind: params.get('sectionKind'),
                articleNumber: params.get('articleNumber'),
                title: params.get('title'),
                urlPath: params.get('urlPath'),
                popup: params.get('popup') === '1',
                launchMode: params.get('launchMode')
            };
        }

        resolveLaunchMode() {
            const fromQuery = (this.context.launchMode || '').toLowerCase();
            if (fromQuery === 'dock' || fromQuery === 'detached') {
                this.saveLaunchMode(fromQuery);
                return fromQuery;
            }

            try {
                const stored = (window.localStorage.getItem(this.launchModeStorageKey) || '').toLowerCase();
                if (stored === 'dock' || stored === 'detached') {
                    return stored;
                }
            } catch {
            }

            return 'dock';
        }

        saveLaunchMode(mode) {
            if (mode !== 'dock' && mode !== 'detached') {
                return;
            }

            this.launchMode = mode;
            try {
                window.localStorage.setItem(this.launchModeStorageKey, mode);
            } catch {
            }
        }

        initialize() {
            this.cacheElements();
            this.initializeLaunchModeSelector();
            this.attachListeners();
            this.initializeAsync();
        }

        initializeLaunchModeSelector() {
            if (this.launchModeSelect) {
                this.launchModeSelect.value = this.launchMode;
            }
        }

        async initializeAsync() {
            if (this.launchMode === 'detached' && !this.context.popup) {
                const popup = this.openDetachedWindow(false);
                if (popup) {
                    this.showStatus('Opening AI Help in a detached window based on your launch preference.');
                    return;
                }

                this.showStatus('Detached mode is enabled, but a new window was blocked. Continuing docked.');
            }

            this.status = await this.getStatus();
            this.updateAvailability();
            await this.loadModelCatalog();
            this.renderMessages();
        }

        cacheElements() {
            this.subtitle = document.getElementById('aiHelpChatSubtitle');
            this.messagesContainer = document.getElementById('aiHelpChatMessages');
            this.statusContainer = document.getElementById('aiHelpChatStatus');
            this.form = document.getElementById('aiHelpChatForm');
            this.input = document.getElementById('aiHelpChatInput');
            this.sendButton = document.getElementById('btnAiHelpSend');
            this.clearButton = document.getElementById('btnAiHelpClearChat');
            this.modelSelect = document.getElementById('aiHelpModelSelect');
            this.modelHelp = document.getElementById('aiHelpModelHelp');
            this.refreshModelsButton = document.getElementById('btnAiHelpRefreshModels');
            this.detachButton = document.getElementById('btnAiHelpDetach');
            this.launchModeSelect = document.getElementById('aiHelpLaunchMode');
            this.actionButtons = Array.from(document.querySelectorAll('[data-ai-help-action]'));
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

            if (this.launchModeSelect) {
                this.launchModeSelect.addEventListener('change', () => {
                    const next = this.launchModeSelect.value === 'detached' ? 'detached' : 'dock';
                    this.saveLaunchMode(next);
                });
            }

            if (this.modelSelect) {
                this.modelSelect.addEventListener('change', async () => {
                    const previous = this.selectedModel;
                    this.selectedModel = this.modelSelect.value || null;
                    const saved = await this.saveSelectedModelPreference(this.selectedModel);
                    if (!saved) {
                        this.selectedModel = previous;
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
                    this.currentMode = button.getAttribute('data-ai-help-mode') || 'general-help';
                    const action = button.getAttribute('data-ai-help-action') || 'chat';
                    const inputText = this.input ? this.input.value.trim() : '';
                    const message = inputText || this.getActionPrompt(action);
                    await this.sendChatMessage(action, message);
                });
            });

            if (this.detachButton) {
                this.detachButton.addEventListener('click', () => {
                    this.saveLaunchMode('detached');
                    if (this.launchModeSelect) {
                        this.launchModeSelect.value = 'detached';
                    }

                    const popup = this.openDetachedWindow(true);
                    if (!popup) {
                        this.showStatus('Unable to open a detached window. Please allow pop-ups for this site.');
                    }
                });
            }
        }

        openDetachedWindow(focusWindow) {
            const popupUrl = this.buildHelpWindowUrl(true);
            const popup = window.open(popupUrl, 'SkyCmsAiHelp', 'popup=yes,width=1100,height=820,scrollbars=yes,resizable=yes');
            if (popup && focusWindow) {
                popup.focus();
            }

            return popup;
        }

        getActionPrompt(action) {
            switch (action) {
                case 'skycms-help':
                    return 'Explain how to do this in SkyCMS and call out common pitfalls.';
                case 'website-help':
                    return 'Give practical guidance for website development and content quality.';
                case 'site-help':
                    return 'Answer this question using the current site context if possible.';
                default:
                    return 'Help me with SkyCMS or website development.';
            }
        }

        getContextQueryString() {
            const params = new URLSearchParams();
            params.set('editorKind', 'help');
            params.set('documentKind', this.context.documentKind || this.currentMode);
            return params.toString();
        }

        buildHelpWindowUrl(forcePopup) {
            const params = new URLSearchParams();
            if (forcePopup) {
                params.set('popup', '1');
            }

            params.set('launchMode', this.launchMode);

            if (this.context.documentKind) {
                params.set('documentKind', this.context.documentKind);
            }

            if (this.context.sectionKind) {
                params.set('sectionKind', this.context.sectionKind);
            }

            if (this.context.articleNumber) {
                params.set('articleNumber', this.context.articleNumber);
            }

            if (this.context.title) {
                params.set('title', this.context.title);
            }

            if (this.context.urlPath) {
                params.set('urlPath', this.context.urlPath);
            }

            const query = params.toString();
            return `/Editor/AiHelp${query ? `?${query}` : ''}`;
        }

        async getStatus() {
            try {
                const query = this.getContextQueryString();
                const response = await fetch(`/api/ai-proxy/status?${query}`, {
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

        async loadModelCatalog(forceRefresh) {
            try {
                const query = new URLSearchParams(this.getContextQueryString());
                if (forceRefresh) {
                    query.set('forceRefresh', 'true');
                }

                const response = await fetch(`/api/ai-proxy/models?${query.toString()}`, {
                    method: 'GET',
                    credentials: 'same-origin',
                    headers: {
                        Accept: 'application/json'
                    }
                });

                if (!response.ok) {
                    this.catalog = null;
                    this.updateModelPicker();
                    return;
                }

                this.catalog = await response.json();
                this.selectedModel = this.catalog.selectedModel ?? this.catalog.SelectedModel ?? null;
                this.updateModelPicker();
            } catch {
                this.catalog = null;
                this.updateModelPicker();
            }
        }

        updateModelPicker() {
            if (!this.modelSelect) {
                return;
            }

            const models = this.catalog && (this.catalog.models ?? this.catalog.Models)
                ? (this.catalog.models ?? this.catalog.Models)
                : [];
            const supportsSelection = !!(this.catalog && (this.catalog.supportsUserModelSelection ?? this.catalog.SupportsUserModelSelection));

            this.modelSelect.innerHTML = '<option value="">Default</option>';
            models.forEach((model) => {
                const option = document.createElement('option');
                option.value = model.id ?? model.Id ?? '';
                option.textContent = model.displayName ?? model.DisplayName ?? option.value;
                this.modelSelect.appendChild(option);
            });

            this.modelSelect.disabled = !supportsSelection;
            this.modelSelect.value = this.selectedModel || '';

            if (this.modelHelp) {
                const provider = this.catalog && (this.catalog.providerDisplayName ?? this.catalog.ProviderDisplayName)
                    ? (this.catalog.providerDisplayName ?? this.catalog.ProviderDisplayName)
                    : 'AI provider';
                this.modelHelp.textContent = supportsSelection
                    ? `Model selection is enabled for ${provider}.`
                    : `Model selection is managed by ${provider}.`;
            }
        }

        updateAvailability() {
            const enabled = this.status && (this.status.enabled ?? this.status.Enabled);
            const configured = this.status && (this.status.configured ?? this.status.Configured);
            const available = !!(enabled && configured);

            if (this.subtitle) {
                this.subtitle.textContent = available
                    ? 'Ask about SkyCMS, site behavior, or website best practices.'
                    : 'AI help is unavailable until the AI proxy is configured.';
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
        }

        createMessage(role, content) {
            return {
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
                empty.textContent = 'Ask your first question to start the help chat.';
                this.messagesContainer.appendChild(empty);
                return;
            }

            this.messages.forEach((entry) => {
                const item = document.createElement('div');
                item.className = `copilot-chat-message ${entry.role}`;

                const content = document.createElement('div');
                content.className = 'copilot-chat-message-content';
                content.textContent = entry.content;
                item.appendChild(content);

                this.messagesContainer.appendChild(item);
            });

            this.messagesContainer.scrollTop = this.messagesContainer.scrollHeight;
        }

        showStatus(message) {
            if (!this.statusContainer) {
                return;
            }

            this.statusContainer.style.display = 'block';
            this.statusContainer.textContent = message;
        }

        hideStatus() {
            if (!this.statusContainer) {
                return;
            }

            this.statusContainer.style.display = 'none';
            this.statusContainer.textContent = '';
        }

        setBusy(isBusy) {
            this.isSending = isBusy;
            if (this.sendButton) {
                this.sendButton.disabled = isBusy;
            }
        }

        async saveSelectedModelPreference(selectedModel) {
            try {
                const response = await fetch('/api/ai-proxy/preferences/model', {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: {
                        'Content-Type': 'application/json',
                        Accept: 'application/json'
                    },
                    body: JSON.stringify({
                        editorKind: 'help',
                        documentKind: this.currentMode,
                        selectedModel: selectedModel
                    })
                });

                return response.ok;
            } catch {
                return false;
            }
        }

        async sendChatMessage(action, message) {
            const trimmed = (message || '').trim();
            if (!trimmed || this.isSending) {
                return;
            }

            this.setBusy(true);
            this.hideStatus();
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
                    body: JSON.stringify({
                        editorKind: 'help',
                        chatMode: this.currentMode,
                        action: action,
                        message: trimmed,
                        title: this.context.title,
                        articleNumber: this.context.articleNumber,
                        documentKind: this.context.documentKind,
                        sectionKind: this.context.sectionKind,
                        urlPath: this.context.urlPath,
                        selectedModel: this.selectedModel,
                        messages: this.messages.slice(-8).map((entry) => ({
                            role: entry.role,
                            content: entry.content
                        }))
                    })
                });

                const data = await response.json().catch(() => null);

                if (response.status === 429) {
                    const retryAfterSeconds = data && (data.retryAfterSeconds ?? data.RetryAfterSeconds)
                        ? (data.retryAfterSeconds ?? data.RetryAfterSeconds)
                        : 5;
                    this.showStatus(`AI help is rate-limited. Try again in about ${retryAfterSeconds} seconds.`);
                    return;
                }

                if (!response.ok) {
                    const errorMessage = data && (data.error ?? data.Error)
                        ? (data.error ?? data.Error)
                        : 'The AI help request failed.';
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
            } catch {
                this.showStatus('The AI help request failed before a response was returned.');
            } finally {
                this.setBusy(false);
            }
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        const chat = new AiHelpChat();
        chat.initialize();
    });
})();