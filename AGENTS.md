# Repository Guidelines

## Project Structure & Module Organization
- Engine core: `src/helengine.core/` (systems, memory, IO, timing).
- DX11 renderer: `src/helengine.graphics.dx11/` (SharpDX/Direct3D 11; Windows‑only).
- Editor launcher: `helengine.ui/helengine.launcher/` — Avalonia driven UI, no connections to engine.
- Test projects: under `tests/` and/or adjacent `**/*.Tests/` folders, named `Helengine.*.Tests` to mirror target modules.
- Support: `assets/`, `scripts/`, CI in `.github/workflows/`.

## Build, Test, and Development Commands
- Requires .NET 9 SDK (win‑x64) and DirectX 11 GPU.
- Build: `dotnet build -c Debug`
- Run editor: `dotnet run -c Debug --project helengine.ui/helengine.editor.launcher`
- Test all: `dotnet test -c Debug`
- Test single project: `dotnet test tests/Helengine.Core.Tests/Helengine.Core.Tests.csproj`
- Publish (Windows, optimized):
  `dotnet publish helengine.ui/helengine.editor.launcher -c Release -r win-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true -p:ReadyToRun=true`

## Coding Style & Naming Conventions
- C# 4‑space indent; file‑scoped namespaces; nullable enabled.
- Names: classes/methods `PascalCase`; fields `_camelCase`; constants `UPPER_SNAKE_CASE`.
- Memory‑first: prefer stack/`Span<T>`; reuse via `ArrayPool<T>`; avoid LINQ/allocations in hot paths.
- Graphics: deterministically dispose all SharpDX objects; minimize resource churn (batch updates, long‑lived buffers).
- Formatting/lint: `dotnet format` and analyzers; keep warnings clean.

## Testing Guidelines
- Framework: xUnit in `tests/Helengine.*.Tests/` or adjacent `*.Tests` projects.
- Naming: `{Target}.Tests/*Tests.cs` (e.g., `Helengine.Core.Tests/RendererMemoryTests.cs`).
- Emphasize deterministic tests on public APIs and memory/latency‑critical paths; mock GPU where practical.
- Optional perf: BenchmarkDotNet for microbenchmarks (not required in CI).

## Commit & Pull Requests
- Conventional Commits: `feat:`, `fix:`, `perf:`, `refactor:`, `test:`, `chore:`.
- For engine/renderer changes, include before/after metrics: frame time, allocs/op, peak/steady RSS.
- PRs: clear description, linked issues (`Closes #123`), repro steps; UI changes add screenshot/GIF.

## Performance & Memory Constraints
- Design for constrained devices (NES → Xbox 360): typical subsystem budgets 64–256 KB.
- Avoid exceptions/RTTI in hot paths; favor branchless logic; keep the render thread the single owner of GPU objects.
- CI/Release: `dotnet build -c Release -p:TreatWarningsAsErrors=true`.

## Agent‑Specific Instructions
- Focus edits in `helengine.ui/helengine.editor.launcher` and renderer modules; keep patches surgical.
- Never introduce unbounded allocations; dispose SharpDX objects; update tests/docs alongside behavior changes.
