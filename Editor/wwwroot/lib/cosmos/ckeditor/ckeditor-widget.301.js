// CKEditor configuration for Sky CMS.
import {
    InlineEditor,
    Autoformat,
    AutoImage,
    Autosave,
    BalloonToolbar,
    BlockQuote,
    Bookmark,
    Bold,
    CodeBlock,
    Essentials,
    Heading,
    ImageBlock,
    ImageCaption,
    ImageInline,
    ImageInsert,
    ImageInsertViaUrl,
    ImageResize,
    ImageStyle,
    ImageTextAlternative,
    ImageToolbar,
    ImageUpload,
    ImageUploadEditing,
    Indent,
    IndentBlock,
    Italic,
    Link,
    LinkImage,
    List,
    ListProperties,
    MediaEmbed,
    Paragraph,
    PasteFromOffice,
    SimpleUploadAdapter,
    Table,
    TableCaption,
    TableCellProperties,
    TableColumnResize,
    TableProperties,
    TableToolbar,
    TextTransformation,
    TodoList,
    Underline
} from 'ckeditor5';

import {
    FileLink,
    InsertImage,
    PageLink,
    SignalR,
    VSCodeEditor,
    getEditorProfile,
    resolveEditorProfileName
} from 'skycmsplugins';

// Shared GUID generator with fallbacks if the shared helper is not loaded yet.
const ccmsGenerateGuid = (typeof window !== 'undefined' && window.ccmsGenerateGuid)
    ? window.ccmsGenerateGuid
    : function ccmsGenerateGuidFallback() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    };

if (typeof window !== 'undefined') {
    window.ccmsGenerateGuid = ccmsGenerateGuid;
    window.ccms__generateGUID = ccmsGenerateGuid;
    window.ccms___generateGUID = ccmsGenerateGuid;
}

function getDistanceFromTop() {
    const element = document.getElementById('ccms---header---end');
    let distance = 0;
    while (element) {
        distance += element.offsetTop;
        element = element.offsetParent;
    }
    return distance;
}

async function ccms___destroyAllEditors() {
    // Destroy all active CKEditor instances
    for (let i = ccms_editors.length - 1; i >= 0; i--) {
        try {
            const editor = ccms_editors[i];
            if (editor && typeof editor.destroy === 'function') {
                await editor.destroy();
            }
        } catch (error) {
            console.warn('Error destroying CKEditor instance:', error);
        }
    }
    ccms_editors = [];
    ccms_editorIds = [];
    focusedEditor = null;
}

/**
 * Create a free account with a trial: https://portal.ckeditor.com/checkout?plan=free
 */
const LICENSE_KEY = 'GPL'; // or <YOUR_LICENSE_KEY>.

/**
 * EditorConfig - Full-featured editor configuration
 * Used for main content areas with rich editing capabilities.
 * Autosave calls parent.saveChanges() -> Command: "SavePageProperties"
 */
