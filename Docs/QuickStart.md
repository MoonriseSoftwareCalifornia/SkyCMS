---
title: Installation Quick Start
description: Get SkyCMS up and running quickly with the setup wizard for single-tenant installations
keywords: quickstart, setup, wizard, installation, Docker, Azure, getting-started
audience: [developers, administrators]
version: 2.0
updated: 2025-12-20
canonical: /QuickStart.html
aliases: ["5-Minute Quick Start"]
scope:
  platforms: [local, azure]
  tenancy: [single]
status: stable
chunk_hint: 320
---

# Installation Quick Start

Use the new setup wizard for **single-tenant** installs. It walks you through storage, admin user, publisher URL, optional email, and optional CDN. Set `CosmosAllowSetup=true` and make sure `ConnectionStrings:ApplicationDbContextConnection` points to a reachable database before you begin.

![Setup wizard welcome screen](images/screenshots/setup-wizard-welcome.webp)

## When to use this
- You want the fastest way to try SkyCMS (single-tenant) locally or on Azure.
- You prefer a guided wizard instead of manual env-var configuration.

## Why this matters
- Minimizes setup friction and lets you validate the product in minutes.
- Ensures prerequisites (database + storage) are in place before you invest more time.

## Key takeaways
- Wizard is **single-tenant only**; multi-tenant uses DynamicConfig with `CosmosAllowSetup=false`.
- Wizard disables itself after completion; re-enable only when needed.
- Works locally via Docker or via the Azure one-click deploy.

## Prerequisites
- Single-tenant intent; for multi-tenant, use DynamicConfig instead of the wizard.
- Database reachable from the container (SQLite for local trials; Azure SQL/Cosmos DB/MySQL for cloud).
- For Azure one-click: access to an Azure subscription and permissions to deploy.

## Quick path
1. Run local Docker or deploy to Azure using the main README button.
2. Browse to `/Setup` and complete the wizard (Storage → Admin → Publisher → optional Email/CDN → Review).
3. Restart when prompted; sign in with the admin you created.

![Storage configuration step](images/screenshots/setup-wizard-storage.webp)

## Key facts {#key-facts}

- Wizard is for single-tenant setups; multi-tenant uses DynamicConfig, not the wizard.
- Requires `CosmosAllowSetup=true` and a reachable database connection string.
- Wizard disables itself after completion; restart when prompted.
- Supports optional email and CDN configuration during setup.

## Local trial (Docker) {#local-trial}

```bash
# No cloud account required; enables the wizard for single-tenant setup
docker run -d -p 8080:8080 \
  -e CosmosAllowSetup=true \
  -e ConnectionStrings__ApplicationDbContextConnection="Data Source=/data/skycms.db" \
  toiyabe/sky-editor:latest
```

1) Open `http://localhost:8080/Setup`
2) Complete the wizard: Storage → Admin → Publisher → (optional) Email → (optional) CDN → Review → Complete
3) Restart the container when prompted, then sign in with the admin account you just created.

## Azure (one-click deploy) {#azure-one-click}

1) Use the **Deploy to Azure** button in the main README to provision resources.
2) Browse to your Editor site, e.g., `https://<editor-app>.azurewebsites.net/Setup`.
3) Run the same single-tenant setup wizard steps above. Azure deploys with the wizard enabled; if you plan to run multi-tenant, leave `CosmosAllowSetup=false` and follow the DynamicConfig docs instead.

## Wizard checklist {#wizard-checklist}

- Storage: Azure Blob / S3 / R2 connection string and public/CDN URL (validated).
- Admin: email + strong password for the first Administrator account.
- Publisher: public URL, site title, starter design, optional auth requirement.
- Email (optional): SendGrid, Azure Communication Services, or SMTP; test send available.
- CDN (optional): Azure CDN/Front Door, Cloudflare, or Sucuri.
- Review & complete: confirm, finish, restart app to apply. Wizard disables itself after completion.

![Review and complete step](images/screenshots/setup-wizard-review.webp)

## FAQ {#faq}

- **Can I use the wizard for multi-tenant?** No. Keep `CosmosAllowSetup=false` and configure DynamicConfig for multi-tenant deployments.
- **Which database works for the wizard?** Any supported provider reachable from the container (SQLite for local trial, Azure SQL/Cosmos DB/MySQL for cloud).
- **How do I rerun the wizard?** Temporarily set `CosmosAllowSetup=true`, restart, rerun `/Setup`, then set it back to false.

<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "HowTo",
  "name": "SkyCMS Quick Start (Local Docker)",
  "description": "Run SkyCMS locally with the setup wizard enabled for single-tenant installs.",
  "step": [
    {"@type": "HowToStep", "name": "Run container", "text": "Execute the docker run command with CosmosAllowSetup=true and a SQLite connection string."},
    {"@type": "HowToStep", "name": "Open wizard", "text": "Visit http://localhost:8080/Setup to start the setup wizard."},
    {"@type": "HowToStep", "name": "Complete steps", "text": "Provide storage, admin user, publisher URL, optional email, and optional CDN details, then review."},
    {"@type": "HowToStep", "name": "Restart and sign in", "text": "Restart the container when prompted and sign in with the admin account you created."}
  ]
}
</script>

<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "FAQPage",
  "mainEntity": [
    {"@type": "Question", "name": "Can I use the wizard for multi-tenant?", "acceptedAnswer": {"@type": "Answer", "text": "No. The wizard is for single-tenant. Leave CosmosAllowSetup=false and configure DynamicConfig for multi-tenant."}},
    {"@type": "Question", "name": "Which database works for the wizard?", "acceptedAnswer": {"@type": "Answer", "text": "Any supported provider reachable from the container: SQLite for local trials, Azure SQL, Cosmos DB, or MySQL for cloud."}},
    {"@type": "Question", "name": "How do I rerun the wizard?", "acceptedAnswer": {"@type": "Answer", "text": "Temporarily set CosmosAllowSetup=true, restart, rerun /Setup, then set it back to false after completion."}}
  ]
}
</script>
