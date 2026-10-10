# Long prose in most cells

Four columns where three of them carry full sentences.

| Layer | Responsibility | Typical failure | Mitigation |
|-------|----------------|-----------------|------------|
| Domain | Owns the conversion contracts, the format identities and the orchestration that is independent of any delivery mechanism. | A model accidentally references an infrastructure type and the dependency direction is silently inverted. | Keep the project free of package references and review every new public type against the architecture guardrails. |
| Infrastructure | Implements parsing, rendering, file access and process execution behind the abstractions that the domain layer declares. | A renderer change fixes one export format while quietly regressing another format that shares the same code path. | Add regression tests for every consuming format whenever a shared renderer is modified. |
| Presentation | Hosts the editor, the live preview and the export commands, and translates browser events into typed application requests. | State is duplicated between script and managed code, so the two copies drift apart after a failed operation. | Keep one source of truth in managed code and treat the browser as a thin adapter. |
