import { Plugin as s, ButtonView as c, IconBrowseFiles as I, ImageInsertUI as h, IconImageUpload as m, MenuBarMenuListItemButtonView as f, IconImageUrl as b, IconImageAssetManager as k, createDropdown as F, IconLink as p, addToolbarToDropdown as x, IconCode as L } from "ckeditor5";
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class T extends s {
  init() {
    const e = this.editor, t = e.t;
    e.ui.componentFactory.add("fileLink", (o) => {
      const i = new c(o);
      return i.set({
        label: t("Link to file uploaded to this website."),
        icon: I,
        tooltip: !0
      }), this.listenTo(i, "execute", () => {
        const n = globalThis.window, a = n?.parent?.openInsertFileLinkModel;
        if (a) {
          a(e);
          return;
        }
        const l = n?.prompt, d = l ? l("Enter file URL (example: /files/brochure.pdf):", "/files/brochure.pdf") : null;
        d && d.trim().length > 0 && e.execute("link", d.trim());
      }), i;
    });
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class B extends s {
  init() {
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class W extends s {
  static get requires() {
    return [T, B];
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class C extends s {
  static get requires() {
    return [h];
  }
  init() {
    const e = this.editor, t = e.t;
    e.ui.componentFactory.add("skyCmsInsertImage", (o) => this._createSkyCmsButton(c, {
      label: t("From website"),
      withText: !1,
      tooltip: !0
    }));
  }
  afterInit() {
    const e = this.editor, t = e.t, o = e.plugins.get("ImageInsertUI"), i = e.config.get("image.insert.integrations") || [], n = ["upload", "skyCmsWebsite", "url"], a = [
      ...n,
      ...i.filter((l) => !n.includes(l))
    ];
    e.config.set("image.insert.integrations", a), o.registerIntegration({
      name: "upload",
      override: !0,
      observable: () => e.commands.get("uploadImage"),
      buttonViewCreator: () => this._createForwardingButton("imageUpload", {
        label: t("From computer"),
        icon: m,
        withText: !1,
        tooltip: !0
      }),
      formViewCreator: () => this._createForwardingButton("imageUpload", {
        label: t("From computer"),
        icon: m,
        withText: !0,
        closeImageInsertDropdown: !0
      }),
      menuBarButtonViewCreator: () => this._createForwardingMenuBarButton("menuBar:uploadImage", t("From computer"))
    }), o.registerIntegration({
      name: "skyCmsWebsite",
      observable: () => e.commands.get("insertImage"),
      buttonViewCreator: () => this._createSkyCmsButton(c, {
        label: t("From website storage"),
        withText: !1,
        tooltip: !0
      }),
      formViewCreator: () => this._createSkyCmsButton(c, {
        label: t("From website storage"),
        withText: !0,
        closeImageInsertDropdown: !0
      }),
      menuBarButtonViewCreator: () => this._createSkyCmsButton(f, {
        label: t("From website storage"),
        withText: !0
      })
    }), o.registerIntegration({
      name: "url",
      override: !0,
      observable: () => e.commands.get("insertImage"),
      buttonViewCreator: () => this._createForwardingButton("insertImageViaUrl", {
        label: t("From another website"),
        icon: b,
        withText: !1,
        tooltip: !0
      }),
      formViewCreator: () => this._createForwardingButton("insertImageViaUrl", {
        label: t("From another website"),
        icon: b,
        withText: !0,
        closeImageInsertDropdown: !0
      }),
      menuBarButtonViewCreator: () => this._createForwardingMenuBarButton("menuBar:insertImageViaUrl", t("From another website"))
    });
  }
  _createForwardingButton(e, {
    label: t,
    icon: o,
    withText: i = !1,
    tooltip: n = !1,
    closeImageInsertDropdown: a = !1
  } = {}) {
    const l = this.editor, d = l.ui.componentFactory.create(e);
    if (o && (d.icon = o), t && (d.label = t), d.withText = i, d.tooltip = n, a) {
      const g = l.plugins.get("ImageInsertUI");
      this.listenTo(d, "execute", () => {
        g.dropdownView && (g.dropdownView.isOpen = !1);
      });
    }
    return d;
  }
  _createForwardingMenuBarButton(e, t) {
    const o = this.editor.ui.componentFactory.create(e);
    return o.label = t, o.withText = !0, o;
  }
  _createSkyCmsButton(e, {
    label: t,
    withText: o = !1,
    tooltip: i = !1,
    closeImageInsertDropdown: n = !1
  } = {}) {
    const a = this.editor, l = new e(a.locale);
    return l.set({
      label: t,
      icon: k,
      withText: o,
      tooltip: i
    }), this.listenTo(l, "execute", () => {
      if (n) {
        const d = a.plugins.get("ImageInsertUI");
        d.dropdownView && (d.dropdownView.isOpen = !1);
      }
      this._openSkyCmsImagePicker();
    }), l;
  }
  _openSkyCmsImagePicker() {
    const e = this.editor, t = globalThis.window, o = t?.parent?.openInsertImageModel;
    if (o) {
      o(e);
      return;
    }
    const i = t?.prompt, n = i ? i("Enter image URL (example: /images/hero.jpg):", "/images/hero.jpg") : null;
    n && n.trim().length > 0 && e.execute("insertImage", { source: n.trim() });
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class y extends s {
  init() {
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class q extends s {
  static get requires() {
    return [C, y];
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class U extends s {
  init() {
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class _ extends s {
  init() {
    const e = this.editor, t = e.t;
    e.ui.componentFactory.add("skyCmsLink", (o) => {
      const i = F(o);
      return i.buttonView.set({
        label: t("Insert link"),
        icon: p,
        tooltip: !0
      }), x(
        i,
        () => [
          this._createPageLinkButton(o, {
            label: t("From website page"),
            withText: !0,
            dropdownView: i
          }),
          this._createForwardingButton("link", {
            label: t("From another website"),
            withText: !0,
            dropdownView: i
          })
        ],
        {
          isVertical: !0,
          ariaLabel: t("Link source options")
        }
      ), i;
    }), this.editor.ui.componentFactory.add("pageLink", (o) => this._createPageLinkButton(o, {
      label: t("Insert a link to a page on this website."),
      tooltip: !0
    }));
  }
  _createForwardingButton(e, {
    label: t,
    withText: o = !1,
    dropdownView: i = null
  } = {}) {
    const n = this.editor.ui.componentFactory.create(e);
    return t && (n.label = t), n.withText = o, this.listenTo(n, "execute", () => {
      i && (i.isOpen = !1);
    }), n;
  }
  _createPageLinkButton(e, {
    label: t,
    withText: o = !1,
    tooltip: i = !1,
    dropdownView: n = null
  } = {}) {
    const a = new c(e);
    return a.set({
      label: t,
      icon: p,
      withText: o,
      tooltip: i
    }), this.listenTo(a, "execute", () => {
      n && (n.isOpen = !1), this._openPickPageModal();
    }), a;
  }
  _openPickPageModal() {
    const e = globalThis.window, t = e?.parent?.openPickPageModal;
    if (t) {
      t(this.editor);
      return;
    }
    const o = e?.prompt, i = o ? o("Enter relative page URL (example: /about):", "/about") : null;
    i && i.trim().length > 0 && this.editor.execute("link", i.trim());
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class D extends s {
  static get requires() {
    return [U, _];
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class S extends s {
  static get pluginName() {
    return "SignalR";
  }
  init() {
    const e = this.editor, t = (o) => {
      const n = globalThis.window?.parent?.cosmosSignalOthers;
      n ? n(e, o) : console.log(`signalr: ${o}`);
    };
    e.editing.view.document.on("change:isFocused", () => {
      e.editing.view.document.isFocused ? t("focus") : t("blur");
    }), e.editing.view.document.on("keydown", () => {
      t("keydown");
    }), e.editing.view.document.on("mousedown", () => {
      t("mousedown");
    });
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class E extends s {
  init() {
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class A extends s {
  static get requires() {
    return [S, E];
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class V extends s {
  init() {
    const e = this.editor, t = e.t;
    e.ui.componentFactory.add("titleModeIndicator", (o) => {
      const i = new c(o);
      return i.set({
        label: t("Title editor"),
        tooltip: t("Title editing mode. Advanced editor tools are not available here."),
        withText: !0,
        isEnabled: !1
      }), i;
    });
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class z extends s {
  static get requires() {
    return [V];
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class P extends s {
  init() {
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class v extends s {
  static get pluginName() {
    return "VSCodeEditor";
  }
  init() {
    const e = this.editor, t = e.t;
    e.ui.componentFactory.add("vsCodeEditor", (o) => {
      const i = new c(o);
      return i.set({
        label: t("Open code editor."),
        icon: L,
        tooltip: !0
      }), this.listenTo(i, "execute", () => {
        const a = globalThis.window?.parent?.openVsCodeBlockEditor;
        if (a)
          a(e);
        else {
          const l = globalThis.alert;
          l && l("No host VS Code editor bridge detected.");
        }
      }), i;
    });
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
class Q extends s {
  static get requires() {
    return [P, v];
  }
}
/**
 * @license Copyright (c) 2003-2026, CKSource Holding sp. z o.o. All rights reserved.
 * For licensing, see LICENSE.md or https://ckeditor.com/legal/ckeditor-licensing-options
 */
const u = {
  title: {
    toolbar: ["titleModeIndicator"],
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
      "skyCmsLink",
      "fileLink",
      "|",
      "imageInsert",
      "|",
      "bulletedList",
      "numberedList",
      "todoList",
      "|",
      "blockQuote",
      "|",
      "mediaEmbed"
    ],
    balloonToolbar: [
      "bold",
      "italic",
      "underline",
      "|",
      "skyCmsLink"
    ]
  },
  advanced: {
    toolbar: [
      "heading",
      "|",
      "skyCmsLink",
      "imageInsert",
      "resizeImage",
      "imageStyle:inline",
      "toggleImageCaption",
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
      "skyCmsLink",
      "|",
      "bulletedList",
      "numberedList"
    ]
  }
}, M = {
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
function O(r = "standard", {
  tagName: e = null,
  fallbackProfile: t = "standard"
} = {}) {
  const o = e ? e.toLowerCase() : null;
  if (o && /^(h[1-6])$/.test(o))
    return "title";
  const i = typeof r == "string" ? r.toLowerCase() : "", n = M[i] || i;
  return u[n] ? n : u[t] ? t : "standard";
}
function w(r = "standard", e = {}) {
  const t = O(r, e);
  return u[t];
}
function j(r = "standard", e = {}) {
  return w(r, e).toolbar;
}
function K(r = "standard", e = {}) {
  return w(r, e).balloonToolbar;
}
const Y = u;
export {
  W as FileLink,
  q as InsertImage,
  D as PageLink,
  u as SKYCMS_EDITOR_PROFILES,
  M as SKYCMS_EDITOR_PROFILE_ALIASES,
  A as SignalR,
  Y as TOOLBAR_PROFILES,
  z as TitleModeIndicator,
  Q as VSCodeEditor,
  K as getBalloonToolbarProfile,
  w as getEditorProfile,
  j as getToolbarProfile,
  O as resolveEditorProfileName
};
