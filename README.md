# ChipCraft

**A music composition engine exposed as an MCP server, so an AI agent can compose, arrange and
export a piece of music the way a person would: a bar at a time, listening back as it goes.**

There is deliberately no one-shot "write me a song" endpoint. The agent creates or generates
material, inspects it, arranges it, previews it and exports it, using 74 tools across nine
groups. The determinism lives in the engine and the taste lives in the agent, which is the split
that makes the output reviewable instead of a black box.

.NET 10, 12,500 lines of C#, 102 test methods covering 180 cases.

---

## What the agent gets

| Group | Tools | What it does |
|---|---|---|
| **Theory** | 6 | Scales, keys, chords and a chord-progression database the agent can query rather than guess at |
| **Composition** | 16 | The tracker-grid canvas: place step notes, read them back, transpose, copy, merge, clear a channel, and generators for melody, bass, drums, harmony, arpeggio and pad as optional starting points |
| **Part** | 13 | The expressive layer over the grid: note durations, automation, patch overrides and targeted refinements |
| **Song** | 11 | Assemble patterns into playback order, edit the arrangement, and control tempo, meter, grid resolution and loop points |
| **Program** | 5 | SoundFont-aware channel patches and channel mix defaults |
| **Export** | 11 | MIDI and WAV out, per-pattern and per-order-entry renders, stems, loop previews and delivery bundles |
| **Review** | 6 | Musical and render analysis, so the agent can check its own work before exporting |
| **Persistence** | 3 | Save and load a song |
| **Session** | 3 | Session lifecycle |

Notes are MIDI-native throughout: `int MidiNumber` is the canonical representation, so nothing
is lost translating between an internal model and the file format. Patterns are a tracker-style
row and channel grid, sixteen rows to a bar at four rows per beat.

## Layout

```
src/ChipCraft.Engine/        theory, generators, sequencer, MIDI export, audio rendering
  Theory/                    scales, keys, chords, the progression database
  Generation/                melody, bass, harmony, arpeggio, pad, drum, accent generators
  Midi/                      MIDI exporter, GM program catalog, SoundFont renderer
  Sequencer/                 Song, Pattern, tracker grid cells, Part and automation model
  Models/                    Note, enums
src/ChipCraft.Mcp/           the MCP server (stdio transport)
src/ChipCraft.Cli/           render MIDI to WAV, and theory lookups
src/ChipCraft.Renderer.Wpf/  Windows utility: MIDI to WAV or MP3 with a chosen SoundFont
tests/                       engine and MCP server tests
```

**The engine library has zero MCP and zero CLI dependencies.** That is enforced as an
architecture rule, not a convention, which is what lets the same composition core sit behind the
MCP server, the CLI and the WPF renderer without any of them leaking into it.

## Build and test

```bash
dotnet build
dotnet test
```

Requires the .NET 10 SDK. Nullable reference types are on across the solution.

## Running the MCP server

The server speaks stdio. Point your client at the built executable:

```json
{
  "mcpServers": {
    "chipcraft": {
      "command": "dotnet",
      "args": ["run", "--project", "src/ChipCraft.Mcp"]
    }
  }
}
```

For Claude Code, `claude mcp add chipcraft -- dotnet run --project src/ChipCraft.Mcp` does the
same thing.

## Rendering audio

Rendering needs a general-MIDI SoundFont, which is not bundled. See
[`soundfonts/README.md`](soundfonts/README.md) for how to get one and why it is not committed.

```bash
dotnet run --project src/ChipCraft.Cli -- render \
  --input song.mid \
  --output song.wav \
  --soundfont soundfonts/your-bank.sf2
```

`theory` is the other CLI command: scale, chord, key and progression lookups.

## Dependencies

[MeltySynth](https://github.com/sinshu/meltysynth) for SoundFont synthesis,
[NAudio](https://github.com/naudio/NAudio) for audio and MIDI I/O, and the
[ModelContextProtocol](https://github.com/modelcontextprotocol/csharp-sdk) C# SDK for the server.

## License

[MIT](LICENSE), covering the code in this repository. SoundFonts are not distributed here and
carry their own terms; see `soundfonts/README.md`.
