#!/usr/bin/env node

const crypto = require("crypto");
const fs = require("fs");
const path = require("path");

const diffFile = process.argv[2];

if (!diffFile) {
  console.error("Usage: import-docs.js <diff-file>");
  process.exit(1);
}

const apiUrl = process.env.SKYCMS_API_URL;
const apiKey = process.env.SKYCMS_API_KEY;
const tenantHost = process.env.SKYCMS_TENANT_HOST;

if (!apiUrl || !apiKey) {
  console.error("SKYCMS_API_URL and SKYCMS_API_KEY are required.");
  process.exit(1);
}

const mapPath = path.resolve(".skycms", "docs-import-map.json");
const mapData = loadMap(mapPath);

const diffLines = fs
  .readFileSync(diffFile, "utf8")
  .split(/\r?\n/)
  .map((line) => line.trim())
  .filter(Boolean);

const md = tryGetMarkdownParser();

(async () => {
  for (const line of diffLines) {
    const parsed = parseDiffLine(line);
    if (!parsed) {
      continue;
    }

    if (parsed.type === "delete") {
      const sourceKey = toSourceKey(parsed.path);
      await deleteItem(sourceKey);
      delete mapData.items[sourceKey];
      continue;
    }

    if (parsed.type === "rename") {
      await renameItem(parsed.oldPath, parsed.newPath);
      const oldKey = toSourceKey(parsed.oldPath);
      const newKey = toSourceKey(parsed.newPath);
      if (mapData.items[oldKey]) {
        mapData.items[newKey] = mapData.items[oldKey];
        delete mapData.items[oldKey];
      }
      continue;
    }

    if (parsed.type === "upsert") {
      const sourceKey = toSourceKey(parsed.path);
      const markdown = fs.readFileSync(parsed.path, "utf8");
      const hash = sha256(markdown);

      if (mapData.items[sourceKey]?.hash === hash) {
        continue;
      }

      const html = md.render(markdown);
      const urlPath = toUrlPath(sourceKey);

      await upsertItem(sourceKey, {
        title: deriveTitle(parsed.path, markdown),
        urlPath,
        html,
        templateKey: "docs-page",
        published: true,
        source: {
          path: parsed.path.replace(/\\/g, "/"),
          hash: hash
        }
      });

      mapData.items[sourceKey] = {
        hash,
        lastSyncedUtc: new Date().toISOString()
      };
    }
  }

  saveMap(mapPath, mapData);
})().catch((error) => {
  console.error(error);
  process.exit(1);
});

function parseDiffLine(line) {
  const parts = line.split("\t");
  if (parts.length < 2) {
    return null;
  }

  const status = parts[0];

  if (status.startsWith("R") && parts.length >= 3) {
    return { type: "rename", oldPath: parts[1], newPath: parts[2] };
  }

  if (status === "D") {
    return { type: "delete", path: parts[1] };
  }

  if (status === "A" || status === "M") {
    return { type: "upsert", path: parts[1] };
  }

  return null;
}

function toSourceKey(filePath) {
  const normalized = filePath.replace(/\\/g, "/");
  const rel = normalized.toLowerCase().startsWith("docs/") ? normalized.slice(5) : normalized;
  return `docs/${rel}`.toLowerCase();
}

function toUrlPath(sourceKey) {
  let urlPath = sourceKey.replace(/^docs\//, "docs/");
  urlPath = urlPath.replace(/\/index\.md$/i, "");
  urlPath = urlPath.replace(/\.md$/i, "");
  return urlPath;
}

function sha256(content) {
  return `sha256:${crypto.createHash("sha256").update(content).digest("hex")}`;
}

function deriveTitle(filePath, markdown) {
  const baseName = path.basename(filePath, path.extname(filePath));
  const match = markdown.match(/^#\s+(.+)$/m);
  return match ? match[1].trim() : baseName;
}

function tryGetMarkdownParser() {
  try {
    const MarkdownIt = require("markdown-it");
    return new MarkdownIt();
  } catch (error) {
    console.error("markdown-it is required. Install with: npm install -g markdown-it");
    throw error;
  }
}

function loadMap(filePath) {
  if (!fs.existsSync(filePath)) {
    return { items: {} };
  }

  return JSON.parse(fs.readFileSync(filePath, "utf8"));
}

function saveMap(filePath, data) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, JSON.stringify(data, null, 2));
}

async function upsertItem(sourceKey, payload) {
  const response = await fetch(`${apiUrl}/_api/import/docs/items/${encodeURIComponent(sourceKey)}`, {
    method: "PUT",
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "Content-Type": "application/json",
      "x-origin-hostname": tenantHost || ""
    },
    body: JSON.stringify(payload)
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Upsert failed: ${response.status} ${text}`);
  }
}

async function deleteItem(sourceKey) {
  const response = await fetch(`${apiUrl}/_api/import/docs/items/${encodeURIComponent(sourceKey)}`, {
    method: "DELETE",
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "x-origin-hostname": tenantHost || ""
    }
  });

  if (!response.ok && response.status !== 404) {
    const text = await response.text();
    throw new Error(`Delete failed: ${response.status} ${text}`);
  }
}

async function renameItem(oldPath, newPath) {
  const response = await fetch(`${apiUrl}/_api/import/docs/rename`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "Content-Type": "application/json",
      "x-origin-hostname": tenantHost || ""
    },
    body: JSON.stringify({
      fromPath: toSourceKey(oldPath),
      toPath: toSourceKey(newPath)
    })
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Rename failed: ${response.status} ${text}`);
  }
}
