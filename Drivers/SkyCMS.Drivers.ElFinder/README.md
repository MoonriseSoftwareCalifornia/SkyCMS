# SkyCMS.Drivers.ElFinder

elFinder protocol adapter driver for SkyCMS. Implements the [elFinder 2.1 API specification](https://github.com/Studio-42/elFinder/wiki) with SkyCMS storage and tenant-awareness.

## Purpose

This driver bridges the gap between the elFinder UI (JavaScript file browser) and SkyCMS's backend storage and service layer. It:

- **Translates** elFinder protocol commands (open, ls, mkdir, rm, upload, etc.) into SkyCMS business operations
- **Adapts** SkyCMS storage responses into elFinder 2.1 JSON format
- **Enforces** API contract compliance to ensure UI/backend synchronization
- **Isolates** integration logic from controllers for testability and maintainability

## Architecture

### Design Pattern: CQRS + Adapter

```
elFinder UI
    ↓ (JSON protocol)
ElFinderConnectorController
    ↓ (dispatches)
ElFinder CQRS Commands (MediatR)
    ↓ (handles)
ElFinderCommandHandlers
    ↓ (delegates to)
IElFinderStorageAdapter
    ↓ (adapts)
IStorageContext (SkyCMS backend)
```

### Key Components

- **Commands & Handlers**: CQRS pattern for each elFinder operation
- **DTOs**: Strongly-typed response models matching elFinder 2.1 spec exactly
- **Storage Adapter**: Abstraction layer insulating driver from storage implementation
- **Utilities**: Path encoding/decoding, MIME type resolution, validation

## Documentation

- [RESEARCH.md](./RESEARCH.md) — API spec analysis, findings, design decisions
- [DESIGN.md](./DESIGN.md) — Architecture, command mapping, DTO design
- [API_SPEC.md](./API_SPEC.md) — elFinder 2.1 API reference (generated from research)

## Development Status

🚀 **Phase 0**: Project setup and documentation framework
📋 **Phase 1**: Research and spec audit (in progress)
🔨 **Phase 2**: Driver architecture and design
⚙️ **Phase 3**: Core command implementation and testing
🎯 **Phase 4**: Controller integration and legacy consolidation

## Usage (TBD)

Once implemented, the driver will be used like:

```csharp
// In ElFinderConnectorController
var command = new ElFinderOpenCommand { Target = hash, IsInit = true };
var response = await mediator.Send(command);
return Json(response);
```

## License

MIT License. See [LICENSE](./LICENSE) for details.

---

**Maintained by:** Moonrise Software, LLC  
**Repository:** https://github.com/CWALabs/SkyCMS  
**Related ADR:** ADR-0035 (elFinder File Explorer Modernization)
