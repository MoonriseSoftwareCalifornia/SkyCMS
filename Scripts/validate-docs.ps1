param(
    [switch]$IncludeLinkchecker
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Require-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $Name"
    }
}

Write-Host "==> Validating docs source before push..." -ForegroundColor Cyan
Require-Command -Name "python"

$sourceGate = @'
import re
from pathlib import Path
from urllib.parse import urlparse, unquote

docs_root = Path('./Docs').resolve()
ignored_parts = {'_archive', '_site', '_templates', '_Marketing', 'images/screenshots'}
link_re = re.compile(r'\[[^\]]+\]\(([^)]+)\)')

broken = []
for md_file in docs_root.rglob('*.md'):
    rel_md = md_file.relative_to(docs_root).as_posix()
    if any(part in rel_md for part in ignored_parts):
        continue

    content = md_file.read_text(encoding='utf-8', errors='ignore')
    for raw_target in link_re.findall(content):
        target = raw_target.strip()
        if not target:
            continue

        parsed = urlparse(target)
        if parsed.scheme in ('http', 'https', 'mailto', 'tel', 'javascript', 'data'):
            continue

        path_part = unquote(parsed.path or '')
        if not path_part or path_part.startswith('#'):
            continue

        if '.md' not in path_part.lower():
            continue

        if path_part.startswith('/'):
            candidate = (Path('.').resolve() / path_part.lstrip('/')).resolve()
        else:
            candidate = (md_file.parent / path_part).resolve()

        if not candidate.exists():
            broken.append(f"{rel_md} -> {target}")

print(f'Prebuild broken_source_md_links={len(broken)}')
if broken:
    for item in broken[:300]:
        print(item)
    raise SystemExit(1)
'@
python -c $sourceGate

Write-Host "==> Building MkDocs site..." -ForegroundColor Cyan
python -m mkdocs build --config-file mkdocs.yml --site-dir ./site | Out-Null

Write-Host "==> Validating markdown-style link resolution in generated HTML..." -ForegroundColor Cyan
$generatedGate = @'
import re
from pathlib import Path
from urllib.parse import urlparse, unquote

site = Path('./site').resolve()
href_re = re.compile(r'href=["\']([^"\']+)["\']', re.IGNORECASE)

unresolved = []
for html_file in site.rglob('*.html'):
    content = html_file.read_text(encoding='utf-8', errors='ignore')
    for href in href_re.findall(content):
        if '.md' not in href.lower():
            continue

        parsed = urlparse(href)
        if parsed.scheme in ('http', 'https'):
            continue

        path_part = unquote(parsed.path or '')
        if not path_part:
            continue

        if path_part.startswith('/'):
            md_path = (site / path_part.lstrip('/')).resolve()
        else:
            md_path = (html_file.parent / path_part).resolve()

        candidates = [md_path]
        if md_path.suffix.lower() == '.md':
            no_ext = md_path.with_suffix('')
            candidates.append((no_ext / 'index.html').resolve())
            candidates.append(no_ext.with_suffix('.html').resolve())

        if md_path.name.lower() == 'readme.md':
            candidates.append((md_path.parent / 'index.html').resolve())

        if not any(c.exists() for c in candidates):
            unresolved.append(f"{html_file.relative_to(site)} -> {href}")

print(f'Generated unresolved markdown-style links={len(unresolved)}')
if unresolved:
    for item in unresolved[:300]:
        print(item)
    raise SystemExit(1)
'@
python -c $generatedGate

Write-Host "==> Validating anchors in generated HTML..." -ForegroundColor Cyan
$anchorGate = @'
import re
from pathlib import Path
from urllib.parse import urlparse, unquote

broken_anchors = []
site_root = Path('./site').resolve()
html_files = list(site_root.glob('**/*.html'))
ignored_referrers = {Path('images/screenshots/INDEX/index.html')}

for html_file in html_files:
    referrer = html_file.relative_to(site_root)
    if referrer in ignored_referrers:
        continue

    content = html_file.read_text(encoding='utf-8', errors='ignore')
    hrefs = re.findall(r'href=["\']([^"\']*#[^"\']*)["\']', content)

    for href in hrefs:
        parsed = urlparse(href)
        if parsed.scheme in ('http', 'https', 'mailto', 'tel', 'javascript', 'data'):
            continue

        anchor = unquote(parsed.fragment or '')
        if not anchor:
            continue

        path_part = unquote(parsed.path or '')
        if path_part.startswith('/'):
            target_file = (site_root / path_part.lstrip('/')).resolve()
        elif path_part:
            target_file = (html_file.parent / path_part).resolve()
        else:
            target_file = html_file.resolve()

        if target_file.is_dir():
            target_file = (target_file / 'index.html').resolve()
        elif not target_file.exists() and target_file.suffix == '':
            candidate = (target_file / 'index.html').resolve()
            if candidate.exists():
                target_file = candidate

        if not target_file.exists() or target_file.suffix.lower() != '.html':
            continue

        target_content = target_file.read_text(encoding='utf-8', errors='ignore')
        target_ids = set(re.findall(r'id=["\']([^"\']+)["\']', target_content))
        if anchor not in target_ids:
            target_rel = target_file.relative_to(site_root)
            broken_anchors.append(f"{referrer} -> {target_rel}: Missing anchor #{anchor}")

print(f'Broken anchors={len(broken_anchors)}')
if broken_anchors:
    for item in broken_anchors[:300]:
        print(item)
    raise SystemExit(1)
'@
python -c $anchorGate

if ($IncludeLinkchecker) {
    Write-Host "==> Running linkchecker (non-blocking signal in workflow, blocking here)..." -ForegroundColor Cyan
    python -m linkcheck ./site --check-extern --timeout=10 --threads=4 --ignore-url='(^|/)sitemap\.xml\.gz$'
}

Write-Host "✅ Docs validation passed." -ForegroundColor Green
