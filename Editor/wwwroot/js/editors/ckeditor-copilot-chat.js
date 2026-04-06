(function () {
    class CkeditorCopilotChat {
        constructor() {
            this.initialized = false;
            this.status = null;
            this.statusPromise = null;
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
                    this.updateAvailability();
                    return status;
                })
                .finally(() => {
                    this.statusPromise = null;
                });

            return this.statusPromise;
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
            } catch {
                return null;
            }
        }

        updateAvailability() {
            const enabled = this.status && (this.status.enabled ?? this.status.Enabled);
            const configured = this.status && (this.status.configured ?? this.status.Configured);
            const model = this.status && (this.status.model ?? this.status.Model);
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

            if (!this.available) {
                this.showStatus('AI chat is unavailable for this tenant right now.');
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

            session.messages.push(this.createMessage('user', trimmed, { editorId: this.activeEditorId }));
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
                this.showStatus('The AI chat request failed before a response was returned.');
            } finally {
                this.isSending = false;
                this.setBusy(false);
            }
        }

        buildPayload(action, message, session, context) {
            const recentMessages = session.messages
                .filter((entry) => entry.role === 'user' || entry.role === 'assistant')
                .slice(-8)
                .map((entry) => ({
                    role: entry.role,
                    content: entry.content
                }));

            return {
                editorKind: 'ckeditor',
                action: action,
                message: message,
                selection: context.selectedHtml,
                currentCode: context.currentHtml,
                language: 'html',
                fieldName: this.getFieldName(this.activeEditor),
                title: this.getInputValue('Title'),
                articleNumber: this.getInputValue('ArticleNumber'),
                messages: recentMessages
            };
        }

        getFieldName(editor) {
            if (!editor || !editor.sourceElement) {
                return null;
            }

            return editor.sourceElement.getAttribute('data-field-name')
                || editor.sourceElement.getAttribute('data-editor-config')
                || editor.sourceElement.tagName
                || null;
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

        captureEditorContext(editor) {
            const selection = editor.model.document.selection;
            const hasExpandedSelection = !selection.isCollapsed;
            const selectionSnapshot = this.serializeSelection(selection);
            const caretSnapshot = this.collapseSelectionSnapshot(selectionSnapshot, 'end');

            let selectedHtml = '';
            if (hasExpandedSelection) {
                const selectedFragment = editor.model.getSelectedContent(selection);
                selectedHtml = editor.data.stringify(selectedFragment);
            }

            const currentHtml = editor.getData();

            return {
                selectedHtml: selectedHtml,
                currentHtml: currentHtml,
                selectionSnapshot: selectionSnapshot,
                caretSnapshot: caretSnapshot,
                hasExpandedSelection: hasExpandedSelection,
                contentFingerprint: this.hashText(currentHtml)
            };
        }

        serializeSelection(selection) {
            return {
                isBackward: !!selection.isBackward,
                ranges: Array.from(selection.getRanges()).map((range) => ({
                    start: this.serializePosition(range.start),
                    end: this.serializePosition(range.end)
                }))
            };
        }

        serializePosition(position) {
            return {
                root: position.root.rootName,
                path: Array.from(position.path),
                stickiness: position.stickiness || 'toNone'
            };
        }

        collapseSelectionSnapshot(snapshot, edge) {
            if (!snapshot || !Array.isArray(snapshot.ranges) || snapshot.ranges.length === 0) {
                return null;
            }

            return {
                isBackward: false,
                ranges: snapshot.ranges.map((range) => {
                    const point = edge === 'start' ? range.start : range.end;
                    return {
                        start: { ...point, path: Array.from(point.path) },
                        end: { ...point, path: Array.from(point.path) }
                    };
                })
            };
        }

        renderMessages() {
            if (!this.messagesContainer) {
                return;
            }

            const session = this.getActiveSession();
            const messages = session ? session.messages : [];

            this.messagesContainer.innerHTML = '';

            if (!messages || messages.length === 0) {
                const empty = document.createElement('div');
                empty.className = 'copilot-chat-empty';
                empty.textContent = this.activeEditorId
                    ? 'Ask AI to improve this region. Suggestions will stay scoped to the selected editor instance.'
                    : 'Open the assistant from a CKEditor toolbar button to start a region-scoped chat.';
                this.messagesContainer.appendChild(empty);
                return;
            }

            messages.forEach((message) => {
                const wrapper = document.createElement('div');
                wrapper.className = `copilot-chat-message ${message.role}`;

                const content = document.createElement('div');
                content.className = 'copilot-chat-message-content';
                content.textContent = message.content;
                wrapper.appendChild(content);

                if (message.role === 'assistant') {
                    const actions = document.createElement('div');
                    actions.className = 'copilot-chat-message-actions';

                    actions.appendChild(this.createApplyButton(message, 'replaceSelection', 'Replace selection', !message.context.hasExpandedSelection));
                    actions.appendChild(this.createApplyButton(message, 'insertAtCursor', 'Insert at cursor', false));
                    actions.appendChild(this.createApplyButton(message, 'replaceBlock', 'Replace block', false));

                    wrapper.appendChild(actions);
                }

                this.messagesContainer.appendChild(wrapper);
            });

            this.messagesContainer.scrollTop = this.messagesContainer.scrollHeight;
        }

        createApplyButton(message, mode, label, disabled) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'btn btn-sm btn-outline-light';
            button.textContent = label;
            button.setAttribute('data-message-id', String(message.id));
            button.setAttribute('data-apply-mode', mode);
            button.disabled = !!disabled;
            return button;
        }

        extractSuggestedHtml(content) {
            const text = (content || '').trim();
            if (!text) {
                return '';
            }

            const htmlMatch = text.match(/```html\s*([\s\S]*?)```/i);
            if (htmlMatch && htmlMatch[1]) {
                return htmlMatch[1].trim();
            }

            const genericMatch = text.match(/```(?:[a-zA-Z0-9#+._-]+)?\s*([\s\S]*?)```/);
            if (genericMatch && genericMatch[1]) {
                return genericMatch[1].trim();
            }

            return text;
        }

        applySuggestion(messageId, mode) {
            const session = this.getActiveSession();
            if (!session) {
                return;
            }

            const message = session.messages.find((entry) => entry.id === messageId);
            if (!message || message.role !== 'assistant') {
                this.showStatus('Could not find that AI suggestion.');
                return;
            }

            const editor = typeof window.findEditor === 'function'
                ? window.findEditor(message.context.editorId)
                : this.activeEditor;
            if (!editor) {
                this.showStatus('The target editor is no longer available.');
                return;
            }

            const suggestedHtml = this.extractSuggestedHtml(message.content);
            if (!suggestedHtml) {
                this.showStatus('No editable suggestion content was found.');
                return;
            }

            const contentChanged = this.hashText(editor.getData()) !== message.context.contentFingerprint;

            try {
                if (mode === 'replaceBlock') {
                    editor.setData(suggestedHtml);
                } else {
                    const snapshot = mode === 'replaceSelection'
                        ? message.context.selectionSnapshot
                        : message.context.caretSnapshot;

                    if (!snapshot) {
                        this.showStatus('No saved selection is available for that suggestion.');
                        return;
                    }

                    editor.model.change((writer) => {
                        const selectionState = this.createSelectionFromSnapshot(writer, editor, snapshot);
                        if (!selectionState) {
                            throw new Error('selection-restore-failed');
                        }

                        writer.setSelection(selectionState.ranges, { backward: selectionState.isBackward });

                        const viewFragment = editor.data.processor.toView(suggestedHtml);
                        const modelFragment = editor.data.toModel(viewFragment);
                        editor.model.insertContent(modelFragment, editor.model.document.selection);
                    });
                }

                editor.editing.view.focus();
                this.showStatus(contentChanged && mode !== 'replaceBlock'
                    ? 'Applied the suggestion using the saved cursor or selection, but the region had changed since the response was generated.'
                    : 'Applied the AI suggestion to the editor.');
            } catch {
                this.showStatus('Could not apply that suggestion to the editor.');
            }
        }

        createSelectionFromSnapshot(writer, editor, snapshot) {
            if (!snapshot || !Array.isArray(snapshot.ranges) || snapshot.ranges.length === 0) {
                return null;
            }

            try {
                const ranges = snapshot.ranges.map((range) => {
                    const startRoot = editor.model.document.getRoot(range.start.root);
                    const endRoot = editor.model.document.getRoot(range.end.root);
                    if (!startRoot || !endRoot) {
                        throw new Error('root-not-found');
                    }

                    const start = writer.createPositionFromPath(startRoot, range.start.path, range.start.stickiness || 'toNone');
                    const end = writer.createPositionFromPath(endRoot, range.end.path, range.end.stickiness || 'toNone');
                    return writer.createRange(start, end);
                });

                return {
                    ranges: ranges,
                    isBackward: !!snapshot.isBackward
                };
            } catch {
                return null;
            }
        }

        setBusy(isBusy) {
            if (this.sendButton) {
                this.sendButton.disabled = isBusy || !this.available;
            }

            if (this.input) {
                this.input.disabled = isBusy || !this.available;
            }

            this.actionButtons.forEach((button) => {
                button.disabled = isBusy || !this.available;
            });
        }

        showStatus(message) {
            if (!this.statusContainer) {
                return;
            }

            this.statusContainer.textContent = message;
            this.statusContainer.style.display = 'block';
        }

        hideStatus() {
            if (!this.statusContainer) {
                return;
            }

            this.statusContainer.textContent = '';
            this.statusContainer.style.display = 'none';
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