const EditorConfig = {
    plugins: [
        Autoformat,
        AutoImage,
        Autosave,
        BalloonToolbar,
        BlockQuote,
        Bold,
        Bookmark,
        CodeBlock,
        Essentials,
        Heading,
        ImageBlock,
        ImageCaption,
        ImageInline,
        ImageInsert,
        ImageInsertViaUrl,
        ImageResize,
        ImageStyle,
        ImageTextAlternative,
        ImageToolbar,
        ImageUpload,
        ImageUploadEditing,
        Indent,
        IndentBlock,
        Italic,
        Link,
        LinkImage,
        List,
        ListProperties,
        MediaEmbed,
        Paragraph,
        PasteFromOffice,
        SimpleUploadAdapter,
        Table,
        TableCaption,
        TableCellProperties,
        TableColumnResize,
        TableProperties,
        TableToolbar,
        TextTransformation,
        TodoList,
        Underline,
        FileLink,
        InsertImage,
        PageLink,
        VSCodeEditor,
        SignalR,
    ],
    balloonToolbar: ['bold', 'italic', 'underline', '|', 'bookmark', 'pageLink', 'link', 'skyCmsInsertImage', '|', 'bulletedList', 'numberedList'],
    placeholder: 'Add your content here.',
    licenseKey: LICENSE_KEY,
    toolbar: {
        items: [
            'heading',
            '|',
            'pageLink',
            'imageInsert',
            'skyCmsInsertImage',
            'mediaEmbed',
            'insertTable',
            'blockQuote',
            'codeBlock',
            '|',
            'bulletedList',
            'numberedList',
            'todoList',
            'outdent',
            'indent'
        ],
        shouldNotGroupWhenFull: false
    },
    // Autosave configuration for full content editors
    // Triggers after 3 seconds of inactivity, calls parent.saveChanges()
    // which sends Command: "SavePageProperties" to the unified endpoint
    autosave: {
        waitingTime: 3000, // in ms
        save(editor) {
            // Safety checks: ensure parent context and autosave are enabled
            if (parent && parent.enableAutoSave === true && typeof parent.saveChanges === 'function') {
                try {
                    return parent.saveChanges(editor.getData(), editor.sourceElement.getAttribute("data-ccms-ceid"));
                } catch (error) {
                    console.error('EditorConfig autosave failed:', error);
                    return Promise.reject(error);
                }
            }
            return Promise.resolve(); // Return resolved promise when autosave is disabled
        }
    },
    simpleUpload: {
        // The URL that the images are uploaded to.
        uploadUrl: '/FileManager/SimpleUpload/' + articleNumber,
            // Enable the XMLHttpRequest.withCredentials property.
            withCredentials: true
        },
    menuBar: {
        isVisible: false
    },
    fontFamily: {
        supportAllValues: true
    },
    fontSize: {
        options: [10, 12, 14, 'default', 18, 20, 22],
        supportAllValues: true
    },
    heading: {
        options: [
            {
                model: 'paragraph',
                title: 'Paragraph',
            },
            {
                model: 'heading1',
                view: 'h1',
                title: 'Heading 1',
            },
            {
                model: 'heading2',
                view: 'h2',
                title: 'Heading 2',
            },
            {
                model: 'heading3',
                view: 'h3',
                title: 'Heading 3',
            },
            {
                model: 'heading4',
                view: 'h4',
                title: 'Heading 4',
            },
            {
                model: 'heading5',
                view: 'h5',
                title: 'Heading 5',
            },
            {
                model: 'heading6',
                view: 'h6',
                title: 'Heading 6',
            }
        ]
    },
    htmlSupport: {
        allow: [
            {
                name: /^.*$/,
                styles: true,
                attributes: true,
                classes: true
            }
        ]
    },
    image: {
        toolbar: [
            'toggleImageCaption',
            'imageTextAlternative',
            '|',
            'imageStyle:inline',
            'imageStyle:wrapText',
            'imageStyle:breakText',
            '|',
            'resizeImage'
        ]
    },
    licenseKey: LICENSE_KEY,
    link: {
        addTargetToExternalLinks: true,
        decorators: {
            openInNewTab: {
                mode: 'manual',
                label: 'Open in a new tab',
                attributes: {
                    target: '_blank',
                    rel: 'noopener noreferrer'
                }
            }
        },
        defaultProtocol: 'https://',
        decorators: {
            toggleDownloadable: {
                mode: 'manual',
                label: 'Downloadable',
                attributes: {
                    download: 'file'
                }
            }
        }
    },
    list: {
        properties: {
            styles: true,
            startIndex: true,
            reversed: true
        }
    },
    ui: {
        viewportOffset: {
            top: getDistanceFromTop(),
        }
    },
    table: {
        contentToolbar: ['tableColumn', 'tableRow', 'mergeTableCells', 'tableProperties', 'tableCellProperties']
    }
};

/**
 * TitleEditorConfig - Minimal editor configuration
 * Used for title and heading elements with limited formatting options.
 * Autosave calls parent.saveEditorRegion() -> Command: "SaveRegion"
 */
