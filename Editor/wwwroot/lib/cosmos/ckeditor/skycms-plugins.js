import { Plugin as s, ButtonView as u, IconBrowseFiles as m, IconImageAssetManager as b, IconLink as p, IconCode as k } from "ckeditor5";
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class I extends s {
  init() {
    const e = this.editor, o = e.t;
    e.ui.componentFactory.add("fileLink", (r) => {
      const n = new u(r);
      return n.set({
        label: o("Link to file uploaded to this website."),
        icon: m,
        tooltip: !0
      }), this.listenTo(n, "execute", () => {
        const i = globalThis.window, l = i?.parent?.openInsertFileLinkModel;
        if (l) {
          l(e);
          return;
        }
        const d = i?.prompt, a = d ? d("Enter file URL (example: /files/brochure.pdf):", "/files/brochure.pdf") : null;
        a && a.trim().length > 0 && e.execute("link", a.trim());
      }), n;
    });
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class h extends s {
  init() {
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class U extends s {
  static get requires() {
    return [I, h];
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class w extends s {
  init() {
    const e = this.editor, o = e.t;
    e.ui.componentFactory.add("skyCmsInsertImage", (r) => {
      const n = new u(r);
      return n.set({
        label: o("Insert image uploaded to this website."),
        icon: b,
        tooltip: !0
      }), this.listenTo(n, "execute", () => {
        const i = globalThis.window, l = i?.parent?.openInsertImageModel;
        if (l) {
          l(e);
          return;
        }
        const d = i?.prompt, a = d ? d("Enter image URL (example: /images/hero.jpg):", "/images/hero.jpg") : null;
        a && a.trim().length > 0 && e.execute("insertImage", { source: a.trim() });
      }), n;
    });
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class L extends s {
  init() {
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class y extends s {
  static get requires() {
    return [w, L];
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class f extends s {
  init() {
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class x extends s {
  init() {
    const o = this.editor.t;
    this.editor.ui.componentFactory.add("pageLink", (r) => {
      const n = new u(r);
      return n.set({
        label: o("Insert a link to a page on this website."),
        icon: p,
        tooltip: !0
      }), this.listenTo(n, "execute", () => {
        const i = globalThis.window, l = i?.parent?.openPickPageModal;
        if (l) {
          l(this.editor);
          return;
        }
        const d = i?.prompt, a = d ? d("Enter relative page URL (example: /about):", "/about") : null;
        a && a.trim().length > 0 && this.editor.execute("link", a.trim());
      }), n;
    });
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class R extends s {
  static get requires() {
    return [f, x];
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class E extends s {
  static get pluginName() {
    return "SignalR";
  }
  init() {
    const e = this.editor, o = (r) => {
      const i = globalThis.window?.parent?.cosmosSignalOthers;
      i ? i(e, r) : console.log(`signalr: ${r}`);
    };
    e.editing.view.document.on("change:isFocused", () => {
      e.editing.view.document.isFocused ? o("focus") : o("blur");
    }), e.editing.view.document.on("keydown", () => {
      o("keydown");
    }), e.editing.view.document.on("mousedown", () => {
      o("mousedown");
    });
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class T extends s {
  init() {
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class O extends s {
  static get requires() {
    return [E, T];
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class F extends s {
  init() {
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class S extends s {
  static get pluginName() {
    return "VSCodeEditor";
  }
  init() {
    const e = this.editor, o = e.t;
    e.ui.componentFactory.add("vsCodeEditor", (r) => {
      const n = new u(r);
      return n.set({
        label: o("Open code editor."),
        icon: k,
        tooltip: !0
      }), this.listenTo(n, "execute", () => {
        const l = globalThis.window?.parent?.openVsCodeBlockEditor;
        if (l)
          l(e);
        else {
          const d = globalThis.alert;
          d && d("No host VS Code editor bridge detected.");
        }
      }), n;
    });
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class M extends s {
  static get requires() {
    return [F, S];
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
const c = {
  title: {
    toolbar: [],
    balloonToolbar: ["bold", "italic"]
  },
  simple: {
    toolbar: [
      "undo",
      "redo",
      "|",
      "heading",
      "|",
      "bold",
      "italic",
      "underline",
      "|",
      "link",
      "|",
      "bulletedList",
      "numberedList",
      "|",
      "blockQuote"
    ],
    balloonToolbar: ["bold", "italic", "link"]
  },
  standard: {
    toolbar: [
      "undo",
      "redo",
      "|",
      "heading",
      "|",
      "bold",
      "italic",
      "underline",
      "|",
      "link",
      "pageLink",
      "fileLink",
      "|",
      "bulletedList",
      "numberedList",
      "todoList",
      "|",
      "blockQuote",
      "|",
      "skyCmsInsertImage"
    ],
    balloonToolbar: [
      "bold",
      "italic",
      "underline",
      "|",
      "link",
      "pageLink",
      "skyCmsInsertImage"
    ]
  },
  advanced: {
    toolbar: [
      "heading",
      "|",
      "pageLink",
      "imageInsert",
      "skyCmsInsertImage",
      "mediaEmbed",
      "insertTable",
      "blockQuote",
      "codeBlock",
      "|",
      "bulletedList",
      "numberedList",
      "todoList",
      "outdent",
      "indent"
    ],
    balloonToolbar: [
      "bold",
      "italic",
      "underline",
      "|",
      "bookmark",
      "pageLink",
      "link",
      "skyCmsInsertImage",
      "|",
      "bulletedList",
      "numberedList"
    ]
  }
}, C = {
  title: "title",
  heading: "title",
  simple: "simple",
  standard: "standard",
  default: "advanced",
  richtext: "advanced",
  ckeditor: "advanced",
  advanced: "advanced",
  skycms: "advanced"
};
function P(t = "standard", {
  tagName: e = null,
  fallbackProfile: o = "standard"
} = {}) {
  const r = e ? e.toLowerCase() : null;
  if (r && /^(h[1-6])$/.test(r))
    return "title";
  const n = typeof t == "string" ? t.toLowerCase() : "", i = C[n] || n;
  return c[i] ? i : c[o] ? o : "standard";
}
function g(t = "standard", e = {}) {
  const o = P(t, e);
  return c[o];
}
function V(t = "standard", e = {}) {
  return g(t, e).toolbar;
}
function B(t = "standard", e = {}) {
  return g(t, e).balloonToolbar;
}
const W = c;
export {
  U as FileLink,
  y as InsertImage,
  R as PageLink,
  c as SKYCMS_EDITOR_PROFILES,
  C as SKYCMS_EDITOR_PROFILE_ALIASES,
  O as SignalR,
  W as TOOLBAR_PROFILES,
  M as VSCodeEditor,
  B as getBalloonToolbarProfile,
  g as getEditorProfile,
  V as getToolbarProfile,
  P as resolveEditorProfileName
};
