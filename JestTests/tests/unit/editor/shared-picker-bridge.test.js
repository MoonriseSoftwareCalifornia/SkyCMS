const fs = require('fs');
const path = require('path');
const vm = require('vm');
const { describe, test, expect } = require('@jest/globals');

function extractFunctionSource(fileText, functionName) {
  const signature = `function ${functionName}(`;
  const start = fileText.indexOf(signature);
  if (start < 0) {
    throw new Error(`Could not find function ${functionName}`);
  }

  const bodyStart = fileText.indexOf('{', start);
  if (bodyStart < 0) {
    throw new Error(`Could not find body for function ${functionName}`);
  }

  let depth = 0;
  for (let i = bodyStart; i < fileText.length; i++) {
    const ch = fileText[i];
    if (ch === '{') {
      depth++;
    } else if (ch === '}') {
      depth--;
      if (depth === 0) {
        return fileText.substring(start, i + 1);
      }
    }
  }

  throw new Error(`Could not parse function ${functionName}`);
}

function loadSharedPickerFunctions() {
  const filePath = path.join(__dirname, '../../../..', 'Editor/Views/Shared/_LayoutEditor.cshtml');
  const source = fs.readFileSync(filePath, 'utf8');

  const functionsToLoad = [
    'resolvePublicFileUrl',
    'resolvePickerPath',
    'buildFilePickerSelection',
    'completeFilePickerSelection',
    'handleFilePickerModalHidden'
  ];

  const extracted = functionsToLoad
    .map((name) => extractFunctionSource(source, name))
    .join('\n\n');

  const script = `
    const fileBaseUrl = 'https://cdn.example.test';
    let pendingFilePickerOptions = null;
    let isCompletingFilePickerSelection = false;
    let saveChangesCallCount = 0;
    let checkBannerImageCallCount = 0;

    function saveChanges() {
      saveChangesCallCount++;
    }

    function checkBannerImage() {
      checkBannerImageCallCount++;
    }

    ${extracted}

    function __setPendingFilePickerOptions(options) {
      pendingFilePickerOptions = options;
    }

    function __setIsCompletingFilePickerSelection(value) {
      isCompletingFilePickerSelection = value;
    }

    function __getPendingFilePickerOptions() {
      return pendingFilePickerOptions;
    }

    function __getSaveChangesCallCount() {
      return saveChangesCallCount;
    }

    function __getCheckBannerImageCallCount() {
      return checkBannerImageCallCount;
    }

    globalThis.__exports = {
      resolvePublicFileUrl,
      resolvePickerPath,
      buildFilePickerSelection,
      completeFilePickerSelection,
      handleFilePickerModalHidden,
      __setPendingFilePickerOptions,
      __setIsCompletingFilePickerSelection,
      __getPendingFilePickerOptions,
      __getSaveChangesCallCount,
      __getCheckBannerImageCallCount
    };
  `;

  const context = { globalThis: {} };
  vm.createContext(context);
  vm.runInContext(script, context);
  return context.globalThis.__exports;
}

describe('shared picker bridge helpers', () => {
  test('buildFilePickerSelection resolves relative image paths', () => {
    const api = loadSharedPickerFunctions();
    const selection = api.buildFilePickerSelection('pub/articles/11/photo.JPG');

    expect(selection.path).toBe('pub/articles/11/photo.JPG');
    expect(selection.url).toBe('https://cdn.example.test/pub/articles/11/photo.JPG');
    expect(selection.name).toBe('photo.JPG');
    expect(selection.extension).toBe('jpg');
    expect(selection.isImage).toBe(true);
  });

  test('buildFilePickerSelection strips known blob base from absolute url', () => {
    const api = loadSharedPickerFunctions();
    const selection = api.buildFilePickerSelection('https://cdn.example.test/pub/articles/11/readme.pdf');

    expect(selection.path).toBe('pub/articles/11/readme.pdf');
    expect(selection.url).toBe('https://cdn.example.test/pub/articles/11/readme.pdf');
    expect(selection.name).toBe('readme.pdf');
    expect(selection.extension).toBe('pdf');
    expect(selection.isImage).toBe(false);
  });

  test('resolvePublicFileUrl preserves already absolute urls', () => {
    const api = loadSharedPickerFunctions();
    const url = api.resolvePublicFileUrl('https://other.example/path/file.svg');

    expect(url).toBe('https://other.example/path/file.svg');
  });

  test('handleFilePickerModalHidden invokes onCancel and clears pending options', () => {
    const api = loadSharedPickerFunctions();
    const onCancel = jest.fn();
    api.__setPendingFilePickerOptions({ onCancel });
    api.__setIsCompletingFilePickerSelection(false);

    api.handleFilePickerModalHidden();

    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(api.__getPendingFilePickerOptions()).toBeNull();
  });

  test('handleFilePickerModalHidden ignores cancel while selection completion is in progress', () => {
    const api = loadSharedPickerFunctions();
    const onCancel = jest.fn();
    const pending = { onCancel };
    api.__setPendingFilePickerOptions(pending);
    api.__setIsCompletingFilePickerSelection(true);

    api.handleFilePickerModalHidden();

    expect(onCancel).not.toHaveBeenCalled();
    expect(api.__getPendingFilePickerOptions()).toBe(pending);
  });

  test('completeFilePickerSelection skips saveChanges when autoSave is false and still updates banner state', () => {
    const api = loadSharedPickerFunctions();
    const onSelect = jest.fn(() => true);
    api.__setPendingFilePickerOptions({ onSelect, autoSave: false });

    const handled = api.completeFilePickerSelection('pub/articles/7/diagram.png');

    expect(handled).toBe(true);
    expect(onSelect).toHaveBeenCalledWith(
      expect.objectContaining({
        path: 'pub/articles/7/diagram.png',
        isImage: true
      })
    );
    expect(api.__getSaveChangesCallCount()).toBe(0);
    expect(api.__getCheckBannerImageCallCount()).toBe(1);
    expect(api.__getPendingFilePickerOptions()).toBeNull();
  });
});
