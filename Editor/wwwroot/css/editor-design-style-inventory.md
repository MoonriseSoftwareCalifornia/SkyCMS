# SkyCMS Editor UI Style Inventory (Views + Pages)

This document is a full sweep of HTML elements and CSS classes used in ./Editor/Views and ./Editor/Pages. Use it as the minimum styling surface area for aligning the editor UI with the product website.

Source folders:
- Editor/Views/**/*.cshtml
- Editor/Pages/**/*.cshtml

Related CSS overrides:
- Editor/wwwroot/css/2024-01-20-01-site.css

## Elements and classes targeted by 2024-01-20-01-site.css
Use this list to see what the custom stylesheet modifies. Anything not listed here should be assumed to use standard Bootstrap styles.

### Elements targeted
`a`, `body`, `h1`, `h2`, `h3`, `h4`, `h5`, `h6`, `input`, `li`, `p`, `small`, `tbody`, `td`, `th`, `thead`, `tr`

### Classes targeted
`active`, `content`, `control`, `control__indicator`, `control--checkbox`, `control--radio`, `cpws-table-pager-row-bottom`, `cpws-table-pager-row-top`, `custom-table`, `cwps-body`, `cwps-editor-container`, `cwps-std-tbl-row-selected`, `dropdown-menu`, `dropdown-submenu`, `dropdown-submenu-left`, `h1`, `h2`, `h3`, `h4`, `h5`, `h6`, `spacer`

### Full selector list (from 2024-01-20-01-site.css)
```
.control input:focus ~ .control__indicator
.cpws-table-pager-row-bottom
.cpws-table-pager-row-top
.custom-table tbody td
.custom-table tbody td small
.custom-table tbody th small
.custom-table tbody tr td
.custom-table tbody tr td a
.custom-table tbody tr td:first-child
.custom-table tbody tr td:last-child
.custom-table tbody tr:hover td
.custom-table tbody tr:hover td a
.custom-table tbody tr:hover th
.custom-table tbody tr:hover th a
.custom-table tbody tr:not(.spacer):hover
.custom-table tbody tr.active td
.custom-table tbody tr.active td a
.custom-table tbody tr.active th a
.custom-table thead th
.cwps-editor-container
.cwps-std-tbl-row-selected
.dropdown-menu .dropdown-submenu
.dropdown-menu .dropdown-submenu-left
.dropdown-menu > li:hover > .dropdown-submenu
.dropdown-menu li
.h1
.h2
.h3
.h4
.h5
.h6
a:hover
body.cwps-body
body.cwps-body .content
body.cwps-body .control
body.cwps-body .control input
body.cwps-body .control input:checked ~ .control__indicator
body.cwps-body .control input:checked ~ .control__indicator:after
body.cwps-body .control input:disabled ~ .control__indicator
body.cwps-body .control__indicator
body.cwps-body .control__indicator:after
body.cwps-body .control--checkbox .control__indicator:after
body.cwps-body .control--checkbox input:disabled ~ .control__indicator:after
body.cwps-body .control--checkbox input:disabled:checked ~ .control__indicator
body.cwps-body .control--radio .control__indicator
body.cwps-body .control:hover input ~ .control__indicator
body.cwps-body .custom-table
body.cwps-body .custom-table tbody th
body.cwps-body .custom-table tbody tr th
body.cwps-body .custom-table tbody tr th a
body.cwps-body .custom-table tbody tr th:first-child
body.cwps-body .custom-table tbody tr th:last-child
body.cwps-body .custom-table tbody tr:not(.spacer)
body.cwps-body .custom-table tbody tr.active th
body.cwps-body .custom-table tbody tr.spacer td
body.cwps-body .custom-table thead tr
body.cwps-body > p
body.cwps-body a
body.cwps-body h1
body.cwps-body h2
h2
h3
h4
h5
h6
```

## Minimum targets (designer focus)
These are the most visible UI surfaces that should be updated to match the product site:
- Global layout: `body.cwps-body`, headings, links, base text, `.content` spacing
- Navigation: `.navbar*`, `.nav*`, `.dropdown*`, `.breadcrumb*`
- Buttons: `.btn`, `.btn-*`, `.btn-group`, `.btn-close`
- Forms: `.form-*`, `.input-group*`, `.invalid-feedback`, `.needs-validation`
- Tables: `.table*`, `.custom-table*`, `.cpws-table-pager-row-*`, `.cwps-std-tbl-row-selected`
- Cards and panels: `.card*`, `.alert*`, `.badge*`, `.toast*`, `.progress*`
- Modals: `.modal*`
- Layout grid: `.container*`, `.row`, `.col-*`, spacing utilities
- Editor containers and CMS-specific UI: `.cwps-editor-container`, `.ccms-*`, `.m-*`, `.gjs-*`, `.k-*`, `.filepond`

