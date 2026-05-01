const fs = require('fs');
const path = require('path');
const vm = require('vm');
const { describe, test, expect, beforeEach } = require('@jest/globals');

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

function createHarness() {
  const filePath = path.join(__dirname, '../../../..', 'Editor/Views/Shared/_GrapesJsEditor.cshtml');
  const source = fs.readFileSync(filePath, 'utf8');

  const openSharedAssetPickerSource = extractFunctionSource(source, 'openSharedAssetPicker');
  const script = `
    ${openSharedAssetPickerSource}
    globalThis.__exports = { openSharedAssetPicker };
  `;

  const context = {
    globalThis: {},
    window: {},
    toastr: { error: () => {} }
  };

  vm.createContext(context);
  vm.runInContext(script, context);

  return {
    openSharedAssetPicker: context.globalThis.__exports.openSharedAssetPicker,
    window: context.window,
    toastr: context.toastr,
    source
  };
}

describe('grapesjs shared picker integration', () => {
  let harness;

  beforeEach(() => {
    harness = createHarness();
  });

  test('shows an error when host picker bridge is unavailable', () => {
    const errorSpy = jest.fn();
    harness.toastr.error = errorSpy;

    harness.openSharedAssetPicker({
      select: jest.fn(),
      close: jest.fn()
    });

    expect(errorSpy).toHaveBeenCalledWith('File picker is not available.');
  });

  test('requests image mode picker and maps selected url into asset manager', () => {
    let pickerOptions = null;
    const selectSpy = jest.fn();
    const closeSpy = jest.fn();

    harness.window.CCMS___OpenFilePicker = (options) => {
      pickerOptions = options;
    };

    harness.openSharedAssetPicker({
      select: selectSpy,
      close: closeSpy
    });

    expect(pickerOptions).not.toBeNull();
    expect(pickerOptions.mode).toBe('image');
    expect(pickerOptions.autoSave).toBe(false);

    const invalidResult = pickerOptions.onSelect({ path: 'pub/articles/1/no-url-only-path' });
    expect(invalidResult).toBe(false);
    expect(selectSpy).not.toHaveBeenCalled();

    const validResult = pickerOptions.onSelect({ url: 'https://cdn.example.test/pub/articles/1/pic.png' });
    expect(validResult).toBe(true);
    expect(selectSpy).toHaveBeenCalledWith(
      {
        src: 'https://cdn.example.test/pub/articles/1/pic.png',
        type: 'image'
      },
      true
    );
    expect(closeSpy).toHaveBeenCalled();

    pickerOptions.onCancel();
    expect(closeSpy).toHaveBeenCalledTimes(2);
  });

  test('wires GrapesJS asset manager custom open to shared picker', () => {
    expect(harness.source).toContain('assetManager:');
    expect(harness.source).toContain('custom: {');
    expect(harness.source).toContain('openSharedAssetPicker(assetManagerProps);');
  });
});
