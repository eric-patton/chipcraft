# ChipCraft Agent Guide

## Project Summary

ChipCraft is a .NET 10 music composition codebase with three main executables:

- `src/ChipCraft.Engine/`: core library for theory, generators, sequencing, MIDI export, and audio rendering
- `src/ChipCraft.Cli/`: command-line entry point for theory utilities and MIDI-to-WAV rendering
- `src/ChipCraft.Mcp/`: MCP server that exposes composition and theory tools over stdio
- `src/ChipCraft.Renderer.Wpf/`: Windows WPF utility for rendering MIDI files to WAV or MP3 with a selected `.sf2` SoundFont

The main test suites live in `tests/ChipCraft.Engine.Tests/` and `tests/ChipCraft.Mcp.Tests/`.

## Source Of Truth

Prefer the code over ad hoc docs and generated output.

- `Program.cs` files and `.csproj` files are the authoritative source for available commands, dependencies, and app shape.
- `src/ChipCraft.Mcp/publish/` and `src/ChipCraft.Mcp/publish2/` are generated artifacts, not source.
- `sample-outputs/`, `sheet-music-samples/`, and `soundfonts/` are reference assets; avoid treating them as implementation guidance.
- `soundfonts/README.md` currently mentions a `compose` CLI command that does not exist in `src/ChipCraft.Cli/Program.cs`.

## Build And Validation

Prefer project-level validation from the repo root:

```powershell
dotnet build src\ChipCraft.Engine\ChipCraft.Engine.csproj
dotnet build src\ChipCraft.Cli\ChipCraft.Cli.csproj
dotnet build src\ChipCraft.Mcp\ChipCraft.Mcp.csproj
dotnet build src\ChipCraft.Renderer.Wpf\ChipCraft.Renderer.Wpf.csproj
dotnet test tests\ChipCraft.Engine.Tests\ChipCraft.Engine.Tests.csproj -p:CollectCoverage=false
dotnet test tests\ChipCraft.Mcp.Tests\ChipCraft.Mcp.Tests.csproj -p:CollectCoverage=false
dotnet run --project src\ChipCraft.Cli -- theory scale C NaturalMinor
dotnet run --project src\ChipCraft.Cli -- theory chord Am
dotnet run --project src\ChipCraft.Mcp
```

A `ChipCraft.slnx` file exists, but if solution-level `dotnet build` behaves oddly in your environment, fall back to the per-project commands above.

## Architecture Guardrails

- Keep `ChipCraft.Engine` free of CLI- or MCP-specific dependencies.
- Put core music logic in the engine, not in the CLI or MCP layers.
- Keep CLI and MCP projects as thin orchestration layers around engine types.
- Treat the MCP server as a manual-first composition toolbox. Favor small controllable tools over opaque one-shot song generation.
- Keep the dual authoring model coherent: tracker grid tools should remain grid-only, while expressive `Part`/automation logic belongs in the event layer.
- Notes are MIDI-native. `int MidiNumber` is the canonical note representation.
- Patterns use a tracker-style grid. `16` rows equals `1` bar at `4` rows per beat.
- Audio rendering depends on external `.sf2` SoundFont files; do not assume a specific local SoundFont exists unless the task adds or references one explicitly.

## Code Style

- Target framework is `net10.0` with nullable reference types and implicit usings enabled via `Directory.Build.props`.
- Match the existing C# style: file-scoped namespaces, concise XML doc comments on public types/methods, and straightforward constructor/property patterns.
- Prefer small focused changes over broad refactors.
- If behavior changes in theory, generation, MIDI export, or sequencing, update or add tests in `tests/ChipCraft.Engine.Tests/`.

## Testing Guidance

- Favor engine-level tests for deterministic behavior, and add MCP tests when the tool contract or exported artifact bundle changes.
- For file output tests, use temporary directories and clean them up, following the existing MIDI exporter tests.
- Avoid introducing tests that require heavyweight local assets unless the feature specifically depends on them.

## Practical Repo Notes

- Large binary assets already exist in `soundfonts/`; avoid adding more generated or publish output unless the task explicitly requires it.
- If you change public behavior exposed by MCP tools or CLI commands, verify the corresponding entry points in `src/ChipCraft.Mcp/Tools/` or `src/ChipCraft.Cli/Commands/`.
- Manual composition flows now rely on pattern/song/program/export tools plus review tools. Do not assume a high-level `compose_song` MCP entry point exists.
- If you change desktop rendering behavior, verify `src/ChipCraft.Renderer.Wpf/` still launches and that the render path still matches `ChipCraft.Engine.Midi.AudioRenderer`.
- `CLAUDE.md` contains a useful architecture overview; keep `AGENTS.md` aligned with it when updating either file.