## HTML elements in use (standard)
`a`, `article`, `b`, `body`, `br`, `button`, `code`, `div`, `em`, `footer`, `form`, `h1`, `h2`, `h3`, `h4`, `h5`, `head`, `header`, `hr`, `html`, `i`, `iframe`, `img`, `input`, `label`, `li`, `link`, `meta`, `nav`, `ol`, `option`, `p`, `script`, `select`, `small`, `span`, `strong`, `style`, `summary`, `svg`, `table`, `tbody`, `td`, `textarea`, `tfoot`, `th`, `thead`, `title`, `tr`, `u`, `ul`

## Custom elements / UI components
- `cosmos-layout-header`
- `cosmos-layout-footer`
- `kendo-dialog`
- `kendo-switch`
- `popup-animation`

## CSS class inventory (grouped)
Note: Items like `continer` and `modal-fooder` appear in markup and should be reviewed as potential typos.

### Global layout and containers
- `cwps-body`, `cwps-editor-container`, `content`, `container`, `container-fluid`, `row`, `col`, `col-*`, `col-md-*`, `col-sm-*`, `row-cols-*`, `h-100`, `w-100`, `m-auto`, `position-fixed`, `top-0`, `bottom-0`, `end-0`, `rounded`, `rounded-pill`, `shadow-lg`

### Typography and text
- `lead`, `h2`, `text-*`, `fw-*`, `fs-6`, `font-monospace`, `text-truncate`, `text-mute`, `text-muted`, `text-white`

### Links, buttons, and actions
- `btn`, `btn-*`, `btn-group`, `btn-close`, `btn-close-white`, `btn-small`, `btnConfirmEmail`, `btnPublish`, `btnShowSelected`

### Forms and input groups
- `form-*`, `input-group*`, `form-control*`, `form-label`, `form-text`, `invalid-feedback`, `needs-validation`, `control-label`, `gridCheckBox`

### Tables and lists
- `table*`, `custom-table`, `custom-table-responsive`, `cpws-table-pager-row-top`, `cpws-table-pager-row-bottom`, `cwps-std-tbl-row-selected`, `row-click-ckbox`, `article-checkbox`

### Navigation and menus
- `navbar*`, `nav*`, `breadcrumb*`, `dropdown*`, `collapse`, `show`

### Cards, alerts, badges
- `card*`, `alert*`, `badge*`, `note`, `note-title`, `note-text`, `summary*`, `category*`, `check*`

### Modals, toasts, progress, spinners
- `modal*`, `toast*`, `progress*`, `spinner-border`, `spinner-border-sm`, `fade`, `show`

### Layout utilities
- `d-*`, `flex-*`, `justify-content-*`, `align-items-center`, `float-*`, `gap-1`, `p-*`, `pt-*`, `pb-*`, `py-*`, `m*`, `ms-*`, `me-*`, `mt-*`, `mb-*`, `g-*`, `border-0`, `visually-hidden`, `sr-only`, `no-select`

### Icons
- Bootstrap icons: `bi`, `bi-*`
- Font Awesome icons: `fa`, `fa-*`, `fas`, `fa-solid`, `fa-regular`, `fa-brands`

### CMS-specific and editor widgets
- `ccms-*`, `m-*`, `gjs-*`, `k-*`, `filepond`, `uploader`, `perm`, `info-panel-*`, `img-container`, `image-fluid`, `code-tabs`, `icon-settings`, `blog-post`, `blog-content`, `mode`, `footer`, `header`, `display`

### Potential typos to review
- `continer`, `modal-fooder`

