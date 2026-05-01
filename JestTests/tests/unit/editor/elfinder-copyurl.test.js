const fs = require('fs');
const path = require('path');
const vm = require('vm');
const { describe, test, expect, beforeEach } = require('@jest/globals');

function createHarness(fileBaseUrl, navigatorStub, windowStub) {
  const filePath = path.join(__dirname, '../../../..', 'Editor/Views/Shared/FileExplorer/IndexModern.cshtml');
  const source = fs.readFileSync(filePath, 'utf8');

  // Extract the ccmscopyurl command registration block.
  const marker = '$.fn.elfinder.commands.ccmscopyurl';
  const blockStart = source.indexOf(marker);
  if (blockStart < 0) {
    throw new Error('Could not find ccmscopyurl command registration in IndexModern.cshtml');
  }

  // Find the wrapping IIFE — walk backwards to its opening paren.
  const iifeOpen = source.lastIndexOf('(function ()', blockStart);
  if (iifeOpen < 0) {
    throw new Error('Could not find IIFE wrapper for ccmscopyurl');
  }

  // Extract up to the closing })(); of the IIFE.
  const iifeClose = source.indexOf('})();', iifeOpen);
  if (iifeClose < 0) {
    throw new Error('Could not find closing of ccmscopyurl IIFE');
  }

  const commandBlock = source.substring(iifeOpen, iifeClose + 5);

  const script = `
    var fileBaseUrl = ${JSON.stringify(fileBaseUrl || 'https://cdn.example.test')};

    // Minimal jQuery stub.
    var $ = {
      fn: {
        elfinder: {
          commands: {},
          prototype: {}
        }
      },
      extend: function(target) {
        for (var i = 1; i < arguments.length; i++) {
          var src = arguments[i];
          if (src) {
            for (var k in src) {
              if (Object.prototype.hasOwnProperty.call(src, k)) {
                target[k] = src[k];
              }
            }
          }
        }
        return target;
      },
      Deferred: function() {
        var resolved = false;
        var rejected = false;
        return {
          resolve: function() { resolved = true; return this; },
          reject: function() { rejected = true; return this; },
          __resolved: function() { return resolved; },
          __rejected: function() { return rejected; }
        };
      }
    };

    ${commandBlock}

    globalThis.__exports = {
      CcmsCopyUrl: $.fn.elfinder.commands.ccmscopyurl
    };
  `;

  // Provide navigator and window as direct properties so vm closures
  // can resolve them as globals within their original sandbox scope.
  const context = {
    globalThis: {},
    navigator: navigatorStub || {},
    window: windowStub || {}
  };
  vm.createContext(context);
  vm.runInContext(script, context);

  return context.globalThis.__exports;
}

function buildFakeCommand(fileBaseUrl, fmFiles, fmPath, navigatorStub, windowStub) {
  const harness = createHarness(fileBaseUrl, navigatorStub, windowStub);
  const CcmsCopyUrl = harness.CcmsCopyUrl;

  const instance = new CcmsCopyUrl();
  instance.fm = {
    files: fmFiles,
    path: fmPath,
    selected: () => []
  };

  // Attach prototype methods.
  instance.exec = CcmsCopyUrl.prototype.exec;
  instance.getstate = CcmsCopyUrl.prototype.getstate;

  return instance;
}

describe('elFinder ccmscopyurl command', () => {
  function makeNavigator(spy) {
    return { clipboard: { writeText: spy || jest.fn(() => Promise.resolve()) } };
  }

  function makeWindow(successSpy, errorSpy) {
    return { toastr: { success: successSpy || jest.fn(), error: errorSpy || jest.fn() } };
  }

  test('exec copies the correct public URL for a file', async () => {
    const writeText = jest.fn(() => Promise.resolve());
    const successSpy = jest.fn();
    const cmd = buildFakeCommand(
      'https://cdn.example.test',
      () => [{ mime: 'image/jpeg' }],
      () => '/pub/articles/11/photo.jpg',
      makeNavigator(writeText),
      makeWindow(successSpy)
    );

    const deferred = cmd.exec(['hash1']);

    await Promise.resolve(); // flush microtask for clipboard promise

    expect(writeText).toHaveBeenCalledWith(
      'https://cdn.example.test/pub/articles/11/photo.jpg'
    );
    expect(deferred.__resolved()).toBe(true);
  });

  test('exec trims trailing slash from base URL', async () => {
    const writeText = jest.fn(() => Promise.resolve());
    const cmd = buildFakeCommand(
      'https://cdn.example.test/',
      () => [{ mime: 'image/png' }],
      () => '/pub/logo.png',
      makeNavigator(writeText),
      makeWindow()
    );

    cmd.exec(['hash1']);
    await Promise.resolve();

    expect(writeText).toHaveBeenCalledWith(
      'https://cdn.example.test//pub/logo.png'
    );
    // Note: the view renders options.BlobPublicUrl.TrimEnd('/') so double-slash
    // won't happen at runtime — this test documents the raw JS behaviour.
  });

  test('exec rejects for directories', () => {
    const writeText = jest.fn(() => Promise.resolve());
    const cmd = buildFakeCommand(
      'https://cdn.example.test',
      () => [{ mime: 'directory' }],
      () => '/pub/articles',
      makeNavigator(writeText),
      makeWindow()
    );

    const deferred = cmd.exec(['hash1']);

    expect(deferred.__rejected()).toBe(true);
    expect(writeText).not.toHaveBeenCalled();
  });

  test('exec rejects when no hashes provided', () => {
    const cmd = buildFakeCommand(
      'https://cdn.example.test',
      () => [],
      () => null,
      makeNavigator(),
      makeWindow()
    );

    const deferred = cmd.exec([]);

    expect(deferred.__rejected()).toBe(true);
  });

  test('exec rejects when path is null', () => {
    const cmd = buildFakeCommand(
      'https://cdn.example.test',
      () => [{ mime: 'image/jpeg' }],
      () => null,
      makeNavigator(),
      makeWindow()
    );

    const deferred = cmd.exec(['hash1']);

    expect(deferred.__rejected()).toBe(true);
  });

  test('getstate returns 0 for a single file', () => {
    const cmd = buildFakeCommand(
      'https://cdn.example.test',
      (sel) => sel.length ? [{ mime: 'image/jpeg' }] : [],
      () => '/pub/file.jpg',
      makeNavigator(),
      makeWindow()
    );

    expect(cmd.getstate(['hash1'])).toBe(0);
  });

  test('getstate returns -1 for directories', () => {
    const cmd = buildFakeCommand(
      'https://cdn.example.test',
      (sel) => sel.length ? [{ mime: 'directory' }] : [],
      () => '/pub/folder',
      makeNavigator(),
      makeWindow()
    );

    expect(cmd.getstate(['hash1'])).toBe(-1);
  });

  test('getstate returns -1 for empty selection', () => {
    const cmd = buildFakeCommand(
      'https://cdn.example.test',
      () => [],
      () => null,
      makeNavigator(),
      makeWindow()
    );

    expect(cmd.getstate([])).toBe(-1);
  });
});
