# ADR 0000: Foundational Product Direction - Cloud-Native Multi-Mode CMS

## Status

Accepted

## Context

SkyCMS exists to solve a foundational product problem: many traditional CMS platforms force an undesirable tradeoff between editorial usability, runtime performance, operational simplicity, and deployment flexibility.

Historically, CMS platforms have often leaned too heavily toward one or two of these qualities:

- strong editorial tooling but heavy, slow, operationally expensive runtimes
- high-performance static delivery but weak authoring workflows
- headless content APIs without an integrated publishing experience for full websites
- tightly coupled monoliths that are hard to scale, hard to harden, or expensive to operate

The original CosmosCMS vision, carried forward into SkyCMS, was to build a CMS that could:

1. outperform classic CMS platforms in speed, capacity, and stability
2. remain usable by both web developers and non-technical content editors
3. stay easy to administer and relatively low cost to operate
4. run in static, decoupled, and headless modes without abandoning one coherent platform

This is not a narrow implementation choice. It is the product-level architectural premise that explains many later decisions in the system.

## Historical Note

SkyCMS is the continuation and evolution of CosmosCMS.

This ADR intentionally preserves the original CosmosCMS product thesis as SkyCMS's foundational direction, while allowing implementation details, naming, and deployment practices to evolve over time.

In that sense, this decision is both:

- retrospective (capturing why the platform was created)
- forward-looking (serving as a standing decision contract for future architecture work)

## Design Goals

This decision aims to:

1. define SkyCMS as a cloud-native CMS optimized for speed, scale, and operational simplicity
2. preserve a strong integrated editing experience for non-technical and technical users alike
3. support multiple content delivery models from one product family
4. treat static delivery as a first-class capability rather than an afterthought
5. keep runtime roles and deployment topologies flexible enough for high-capacity and high-availability use cases

## Non-Goals

This decision does not attempt to:

- require every deployment to use every delivery mode
- require every environment to use Azure-specific infrastructure
- define the complete implementation of Editor, Publisher, or API boundaries
- prescribe one mandatory frontend rendering strategy for all tenants
- replace later ADRs that define concrete runtime and storage contracts

## Decision

SkyCMS is defined as a cloud-native, multi-mode CMS platform whose primary architectural purpose is to combine:

- high-performance content delivery
- low-friction content authoring
- low-cost, operationally efficient deployment options
- multiple serving models, including static, decoupled, and headless delivery

This means subsequent architectural decisions should favor solutions that preserve these core product characteristics, even when they introduce some implementation complexity.

In practical terms, this foundational direction establishes that:

- static delivery is a first-class design target
- decoupled serving is a first-class design target
- headless/API-oriented delivery is a first-class design target
- editorial usability is a first-class design target
- cloud-native storage and hosting patterns are preferred when they strengthen performance, scale, or operating simplicity

## Detailed Rationale

### Performance Is A Product Feature

SkyCMS is intended for high-capacity, content-heavy, and burst-sensitive websites. Performance is not merely an optimization concern; it is part of the product promise.

This is why static publishing, storage-backed asset delivery, CDN integration, and lightweight serving paths matter so much in the architecture.

### Editorial Usability Must Survive Architectural Ambition

Many high-performance or headless-first systems shift too much burden onto developers. SkyCMS deliberately keeps integrated authoring tools central to the product so non-technical editors can create and maintain sites with minimal training.

### Multi-Mode Delivery Avoids False Tradeoffs

Different teams and environments need different delivery models. Some want static-first delivery for scale and cost. Some need decoupled serving. Some need headless APIs for multi-channel delivery. SkyCMS exists to support these without forcing teams onto separate products.

### Operational Simplicity Matters

Low-cost and low-administration operation is part of the original value proposition. Decisions that reduce runtime burden, support static hosting, simplify scaling, or isolate responsibilities are consistent with the platform's purpose.

## Alternatives Considered

### Traditional Monolithic CMS

Rejected because it does not align well with the goals of high performance, flexible delivery modes, and low operational burden.

### Pure Headless CMS

Rejected because it weakens the integrated website publishing and authoring experience that SkyCMS is intended to provide.

### Static Site Generator Only

Rejected because it cannot, by itself, satisfy the broader product goal of supporting dynamic, decoupled, and headless scenarios from one platform.

### Separate Products Per Delivery Model

Rejected because it fragments the authoring experience, increases maintenance burden, and undermines the goal of one coherent CMS platform serving multiple operational shapes.

## Consequences

### Positive Outcomes

- creates a clear north star for later ADRs
- justifies static, decoupled, and headless capabilities as intentional design goals
- supports editorial tooling investment alongside delivery-performance investment
- encourages deployment and runtime flexibility across varied customer needs

### Constraints Introduced

- architecture must balance multiple delivery models rather than optimizing for only one
- documentation must explain mode-specific behaviors clearly
- new features should be evaluated for whether they preserve both usability and operational efficiency
- platform boundaries may be more complex than in a single-mode CMS

## Relationship To Later ADRs

This ADR is intentionally foundational.

Later ADRs should be understood as refinements or concrete implementations of this core product direction, including decisions such as:

- separate Editor and Publisher runtimes
- dynamic vs static publisher operating modes
- shared storage and publishing artifacts
- tenant-aware configuration and isolation
- storage-backed static delivery and CDN-oriented content paths

## Evidence

- Original product goals documented in the archived CosmosCMS README:
  - speed, capacity, and stability
  - usability for developers and non-technical editors
  - low-cost operation
  - static, decoupled, and headless modes
- Related implementation ADRs already present in this repository:
  - docs/adr/0006-publisher-operating-modes-dynamic-vs-static.md
  - docs/adr/0024-editor-publisher-separation-with-shared-data-and-storage.md