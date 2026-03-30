export * from "monaco-editor";
const R = "0.2.0", A = {
  0: "javascript",
  1: "html",
  2: "css",
  3: "xml",
  4: "json"
};
function j(e) {
  if (typeof e == "number")
    return A[e] || "plaintext";
  const t = String(e).trim().toLowerCase();
  return t && {
    javascript: "javascript",
    js: "javascript",
    typescript: "typescript",
    ts: "typescript",
    html: "html",
    css: "css",
    scss: "scss",
    less: "less",
    xml: "xml",
    json: "json",
    markdown: "markdown",
    md: "markdown",
    handlebars: "handlebars",
    hbs: "handlebars",
    razor: "razor",
    cshtml: "razor",
    liquid: "liquid",
    twig: "twig",
    graphql: "graphql",
    gql: "graphql",
    yaml: "yaml",
    yml: "yaml",
    plaintext: "plaintext",
    text: "plaintext"
  }[t] || "plaintext";
}
function T(e) {
  const t = e.fields || [], i = e.values || {}, o = (e.uriBase || "memory://skycms").replace(/\/$/, "");
  return t.map((a) => {
    const s = a.FieldId;
    return {
      id: s,
      name: a.FieldName,
      language: j(a.EditorMode),
      value: i[s] || "",
      uri: `${o}/${encodeURIComponent(s)}.txt`,
      readOnly: !!e.readOnly
    };
  });
}
function q(e, t) {
  if (!e.length)
    throw new Error("resolveActiveFieldId requires at least one field.");
  const i = String(t || "").trim();
  if (!i)
    return e[0].id;
  const o = e.find((s) => s.id === i);
  if (o)
    return o.id;
  const a = e.find((s) => s.name === i);
  return a ? a.id : e[0].id;
}
function P(e, t) {
  return t.Uri.parse(e.uri || `memory://skycms/${encodeURIComponent(e.id)}.txt`);
}
function $(e = {}) {
  const t = e.globalObject || globalThis, i = e.vsBasePath || "/lib/monaco/min/vs", o = t.require;
  o?.config && o.config({ paths: { vs: i } });
  const a = t.MonacoEnvironment || {};
  t.MonacoEnvironment = {
    ...a,
    getWorkerUrl: (s, l) => `${i}/base/worker/workerMain.js`
  };
}
function L(e, t) {
  if (!t.fields?.length)
    throw new Error("SkyCmsEditor requires at least one field.");
  const i = /* @__PURE__ */ new Map(), o = /* @__PURE__ */ new Map(), a = /* @__PURE__ */ new Map(), s = /* @__PURE__ */ new Map(), l = /* @__PURE__ */ new Map(), y = /* @__PURE__ */ new Set(), p = /* @__PURE__ */ new Set();
  for (const n of t.fields) {
    i.set(n.id, n);
    const r = P(n, e), d = e.editor.getModel(r) || e.editor.createModel(n.value || "", n.language || "plaintext", r);
    o.set(n.id, d), s.set(n.id, n.value || ""), l.set(n.id, !1);
  }
  if (!i.has(t.activeFieldId))
    throw new Error(`Active field '${t.activeFieldId}' was not found in fields.`);
  const f = e.languages?.typescript;
  if (f) {
    const n = {
      target: f.ScriptTarget.ES2020,
      lib: ["es2020", "dom", "dom.iterable"],
      allowNonTsExtensions: !0,
      moduleResolution: f.ModuleResolutionKind.NodeJs,
      noEmit: !0,
      esModuleInterop: !0,
      jsx: f.JsxEmit.React,
      allowJs: !0,
      typeRoots: ["node_modules/@types"]
    };
    f.typescriptDefaults.setCompilerOptions(n), f.javascriptDefaults.setCompilerOptions(n), f.typescriptDefaults.setDiagnosticsOptions({ noSemanticValidation: !1, noSyntaxValidation: !1 }), f.javascriptDefaults.setDiagnosticsOptions({ noSemanticValidation: !0, noSyntaxValidation: !1 });
  }
  const v = {
    theme: t.theme || "vs-dark",
    readOnly: !!t.readOnly,
    automaticLayout: t.automaticLayout !== !1,
    minimap: { enabled: !1 },
    fontSize: 14,
    inlineSuggest: { enabled: !0 },
    quickSuggestions: { other: !0, comments: !1, strings: !0 },
    suggestOnTriggerCharacters: !0,
    acceptSuggestionOnEnter: "on",
    tabCompletion: "on",
    model: o.get(t.activeFieldId)
  }, m = e.editor.create(t.container, v);
  let u = t.activeFieldId;
  const b = [];
  if (t.inlineCompletionsProvider) {
    const n = Array.from(new Set(t.fields.map((r) => r.language || "plaintext")));
    for (const r of n) {
      const c = e.languages.registerInlineCompletionsProvider(r, {
        provideInlineCompletions: async (d, h, k, D) => {
          if (D.isCancellationRequested)
            return { items: [] };
          let O = u;
          for (const [V, N] of o.entries())
            if (N === d) {
              O = V;
              break;
            }
          try {
            return { items: await t.inlineCompletionsProvider({
              monaco: e,
              model: d,
              position: h,
              languageId: d.getLanguageId(),
              fieldId: O,
              cancellationToken: D
            }) || [] };
          } catch {
            return { items: [] };
          }
        },
        freeInlineCompletions: () => {
        }
      });
      b.push(c);
    }
  }
  m.onDidChangeModelContent(() => {
    const n = o.get(u);
    if (!n)
      return;
    const r = n.getValue(), c = s.get(u) || "", d = r !== c, h = l.get(u) || !1;
    if (d !== h) {
      l.set(u, d);
      for (const k of p)
        k(u, d);
    }
  });
  function x(n) {
    if (n === u)
      return;
    const r = o.get(n), c = i.get(n);
    if (!r || !c)
      throw new Error(`Cannot switch to unknown field '${n}'.`);
    a.set(u, m.saveViewState()), m.setModel(r), u = n, m.updateOptions({ readOnly: !!t.readOnly || !!c.readOnly });
    const d = a.get(n);
    d && m.restoreViewState(d), m.focus();
    for (const h of y)
      h(n);
  }
  function M(n) {
    const r = n || u, c = o.get(r);
    return c ? c.getValue() : "";
  }
  function w(n, r) {
    const c = o.get(n);
    if (!c)
      throw new Error(`Cannot set value for unknown field '${n}'.`);
    c.setValue(r || "");
  }
  function C() {
    const n = {};
    for (const [r, c] of o.entries())
      n[r] = c.getValue();
    return n;
  }
  function S(n) {
    const r = n || u, c = o.get(r);
    if (!c)
      return;
    if (s.set(r, c.getValue()), l.get(r) || !1) {
      l.set(r, !1);
      for (const h of p)
        h(r, !1);
    }
  }
  function g(n) {
    return l.get(n || u) || !1;
  }
  function E(n) {
    return y.add(n), () => y.delete(n);
  }
  function I(n) {
    return p.add(n), () => p.delete(n);
  }
  return {
    switchField: x,
    getActiveFieldId: () => u,
    getValue: M,
    setValue: w,
    getAllValues: C,
    markClean: S,
    isDirty: g,
    onDidChangeActiveField: E,
    onDidChangeDirty: I,
    getEditor: () => m,
    focus: () => m.focus(),
    dispose: () => {
      for (const n of b)
        n.dispose();
      m.dispose();
      for (const n of o.values())
        n.dispose();
      i.clear(), o.clear(), a.clear(), s.clear(), l.clear(), y.clear(), p.clear();
    }
  };
}
async function W(e) {
  const { emmetHTML: t, emmetCSS: i } = await import("./emmet-monaco.esm-BfUgFTjS.js"), o = t(
    e,
    ["html", "handlebars", "razor"]
  ), a = i(
    e,
    ["css", "scss", "less"]
  );
  return {
    dispose() {
      o.dispose(), a.dispose();
    }
  };
}
function z(e) {
  const t = e.trim();
  return t.endsWith("/complete") ? `${t.slice(0, -9)}/status` : "/api/copilot/status";
}
async function _(e) {
  const t = e.fetchImpl || fetch, i = z(e.completionEndpoint);
  try {
    const o = await t(i, {
      method: "GET",
      headers: { Accept: "application/json" }
    });
    if (!o.ok)
      return null;
    const a = await o.json();
    return {
      enabled: !!a.enabled,
      configured: !!a.configured,
      endpointConfigured: !!a.endpointConfigured,
      model: typeof a.model == "string" ? a.model : void 0
    };
  } catch {
    return null;
  }
}
function B(e) {
  return new Promise((t) => setTimeout(t, e));
}
async function G(e) {
  const t = e.retries ?? 3, i = e.initialDelayMs ?? 250, o = e.backoffMultiplier ?? 2, a = Math.max(0, e.jitterRatio ?? 0.3);
  let s = i;
  for (let l = 0; l <= t; l += 1) {
    const y = await _({
      completionEndpoint: e.completionEndpoint,
      fetchImpl: e.fetchImpl
    });
    if (y)
      return y;
    if (l < t) {
      const p = 1 + (Math.random() * 2 - 1) * a, f = Math.max(0, Math.floor(s * p));
      await B(f), s = Math.floor(s * o);
    }
  }
  return null;
}
function H(e) {
  const t = e.fetchImpl || fetch, i = e.maxPrefixChars ?? 4e3, o = e.maxSuffixChars ?? 1e3;
  return async (a) => {
    const s = a.model, l = a.position, y = {
      startLineNumber: 1,
      startColumn: 1,
      endLineNumber: l.lineNumber,
      endColumn: l.column
    }, p = s.getLineCount(), f = {
      startLineNumber: l.lineNumber,
      startColumn: l.column,
      endLineNumber: p,
      endColumn: s.getLineMaxColumn(p)
    }, v = s.getValueInRange(y).slice(-i), m = s.getValueInRange(f).slice(0, o);
    if (!v.trim())
      return [];
    const u = new AbortController(), b = a.cancellationToken.onCancellationRequested(() => {
      u.abort();
    });
    try {
      const x = e.getAccessToken ? await e.getAccessToken() : null, M = await t(e.endpoint, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...x ? { Authorization: `Bearer ${x}` } : {}
        },
        body: JSON.stringify({
          prefix: v,
          suffix: m,
          language: a.languageId,
          fieldId: a.fieldId,
          uri: s.uri.toString()
        }),
        signal: u.signal
      });
      if (!M.ok)
        return [];
      const w = await M.json(), C = [];
      if (typeof w.completion == "string" && C.push(w.completion), Array.isArray(w.completions))
        for (const g of w.completions)
          typeof g == "string" && C.push(g);
      const S = C.map((g) => g.trimEnd()).filter((g, E, I) => g.length > 0 && I.indexOf(g) === E);
      return S.length ? S.map((g) => ({
        insertText: g,
        range: new a.monaco.Range(
          l.lineNumber,
          l.column,
          l.lineNumber,
          l.column
        )
      })) : [];
    } catch {
      return [];
    } finally {
      b.dispose();
    }
  };
}
async function F(e) {
  const t = await import("monaco-editor");
  return L(t, e);
}
async function U(e, t = {}) {
  const { editor: i } = await import("monaco-editor");
  return i.create(e, t);
}
async function J(e, t = {}) {
  const i = await F({
    container: e,
    fields: [
      {
        id: "Content",
        name: "Content",
        language: String(t.language || "html"),
        value: String(t.value || "")
      }
    ],
    activeFieldId: "Content",
    theme: String(t.theme || "vs-dark"),
    readOnly: !!t.readOnly,
    automaticLayout: t.automaticLayout !== !1
  });
  return {
    getValue: () => i.getValue("Content"),
    setValue: (o) => i.setValue("Content", o),
    focus: () => i.focus(),
    dispose: () => i.dispose(),
    __instance: i
  };
}
const K = {
  version: R,
  initializeEditor: U,
  createSkyCMSEditor: J,
  createSkyCmsEditor: F,
  createSkyCmsEditorWithMonaco: L,
  mapSkyCmsEditorModeToLanguage: j,
  mapSkyCmsEditorFields: T,
  resolveActiveFieldId: q,
  configureMonacoAmdEnvironment: $
};
export {
  $ as configureMonacoAmdEnvironment,
  H as createGitHubCopilotInlineProvider,
  J as createSkyCMSEditor,
  F as createSkyCmsEditor,
  L as createSkyCmsEditorWithMonaco,
  K as default,
  W as enableEmmet,
  _ as fetchCopilotProxyStatus,
  G as fetchCopilotProxyStatusWithRetry,
  U as initializeEditor,
  T as mapSkyCmsEditorFields,
  j as mapSkyCmsEditorModeToLanguage,
  q as resolveActiveFieldId,
  z as resolveCopilotStatusEndpoint,
  R as version
};
