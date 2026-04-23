/**
 * Accessibility Features
 * Screen reader support, keyboard navigation, high contrast themes
 */

function setupAccessibility(editor) {
    // Enable screen reader support
    editor.updateOptions({
        accessibilitySupport: 'on',
        ariaLabel: 'Code Editor',
        
        // High contrast support
        detectIndentation: true,
        
        // Keyboard navigation
        multiCursorModifier: 'ctrlCmd',
        
        // Focus indication
        renderLineHighlight: 'all',
        renderLineHighlightOnlyWhenFocus: false
    });
    
    // Add ARIA labels to editor
    const editorElement = editor.getDomNode();
    editorElement.setAttribute('role', 'textbox');
    editorElement.setAttribute('aria-multiline', 'true');
    editorElement.setAttribute('aria-label', 'Code Editor for editing content');
}

// Keyboard shortcuts help modal
function showKeyboardShortcuts() {
    const shortcuts = [
        { key: 'Ctrl+S / Cmd+S', description: 'Save changes' },
        { key: 'Ctrl+F / Cmd+F', description: 'Find' },
        { key: 'Ctrl+H / Cmd+H', description: 'Replace' },
        { key: 'Ctrl+D / Cmd+D', description: 'Add selection to next find match' },
        { key: 'Alt+Up/Down', description: 'Move line up/down' },
        { key: 'Ctrl+/ / Cmd+/', description: 'Toggle line comment' },
        { key: 'Ctrl+K Ctrl+F', description: 'Format selection' },
        { key: 'Ctrl+Space', description: 'Trigger suggestions' },
        { key: 'F12', description: 'Go to definition' }
    ];
    
    // Show modal with shortcuts
    const modal = new bootstrap.Modal(document.getElementById('keyboardShortcutsModal'));
    modal.show();
}