### Full class list (alphabetical)
```
active
alert
alert-danger
alert-info
alert-success
alert-warning
align-items-center
article-checkbox
badge
badge-danger
badge-info
badge-primary
badge-secondary
badge-warning
bg-danger
bg-dark
bg-light
bg-secondary
bg-success
bg-warning
bg-white
bi
bi-arrow-left
bi-check
bi-check-circle
bi-clipboard
bi-exclamation-triangle
bi-info-circle
blog-content
blog-post
border-0
bottom-0
breadcrumb
breadcrumb-item
btn
btn-close
btn-close-white
btn-danger
btn-group
btn-info
btn-outline-secondary
btn-primary
btn-secondary
btn-sm
btn-small
btn-success
btn-warning
btnConfirmEmail
btnPublish
btnShowSelected
card
card-body
card-group
card-header
card-img-top
card-text
card-title
category
category-title
ccms-chat-sender
ccms-chat-toast-trigger
ccms-clip-board
ccms-img-widget-container
ccms-img-widget-img
ccms-typing-indicator
check
check-description
check-details
check-header
check-icon
check-message
check-name
check-title
close
code-tabs
col
col-1
col-11
col-12
col-3
col-4
col-6
col-8
col-9
col-auto
col-md-10
col-md-12
col-md-2
col-md-4
col-md-6
col-md-8
col-sm-10
collapse
container
container-fluid
content
continer
control-label
cpws-table-pager-row-bottom
cpws-table-pager-row-top
custom-table
custom-table-responsive
cwps-body
cwps-editor-container
d-block
d-flex
d-inline-block
d-inline-flex
display
dropdown
dropdown-divider
dropdown-item
dropdown-menu
dropdown-menu-dark
dropdown-toggle
end-0
fa
fa-angle-double-right
fa-arrow-rotate-left
fa-arrows-left-right
fa-arrows-rotate
fa-backward-fast
fa-bell-concierge
fa-binoculars
fa-brands
fa-calendar-day
fa-caret-left
fa-caret-right
fa-check
fa-circle-arrow-right
fa-circle-check
fa-circle-exclamation
fa-circle-info
fa-circle-question
fa-clipboard
fa-cloud
fa-cloud-arrow-down
fa-cloud-arrow-up
fa-code
fa-cog
fa-copy
fa-exclamation-triangle
fa-eye
fa-file-export
fa-forward-fast
fa-house
fa-image
fa-info-circle
fa-key
fa-magnifying-glass
fa-mailchimp
fa-paper-plane
fa-pen-to-square
fa-pencil
fa-regular
fa-right-from-bracket
fa-rotate
fa-solid
fa-trash
fa-trash-can-arrow-up
fa-triangle-exclamation
fa-user
fa-user-pen
fa-users
fa-wand-magic-sparkles
fa-xmark
fade
fas
filepond
flex-row
flex-row-reverse
float-end
float-right
font-monospace
footer
form-check
form-check-input
form-check-label
form-control
form-control-sm
form-group
form-inline
form-label
form-switch
form-text
fs-6
fw-bold
fw-lighter
g-3
g-4
gap-1
gjs-four-color
gjs-logo
gjs-logo-cont
gjs-logo-version
gjs-sm-properties
gjs-sm-sector
gjs-sm-sector-label
gjs-sm-sector-title
gridCheckBox
h-100
h2
header
icon-settings
image-fluid
img-container
img-fluid
info-panel-label
info-panel-link
info-panel-logo
input-group
input-group-sm
input-group-text
invalid-feedback
justify-content-between
justify-content-center
justify-content-end
k-bubble
k-button
k-button-md
k-button-rectangle
k-button-solid
k-button-solid-base
k-message
k-rounded-md
k-typing-indicator
lead
m-auto
m-editor-container
m-fileselector-container
mb-0
mb-2
mb-3
mb-4
mb-5
mb-lg-0
me-1
me-2
me-3
me-auto
modal
modal-body
modal-content
modal-dialog
modal-dialog-centered
modal-fooder
modal-footer
modal-fullscreen
modal-header
modal-lg
modal-title
modal-xl
mode
ms-1
ms-2
ms-3
ms-5
mt-1
mt-2
mt-3
mt-4
mt-5
nav
nav-item
nav-link
nav-tabs
navbar
navbar-brand
navbar-collapse
navbar-dark
navbar-expand-lg
navbar-expand-sm
navbar-nav
navbar-text
navbar-toggler
navbar-toggler-icon
needs-validation
no-select
note
note-text
note-title
p-0
p-3
pb-3
pb-4
perm
position-fixed
progress
progress-bar
progress-bar-animated
progress-bar-striped
pt-2
pt-3
py-4
rounded
rounded-pill
row
row-click-ckbox
row-cols-1
row-cols-md-4
shadow-lg
show
spinner-border
spinner-border-sm
sr-only
summary
summary-content
summary-icon
summary-text
table
table-dark
table-hover
table-responsive
table-striped
text-bg-dark
text-bg-primary
text-bg-secondary
text-center
text-danger
text-dark
text-light
text-mute
text-muted
text-primary
text-success
text-truncate
text-warning
text-white
toast
toast-body
toast-container
toast-header
top-0
uploader
visually-hidden
w-100
```
