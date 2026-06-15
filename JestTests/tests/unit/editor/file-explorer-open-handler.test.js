const fs = require('fs');
const path = require('path');
const vm = require('vm');
const { describe, test, expect } = require('@jest/globals');

function findMatchingBrace(source, openBraceIndex) {
  let depth = 0;
  for (let i = openBraceIndex; i < source.length; i++) {
    const ch = source[i];
    if (ch === '{') {
      depth++;
    } else if (ch === '}') {
      depth--;
      if (depth === 0) {
        return i;
      }
    }
  }

  throw new Error('Could not find matching closing brace.');
}

function extractNamedFunction(source, functionName) {
  const marker = `function ${functionName}(`;
  const start = source.indexOf(marker);
  if (start < 0) {
    throw new Error(`Could not find function ${functionName} in Index.cshtml`);
  }

  const openBrace = source.indexOf('{', start);
  const closeBrace = findMatchingBrace(source, openBrace);
  return source.substring(start, closeBrace + 1);
}

function extractOpenHandlerBody(source) {
  const marker = 'open: function (event) {';
  const start = source.indexOf(marker);
  if (start < 0) {
    throw new Error('Could not find elFinder open handler in Index.cshtml');
  }

  const openBrace = source.indexOf('{', start);
  const closeBrace = findMatchingBrace(source, openBrace);
  return source.substring(openBrace + 1, closeBrace);
}

function buildHarness(initialState = {}) {
  const filePath = path.join(__dirname, '../../../..', 'Editor/Views/Shared/FileExplorer/Index.cshtml');
  const source = fs.readFileSync(filePath, 'utf8');

  const normalizePathFn = extractNamedFunction(source, 'normalizePath');
  const decodePathFn = extractNamedFunction(source, 'decodePath');
  const openHandlerBody = extractOpenHandlerBody(source);

  const script = `
    ${normalizePathFn}
    ${decodePathFn}

    var currentUploadPath = ${JSON.stringify(initialState.currentUploadPath || '/pub')};
    var currentUploadPathDisplay = ${JSON.stringify(initialState.currentUploadPathDisplay || '/pub')};
    var updateReadyUploadStatusCallCount = 0;

    function updateReadyUploadStatus() {
      updateReadyUploadStatusCallCount += 1;
    }

    var elFinderInstance = ${initialState.elFinderInstance ? 'initialElFinderInstance' : 'null'};

    function runOpenHandler(event) {
      ${openHandlerBody}
    }

    globalThis.__exports = {
      runOpenHandler: runOpenHandler,
      getState: function () {
        return {
          currentUploadPath: currentUploadPath,
          currentUploadPathDisplay: currentUploadPathDisplay,
          updateReadyUploadStatusCallCount: updateReadyUploadStatusCallCount
        };
      }
    };
  `;

  const context = {
    globalThis: {},
    Buffer,
    initialElFinderInstance: initialState.elFinderInstance || null,
    atob: (input) => Buffer.from(input, 'base64').toString('binary'),
    decodeURIComponent,
    escape,
  };

  vm.createContext(context);
  vm.runInContext(script, context);

  return context.globalThis.__exports;
}

function encodePath(pathValue) {
  const trimmed = pathValue.replace(/^\//, '').replace(/\/$/, '');
  const encoded = Buffer.from(trimmed, 'utf8')
    .toString('base64')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');

  return `l1_${encoded}`;
}

describe('FileExplorer open handler display path resolution', () => {
  test('prefers cwd.displayPath over top-level options.path', () => {
    const harness = buildHarness();

    harness.runOpenHandler({
      data: {
        cwd: {
          hash: encodePath('/pub/articles/42'),
          displayPath: '/pub/articles/My Great Article'
        },
        options: {
          path: 'pub/articles/Wrong Fallback'
        }
      }
    });

    const state = harness.getState();
    expect(state.currentUploadPath).toBe('/pub/articles/42');
    expect(state.currentUploadPathDisplay).toBe('/pub/articles/My Great Article');
    expect(state.updateReadyUploadStatusCallCount).toBe(1);
  });

  test('falls back to top-level options.path when cwd.displayPath is absent', () => {
    const harness = buildHarness();

    harness.runOpenHandler({
      data: {
        cwd: {
          hash: encodePath('/pub/articles/42/assets/images')
        },
        options: {
          path: 'pub/articles/My Great Article/assets/images'
        }
      }
    });

    const state = harness.getState();
    expect(state.currentUploadPath).toBe('/pub/articles/42/assets/images');
    expect(state.currentUploadPathDisplay).toBe('/pub/articles/My Great Article/assets/images');
    expect(state.updateReadyUploadStatusCallCount).toBe(1);
  });

  test('falls back to canonical decoded cwd hash when no display path is provided', () => {
    const harness = buildHarness();

    harness.runOpenHandler({
      data: {
        cwd: {
          hash: encodePath('/pub/articles/42/logo.png')
        }
      }
    });

    const state = harness.getState();
    expect(state.currentUploadPath).toBe('/pub/articles/42/logo.png');
    expect(state.currentUploadPathDisplay).toBe('/pub/articles/42/logo.png');
    expect(state.updateReadyUploadStatusCallCount).toBe(1);
  });
});
