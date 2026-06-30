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

    class AiHelpChat {
        constructor() {
            this.messages = [];
            this.isSending = false;
            this.status = null;
            this.catalog = null;
            this.selectedModel = null;
            this.currentMode = 'general-help';
            this.available = false;
            this.context = this.readContextFromQuery();
        }

        readContextFromQuery() {
            const params = new URLSearchParams(window.location.search || '');
            return {
                documentKind: params.get('documentKind'),
                sectionKind: params.get('sectionKind'),
                articleNumber: params.get('articleNumber'),
                title: params.get('title'),
                urlPath: params.get('urlPath'),
                popup: params.get('popup') === '1'
            };
        }

        initialize() {
            this.cacheElements();
            this.attachListeners();
            this.initializeAsync();
        }

        async initializeAsync() {
            const catalog = getAiModelCatalog();
            this.status = catalog ? await catalog.getStatus(this.getCatalogContext()) : await this.getStatus();
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

        getCatalogContext() {
            return {
                editorKind: 'help',
                documentKind: this.context.documentKind || this.currentMode
            };
        }

        getContextQueryString() {
            const catalog = getAiModelCatalog();
            return catalog
                ? catalog.buildPreferenceQueryString(this.getCatalogContext())
                : new URLSearchParams({ editorKind: 'help', documentKind: this.context.documentKind || this.currentMode }).toString();
        }

        async getStatus() {
            const catalog = getAiModelCatalog();
            if (catalog) {
                return await catalog.getStatus(this.getCatalogContext());
            }

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
            const catalog = getAiModelCatalog();
            if (catalog) {
                this.catalog = await catalog.getCatalog({
                    context: this.getCatalogContext(),
                    forceRefresh: !!forceRefresh,
                    providerKey: 'help'
                });
                this.selectedModel = catalog.getSelectedModel();
                this.updateModelPicker();
                return;
            }

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
            this.available = available;

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
            this.statusContainer.style.display = 'block';
            this.statusContainer.textContent = message;
        }

        hideStatus() {
            if (!this.statusContainer) {
                return;
            }

            this.statusContainer.classList.remove('is-thinking');
            this.statusContainer.style.display = 'none';
            this.statusContainer.textContent = '';
        }

        showThinking() {
            if (!this.statusContainer) {
                return;
            }

            this.statusContainer.classList.add('is-thinking');
            this.statusContainer.style.display = 'block';
            this.statusContainer.textContent = 'Thinking...';
        }

        hideThinking() {
            if (!this.statusContainer || !this.statusContainer.classList.contains('is-thinking')) {
                return;
            }

            this.hideStatus();
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
            this.isSending = isBusy;
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

        async saveSelectedModelPreference(selectedModel) {
            const catalog = getAiModelCatalog();
            if (catalog) {
                return await catalog.saveSelectedModelPreference(selectedModel, {
                    context: this.getCatalogContext()
                });
            }

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

            let hadError = false;

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
                    hadError = true;
                    const retryAfterSeconds = data && (data.retryAfterSeconds ?? data.RetryAfterSeconds)
                        ? (data.retryAfterSeconds ?? data.RetryAfterSeconds)
                        : 5;
                    this.showStatus(`AI help is rate-limited. Try again in about ${retryAfterSeconds} seconds.`);
                    return;
                }

                if (!response.ok) {
                    hadError = true;
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
                hadError = true;
                this.showStatus('The AI help request failed before a response was returned.');
            } finally {
                if (hadError) {
                    await this.flashErrorOnSendButton();
                }

                this.setBusy(false);
            }
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        const chat = new AiHelpChat();
        chat.initialize();
    });
})();