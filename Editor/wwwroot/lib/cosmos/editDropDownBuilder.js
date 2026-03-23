/*

Builds a standard dropdown list for editing a property. The dropdown list is built based on the options provided in the property definition.

*/
function ccms___buildEditDropDownMenu(articleNumber, usesPageEditor, buttonText) {
    const template = document.createElement("template");

    template.innerHTML = `
        <div class="dropdown">
            <button class="btn btn-secondary dropdown-toggle" type="button" data-bs-toggle="dropdown" aria-expanded="false">
                ${buttonText}
            </button>
            <ul class="dropdown-menu"></ul>
        </div>`;

    const dropdown = template.content.firstElementChild;
    const list = dropdown.querySelector(".dropdown-menu");

    const visualEditor = ccms___buildEditDropDownMenuItem(articleNumber, "Edit");
    const designer = ccms___buildEditDropDownMenuItem(articleNumber, "Designer");
    const editCode = ccms___buildEditDropDownMenuItem(articleNumber, "EditCode");

    // Disable visual editor option when page editor support is not enabled.
    if (!usesPageEditor) {
        const visualEditorLink = visualEditor.querySelector("a");
        visualEditorLink.classList.add("disabled");
        visualEditorLink.setAttribute("aria-disabled", "true");
        visualEditorLink.setAttribute("role", "button");
        visualEditor.title = "Visual editing is not available for this page.";
        visualEditorLink.href = "#";
        visualEditorLink.innerHTML += '<i class="ms-2 text-danger fa-solid fa-ban"></i>';
    }

    list.appendChild(visualEditor);
    list.appendChild(designer);
    list.appendChild(editCode);

    return dropdown;
}

/**
 * Builds one editor menu item.
 * @param {string|number} articleNumber Article identifier used in editor URLs.
 * @param {"Edit"|"Designer"|"EditCode"} editorType Type of editor destination.
 * @returns {HTMLLIElement} Menu item element.
 */
function ccms___buildEditDropDownMenuItem(articleNumber, editorType) {
    const settings = {
        Edit: {
            title: "Visually edit content much like a word processor.",
            text: "Visual Editor",
            iconClasses: "fa-regular fa-pen-to-square me-2"
        },
        Designer: {
            title: "Build more complex pages by drag-and-drop components onto the page.",
            text: "Page Builder",
            iconClasses: "fa-solid fa-table-cells me-2"
        },
        EditCode: {
            title: "Edit the code on a web page (HTML, JavaScript, CSS).",
            text: "Code Editor",
            iconClasses: "fa-solid fa-code me-2"
        }
    };

    const selectedEditor = settings[editorType] || settings.EditCode;

    const listItem = document.createElement("li");
    const link = document.createElement("a");
    const icon = document.createElement("i");

    icon.className = selectedEditor.iconClasses;

    link.title = selectedEditor.title;
    link.className = "dropdown-item";
    link.href = `/Editor/${editorType}/${encodeURIComponent(articleNumber)}`;
    link.appendChild(icon);
    link.append(document.createTextNode(selectedEditor.text));

    listItem.appendChild(link);

    return listItem;
}