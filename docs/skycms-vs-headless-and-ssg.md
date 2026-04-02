# SkyCMS vs Headless CMS and Static Site Generator Workflows

This document provides a technical comparison for teams evaluating SkyCMS against two common alternatives:

- Headless CMS with a separate frontend application
- Git-based static site generator workflows

## Summary

SkyCMS is a page-rendering CMS with two delivery modes:

- Dynamic rendering mode (server-side)
- Static publishing/proxy mode (pre-rendered artifacts)

This allows one platform to support both dynamic and static delivery models without requiring separate CMS and build-chain products.

## Architecture Comparison

| Dimension | SkyCMS | Headless CMS + Frontend | Static Site Generator + Git |
| --- | --- | --- | --- |
| Primary output | Complete HTML pages | API content consumed by app | Pre-built static files |
| Authoring | Integrated CMS editor experience | CMS authoring + separate frontend ownership | Authoring often tied to Markdown/Git workflow |
| Delivery modes | Dynamic and static proxy modes | Usually dynamic app runtime | Static only |
| Build/deploy chain for content changes | In-platform publish workflow | Often CI/CD plus frontend deployment | CI/CD build and deploy required |
| Multi-tenant domain routing | First-class in core config model | Product-dependent, often custom | Usually custom |
| Role-based editorial workflow | Built-in roles and publishing controls | CMS-dependent | Usually external tooling/process |
| Setup for non-developers | Setup wizard and admin workflows | Depends on stack composition | Usually higher operational barrier |

## Where SkyCMS Is Strong

- Teams that want page-first delivery with strong SEO defaults.
- Teams that need both dynamic behavior and static delivery options.
- Teams that want an integrated content workflow instead of stitching CMS + frontend + build orchestration.
- Multi-tenant scenarios where host/domain determines tenant context.
- Organizations with mixed roles (developers, designers/site builders, editors, admins) working in one platform.

## Where Alternatives May Fit Better

- Frontend teams that require a fully custom SPA/SSR app stack as the primary product and already have mature frontend platform operations.
- Teams standardized on Git-based editorial workflows where non-technical editing is not a core requirement.
- Cases where content is consumed mainly by many external client applications and page rendering is not a priority.

## Operational Tradeoff Notes

SkyCMS reduces the number of moving parts for many website-focused teams, but tradeoffs still exist:

- You should still plan deployment, monitoring, backup, and role governance.
- Static and dynamic modes require clear operational decisions per environment.
- Multi-tenant deployments require disciplined domain and configuration management.

## Related Documents

- Self-host quick start: [docs/self-host-quick-start.md](./self-host-quick-start.md)
- Editor details: [Editor/README.md](../Editor/README.md)
- Publisher details: [Publisher/README.md](../Publisher/README.md)
- Multi-tenant config details: [Cosmos.ConnectionStrings/README.md](../Cosmos.ConnectionStrings/README.md)
