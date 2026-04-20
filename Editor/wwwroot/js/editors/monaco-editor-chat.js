(function () {
    class MonacoCopilotChat {
        constructor() {
            this.initialized = false;
            this.core = null;
            this.messages = [];
            this.isSending = false;
            this.status = null;
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
            this.updateAvailability();
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
            try {
                const response = await fetch('/api/copilot/status', {
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
            const model = this.status && (this.status.model ?? this.status.Model);
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

            if (!available) {
                this.showStatus('AI chat is unavailable for this tenant right now.');
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

            this.messages.push(this.createMessage('user', trimmed));
            this.renderMessages();

            try {
                const response = await fetch('/api/copilot/chat', {
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
                    const retryAfterSeconds = data && (data.retryAfterSeconds ?? data.RetryAfterSeconds)
                        ? (data.retryAfterSeconds ?? data.RetryAfterSeconds)
                        : 5;
                    this.showStatus(`AI chat is rate-limited. Try again in about ${retryAfterSeconds} seconds.`);
                    return;
                }

                if (!response.ok) {
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
                this.showStatus('The AI chat request failed before a response was returned.');
            } finally {
                this.isSending = false;
                this.setBusy(false);
            }
        }

        buildPayload(action, message) {
            const editor = this.core && this.core.editor ? this.core.editor : null;
            const model = editor && typeof editor.getModel === 'function' ? editor.getModel() : null;
            const selection = editor && typeof editor.getSelection === 'function' ? editor.getSelection() : null;

            let selectedText = '';
            if (model && selection && typeof selection.isEmpty === 'function' && !selection.isEmpty()) {
                selectedText = model.getValueInRange(selection) || '';
            }

            const currentCode = model && typeof model.getValue === 'function'
                ? model.getValue()
                : '';

            const recentMessages = this.messages.slice(-8).map((entry) => ({
                role: entry.role,
                content: entry.content
            }));

            const ctx = window.ccmsEditorContext || {};
            const activeFieldName = this.core && this.core.currentField ? this.core.currentField.FieldName : null;
            const sectionKind = (ctx.sectionKindMap && activeFieldName && ctx.sectionKindMap[activeFieldName])
                || ctx.sectionKind
                || null;

            return {
                action: action,
                message: message,
                selection: selectedText,
                currentCode: currentCode,
                language: model && typeof model.getLanguageId === 'function' ? model.getLanguageId() : null,
                fieldName: activeFieldName,
                title: this.getInputValue('Title'),
                articleNumber: this.getInputValue('ArticleNumber'),
                messages: recentMessages,
                documentKind: ctx.documentKind || null,
                sectionKind: sectionKind,
                articleType: ctx.articleType || null,
                category: ctx.category || null,
                urlPath: ctx.urlPath || null,
                templateId: ctx.templateId || null,
                layoutId: ctx.layoutId || null
            };
        }

        getInputValue(id) {
            const element = document.getElementById(id);
            if (!element) {
                return null;
            }

            if (typeof element.value === 'string') {
                return element.value;
            }

            return element.textContent || null;
        }

        createMessage(role, content) {
            return {
                id: this.nextMessageId++,
                role: role,
                content: content,
                appliedInPlace: false,
                appendedToEnd: false
            };
        }

        extractSuggestedText(content) {
            const text = (content || '').trim();
            if (!text) {
                return '';
            }

            const codeBlocks = [];
            const regex = /```(?:[a-zA-Z0-9#+._-]+)?\s*\n([\s\S]*?)```/g;
            let match;
            while ((match = regex.exec(text)) !== null) {
                const block = (match[1] || '').trim();
                if (block) {
                    codeBlocks.push(block);
                }
            }

            if (codeBlocks.length > 0) {
                return codeBlocks.join('\n\n');
            }

            return text;
        }

        applySuggestion(messageId) {
            const messageIndex = this.messages.findIndex((m) => m.id === messageId);
            if (messageIndex < 0) {
                this.showStatus('Could not find that AI suggestion.');
                return;
            }

            const message = this.messages[messageIndex];
            if (!message || message.role !== 'assistant') {
                this.showStatus('Only assistant suggestions can be applied.');
                return;
            }

            const editor = this.core && this.core.editor ? this.core.editor : null;
            if (!editor || typeof editor.getModel !== 'function') {
                this.showStatus('Editor is not ready yet.');
                return;
            }

            const model = editor.getModel();
            if (!model) {
                this.showStatus('Editor model is not available.');
                return;
            }

            const suggestedText = this.extractSuggestedText(message.content);
            if (!suggestedText) {
                this.showStatus('No editable suggestion content was found.');
                return;
            }

            if (!this.confirmLargeInsert(suggestedText, 'apply this suggestion in place')) {
                this.showStatus('Apply canceled.');
                return;
            }

            const selection = typeof editor.getSelection === 'function' ? editor.getSelection() : null;
            let range = selection;
            let actionSummary = 'inserted at the cursor';

            if (!range || (typeof range.isEmpty === 'function' && range.isEmpty())) {
                const position = typeof editor.getPosition === 'function' ? editor.getPosition() : null;
                if (!position) {
                    this.showStatus('Could not determine where to apply the suggestion.');
                    return;
                }

                range = new window.monaco.Range(
                    position.lineNumber,
                    position.column,
                    position.lineNumber,
                    position.column);
            } else {
                actionSummary = 'replaced the selected text';
            }

            editor.pushUndoStop();
            editor.executeEdits('copilot-chat-apply', [{
                range: range,
                text: suggestedText,
                forceMoveMarkers: true
            }]);
            editor.pushUndoStop();

            message.appliedInPlace = true;
            this.renderMessages();
            this.showStatus(`Applied suggestion: ${actionSummary}.`);

            if (typeof editor.focus === 'function') {
                editor.focus();
            }
        }

        appendSuggestionAtEnd(messageId) {
            const messageIndex = this.messages.findIndex((m) => m.id === messageId);
            if (messageIndex < 0) {
                this.showStatus('Could not find that AI suggestion.');
                return;
            }

            const message = this.messages[messageIndex];
            if (!message || message.role !== 'assistant') {
                this.showStatus('Only assistant suggestions can be appended.');
                return;
            }

            const editor = this.core && this.core.editor ? this.core.editor : null;
            if (!editor || typeof editor.getModel !== 'function') {
                this.showStatus('Editor is not ready yet.');
                return;
            }

            const model = editor.getModel();
            if (!model) {
                this.showStatus('Editor model is not available.');
                return;
            }

            const suggestedText = this.extractSuggestedText(message.content);
            if (!suggestedText) {
                this.showStatus('No editable suggestion content was found.');
                return;
            }

            if (!this.confirmLargeInsert(suggestedText, 'append this suggestion at the end of the file')) {
                this.showStatus('Append canceled.');
                return;
            }

            const endLine = model.getLineCount();
            const endColumn = model.getLineMaxColumn(endLine);
            const endRange = new window.monaco.Range(endLine, endColumn, endLine, endColumn);

            let textToInsert = suggestedText;
            const currentContent = model.getValue();
            if (currentContent && currentContent.trim().length > 0) {
                textToInsert = `\n\n${suggestedText}`;
            }

            editor.pushUndoStop();
            editor.executeEdits('copilot-chat-append', [{
                range: endRange,
                text: textToInsert,
                forceMoveMarkers: true
            }]);
            editor.pushUndoStop();

            message.appendedToEnd = true;
            this.renderMessages();
            this.showStatus('Applied suggestion: appended to the end of the file.');

            if (typeof editor.focus === 'function') {
                editor.focus();
            }
        }

        confirmLargeInsert(text, actionDescription) {
            const characterCount = (text || '').length;
            if (characterCount <= this.largeInsertThreshold) {
                return true;
            }

            const lineCount = text.split(/\r\n|\r|\n/).length;
            return window.confirm(
                `This AI suggestion is large (${lineCount} lines, ${characterCount} characters).\n\nDo you want to ${actionDescription}?`);
        }

        renderMessages() {
            if (!this.messagesContainer) {
                return;
            }

            this.messagesContainer.innerHTML = '';
            if (this.messages.length === 0) {
                const empty = document.createElement('div');
                empty.className = 'copilot-chat-empty';
                empty.textContent = 'Use the actions above or ask a question about the current file.';
                this.messagesContainer.appendChild(empty);
                return;
            }

            this.messages.forEach((message) => {
                const node = document.createElement('div');
                node.className = `copilot-chat-message ${message.role}`;

                const content = document.createElement('div');
                content.className = 'copilot-chat-message-content';
                content.textContent = message.content;
                node.appendChild(content);

                if (message.role === 'assistant') {
                    const actions = document.createElement('div');
                    actions.className = 'copilot-chat-message-actions';

                    const applyButton = document.createElement('button');
                    applyButton.type = 'button';
                    applyButton.className = 'btn btn-sm btn-outline-info';
                    applyButton.textContent = message.appliedInPlace ? 'Applied In Place' : 'Apply To Editor';
                    applyButton.disabled = !!message.appliedInPlace;
                    applyButton.addEventListener('click', () => {
                        this.applySuggestion(message.id);
                    });

                    const appendButton = document.createElement('button');
                    appendButton.type = 'button';
                    appendButton.className = 'btn btn-sm btn-outline-success';
                    appendButton.textContent = message.appendedToEnd ? 'Appended At End' : 'Append At End';
                    appendButton.disabled = !!message.appendedToEnd;
                    appendButton.addEventListener('click', () => {
                        this.appendSuggestionAtEnd(message.id);
                    });

                    actions.appendChild(applyButton);
                    actions.appendChild(appendButton);
                    node.appendChild(actions);
                }

                this.messagesContainer.appendChild(node);
            });

            this.messagesContainer.scrollTop = this.messagesContainer.scrollHeight;
        }

        showStatus(message) {
            if (!this.statusContainer) {
                return;
            }

            this.statusContainer.textContent = message;
            this.statusContainer.style.display = '';
        }

        hideStatus() {
            if (!this.statusContainer) {
                return;
            }

            this.statusContainer.textContent = '';
            this.statusContainer.style.display = 'none';
        }

        setBusy(isBusy) {
            if (this.sendButton) {
                this.sendButton.disabled = isBusy || (this.input && this.input.disabled);
                this.sendButton.textContent = isBusy ? 'Sending...' : 'Send';
            }

            this.actionButtons.forEach((button) => {
                button.disabled = isBusy || !this.available;
            });

            if (this.input) {
                this.input.readOnly = isBusy;
            }
        }
    }

    window.monacoCopilotChat = new MonacoCopilotChat();
})();