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
    hrefs = re.findall(r'href=[\"\']([^\"\']*#[^\"\']*)[\"\']', content)
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
        target_ids = set(re.findall(r'id=[\"\']([^\"\']+)[\"\']', target_content))
        if anchor not in target_ids:
            target_rel = target_file.relative_to(site_root)
            broken_anchors.append((str(referrer), str(target_rel), anchor, href))

print('count', len(broken_anchors))
for item in broken_anchors:
    print(repr(item))