const TitleEditorConfig = {
    plugins: [
        Autosave,
        BalloonToolbar,
        Bold,
        Essentials,
        Italic,
        Paragraph,
        SignalR,
    ],
    balloonToolbar: ['bold', 'italic'],
    placeholder: 'Enter title...',
    licenseKey: LICENSE_KEY,
    toolbar: {
        items: [],
        shouldNotGroupWhenFull: false
    },
    // Autosave configuration for title/heading editors (minimal toolbar)
    // Triggers after 3 seconds of inactivity, calls parent.saveEditorRegion()
    // which sends Command: "SaveRegion" to the unified endpoint
    autosave: {
        waitingTime: 3000, // in ms
        save(editor) {
            // Safety checks: ensure parent context and autosave are enabled
            if (parent && parent.enableAutoSave === true && typeof parent.saveEditorRegion === 'function') {
                try {
                    return parent.saveEditorRegion(editor.getData(), editor.sourceElement.getAttribute("data-ccms-ceid"));
                } catch (error) {
                    console.error('TitleEditorConfig autosave failed:', error);
                    return Promise.reject(error);
                }
            }
            return Promise.resolve(); // Return resolved promise when autosave is disabled
        }
    },
    menuBar: {
        isVisible: false
    },
    ui: {
        viewportOffset: {
            top: getDistanceFromTop(),
        }
    }
};

function ccms___createEditor(editorElement) {

    if (typeof editorElement.ckeditorInstance !== "undefined" && editorElement.ckeditorInstance !== null) {
        return; //
    }

    const isNew = editorElement.getAttribute("data-ccms-new");

    if (isNew) {
        const guid = ccmsGenerateGuid();
        editorElement.setAttribute("data-ccms-ceid", guid);
        editorElement.removeAttribute("data-ccms-new");
    }

    // Determine which profile and configuration to use.
    // Unknown values intentionally fall back to standard for a safer authoring baseline.
    const tagName = editorElement.tagName.toLowerCase();
    const editorType = editorElement.hasAttribute('data-editor-config') 
        ? editorElement.getAttribute('data-editor-config').toLowerCase() 
        : null;
    const resolvedProfileName = resolveEditorProfileName(editorType, {
        tagName,
        fallbackProfile: 'standard'
    });
    const resolvedProfile = getEditorProfile(resolvedProfileName);

    const baseConfig = resolvedProfileName === 'title' ? TitleEditorConfig : EditorConfig;
    const config = {
        ...baseConfig,
        toolbar: {
            ...(baseConfig.toolbar || {}),
            items: [ ...resolvedProfile.toolbar ]
        },
        balloonToolbar: [ ...resolvedProfile.balloonToolbar ]
    };

    InlineEditor
        .create(editorElement, config)
        .then(editor => {
            window.editor = editor;
            editorElement.ckeditorInstance = editor;
            
            if (editor.plugins.has('ImageUploadEditing')) {
                const imageUploadEditing = editor.plugins.get('ImageUploadEditing');
                imageUploadEditing.on('uploadComplete', (evt, { data, imageElement }) => {
                    if (parent && parent.ccms_setBannerImage) {
                        parent.ccms_setBannerImage(data.url);
                    }
                });
            }
            
            editor.editing.view.document.on('change:isFocused', (evt, data, isFocused) => {
                console.log(`View document is focused: ${isFocused}.`);
                if (isFocused) {
                    focusedEditor = editor;
                } else {
                    focusedEditor = null;
                }
            });
            ccms_editors.push(editor);
        })
        .catch(error => {
            console.error('Failed to create CKEditor instance:', error);
        });
}

function ccms___createEditors() {
    // Editor instances
    const editorElements = document.querySelectorAll('[data-ccms-ceid]');
    editorElements.forEach(editorElement => {
        // let config;
        let editorType = 'default';

        if (editorElement.hasAttribute('data-editor-config')) {
            editorType = editorElement.getAttribute('data-editor-config').toLowerCase();
        }

        if (editorType !== 'image-widget') {
            ccms___createEditor(editorElement); // Function in ckeditor-widget.js
        }
    });
}

window.createCkEditor = ccms___createEditor;
window.ccms___destroyAllEditors = ccms___destroyAllEditors;

document.addEventListener("DOMContentLoaded", function (event) {
    ccms___createEditors();
});