# Architecture Guardrails

This document records stable dependency and ownership rules. It does not describe current task status. The feature inventory and solution map remain in the root README.

## Dependency direction

```text
Core ──> nothing
Infrastructure ──> Core
WebApp.Core ──> Core
CLI ──> Core + Infrastructure
WebApp ──> WebApp.Core + Core + Infrastructure
Desktop ──> Core + Infrastructure + built WebApp assets
```

Dependencies point inward. Outer projects compose or adapt inner behavior; inner projects must not learn about a frontend, browser, filesystem, process implementation, or DI container.

## Core

`MarkdownConverter.Core` owns conversion contracts, domain models, format identities, and application orchestration that is independent of a delivery mechanism.

- It must keep zero project and NuGet references.
- External behavior is represented through focused interfaces.
- Models must not contain Blazor, OpenXml, Markdig, filesystem, process, HTTP, or JavaScript types.
- Exceptions and result types should express stable domain/application outcomes rather than infrastructure messages.

## Infrastructure

`MarkdownConverter.Infrastructure` owns concrete parsing, conversion, filesystem, and process behavior.

- NuGet-dependent implementations stay here.
- Implementations depend on Core contracts, not on a UI.
- Format-specific behavior should remain behind `IFormatConverter` and keyed DI registration.
- Shared renderer changes require regression tests across every consuming format.
- Infrastructure details must not leak through Core public contracts without an explicit architectural decision.

## WebApp.Core and PVM

`MarkdownConverter.WebApp.Core` owns testable presentation behavior and UI state.

- It depends only on Core and contains no Blazor or `IJSRuntime` references.
- Presenters receive narrow capability ports and update view models or plain C# view interfaces.
- Per-document state must be keyed by stable document identity rather than tab position or filename.
- Async operations that can overlap must define serialization, cancellation, or stale-result rejection.
- Browser exceptions are translated at the adapter boundary; presenters consume typed outcomes.

## WebApp browser boundary

`MarkdownConverter.WebApp` is an outer adapter and composition root.

Legitimate browser-only responsibilities include textarea selection and geometry, native undo-preserving edits, high-frequency scroll synchronization, file pickers/downloads, object URLs, clipboard, printing/windows, local storage, OS file-drop reading, and KaTeX invocation.

Application state, search/replacement rules, extension routing, command decisions, HTTP orchestration, validation, and status formatting belong in C# whenever they do not require a browser API.

- Razor components should depend on typed capability interfaces rather than named global JavaScript functions.
- Retained event listeners and `DotNetObjectReference` instances require explicit ownership and disposal.
- High-frequency geometry should remain local to JavaScript when a .NET round trip would harm responsiveness.
- JavaScript must not become a second source of application truth.

The detailed retained-browser inventory belongs in a dedicated browser-boundary document once the final capability ports are established.

## Desktop boundary

`MarkdownConverter.Desktop` owns Windows/WebView2 hosting, loopback endpoints, single-instance coordination, local process access, and Desktop-specific file-open behavior.

- Shared UI behavior remains in WebApp/WebApp.Core.
- Loopback HTTP contracts should use typed request/response models and cancellation.
- Desktop-only failures must not break standalone WASM behavior.
- Windows integration changes require focused helper tests plus an appropriate smoke check.

## Composition roots and DI

DI registration belongs in `ServiceCollectionExtensions` composition methods or the executable startup project.

- Use the narrowest correct lifetime.
- Remember that scoped services have application lifetime in Blazor WebAssembly.
- Do not resolve arbitrary services from `IServiceProvider` outside an intentional factory/composition boundary.
- Add a composition test whenever a change introduces or replaces capability registrations.
- Do not add mocks, test doubles, or conditional test behavior to production assemblies.

## Change design

Apply these rules in order:

1. State the observable defect or required outcome.
2. Identify the existing owner and nearest valid seam.
3. Make the smallest cohesive change at that seam.
4. Prove behavior at the cheapest sufficient test layer.
5. Preserve unrelated contracts and defer incidental cleanup.

File length, stylistic preference, or theoretical reuse alone does not justify a new abstraction or refactor. Cross-cutting decisions that future work must preserve belong in an ADR under `docs/architecture/decisions/`.
